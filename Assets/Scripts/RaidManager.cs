using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RaidProtocol { Safe, Aggressive }
public enum RaidOutcome { Perfect, Standard, Costly, Failure, Catastrophic }

/// Raid dengan persiapan: target (RaidTarget), squad Fighter pilihan, protokol Safe/Aggressive.
/// Hasil: Perfect / Standard / Costly / Failure / Catastrophic. Unit spesial hanya didapat dari sini.
///
/// Rumus win chance: 50% saat power squad = power musuh, +/-50% per selisih rasio, lalu
/// Safe -10% atau Aggressive +15%, dibatasi 5%..95%. Semua angka ada di method di bawah, mudah di-tuning.
public class RaidManager : MonoBehaviour
{
    public static RaidManager Instance { get; private set; }

    public int RaidCount { get; private set; }
    public int CooldownLeft { get; private set; }
    public string LastResult { get; private set; }

    RaidTarget current;
    RaidTarget fallback;

    GameManager gm { get { return GameManager.Instance; } }

    void Awake() { Instance = this; }

    /// Dipanggil GameManager tiap tick.
    public void OnTick() { if (CooldownLeft > 0) CooldownLeft--; }

    // ================= Target =================

    public RaidTarget CurrentTarget
    {
        get
        {
            if (current == null) RollTarget();
            return current;
        }
    }

    RaidTarget[] AllTargets()
    {
        var db = gm.database;
        return (db != null && db.raidTargets != null) ? db.raidTargets : new RaidTarget[0];
    }

    void RollTarget()
    {
        var pool = AllTargets().Where(t => t != null && t.minRaidCount <= RaidCount).ToList();
        if (pool.Count == 0)
        {
            if (fallback == null) fallback = RaidTarget.CreateDefault();
            current = fallback;
            return;
        }

        float total = pool.Sum(t => t.weight);
        float r = Random.value * total;
        foreach (var t in pool)
        {
            r -= t.weight;
            if (r <= 0f) { current = t; return; }
        }
        current = pool[pool.Count - 1];
    }

    /// Dipakai SaveManager. targetName kosong/tidak ketemu = target baru diundi saat dibutuhkan.
    public void Restore(int count, int cooldown, string targetName)
    {
        RaidCount = count;
        CooldownLeft = cooldown;
        current = AllTargets().FirstOrDefault(t => t != null && t.name == targetName);
    }

    // ================= Statistik =================

    public int EnemyPower
    {
        get
        {
            var t = CurrentTarget;
            float p = (GameConfig.RaidBasePower + RaidCount * GameConfig.RaidPowerPerRaid) * t.powerMultiplier;
            if (t.anomalyClass == AnomalyClass.Armored) p *= 1.25f;
            return Mathf.Max(1, Mathf.RoundToInt(p));
        }
    }

    /// Total power mentah semua Fighter (tanpa efek tipe anomali).
    public int FighterPower
    {
        get { return gm.units.Where(u => u.type == UnitType.Fighter).Sum(u => u.power); }
    }

    /// Kontribusi satu Fighter terhadap target saat ini (setelah efek tipe anomali).
    public float Contribution(UnitData u)
    {
        float p = u.power;
        switch (CurrentTarget.anomalyClass)
        {
            case AnomalyClass.HighMobility:
                if (u.power < 5) p *= 0.5f;
                break;
            case AnomalyClass.Psychic:
                p *= 0.6f + 0.4f * (u.mood / 100f);
                break;
        }
        return p;
    }

    public float SquadPower(IEnumerable<UnitData> squad)
    {
        float s = 0f;
        foreach (var u in squad) s += Contribution(u);
        return s;
    }

    public float WinChance(float squadPower, RaidProtocol protocol)
    {
        float ratio = squadPower / EnemyPower;
        float c = 0.5f + (ratio - 1f) * 0.5f;
        c += protocol == RaidProtocol.Aggressive ? 0.15f : -0.10f;
        return Mathf.Clamp(c, 0.05f, 0.95f);
    }

    // ================= Resolve =================

    public void Launch(List<UnitData> squad, RaidProtocol protocol)
    {
        if (CooldownLeft > 0 || squad == null || squad.Count == 0) return;

        var t = CurrentTarget;
        float chance = WinChance(SquadPower(squad), protocol);
        RaidOutcome outcome = RollOutcome(chance, protocol, t);

        CooldownLeft = GameConfig.RaidCooldownTicks;
        string msg = Apply(outcome, squad, protocol, t);   // bisa menambah cooldown (Catastrophic)

        RaidCount++;
        LastResult = msg
            + "\n\nSite: " + t.displayName + "  (" + t.anomalyClass + ")"
            + "\nProtocol: " + protocol + "   Win chance: " + Mathf.RoundToInt(chance * 100f) + "%";
        gm.Log(msg);

        RollTarget();   // target berikutnya
        gm.NotifyChanged();
    }

    RaidOutcome RollOutcome(float winChance, RaidProtocol p, RaidTarget t)
    {
        bool unstable = t.anomalyClass == AnomalyClass.Unstable;
        bool safe = p == RaidProtocol.Safe;
        float r = Random.value;

        if (r < winChance)
        {
            float q = r / winChance;   // 0 = sangat beruntung, 1 = menang tipis
            float perfect = 0.20f + (safe ? 0.10f : 0f) + (unstable ? 0.10f : 0f);
            float costly = 0.70f + (safe ? 0.10f : -0.10f);
            if (q < perfect) return RaidOutcome.Perfect;
            if (q > costly) return RaidOutcome.Costly;
            return RaidOutcome.Standard;
        }

        float cat = 0.08f + (unstable ? 0.10f : 0f) + (p == RaidProtocol.Aggressive ? 0.07f : 0f);
        return Random.value < cat ? RaidOutcome.Catastrophic : RaidOutcome.Failure;
    }

    string Apply(RaidOutcome o, List<UnitData> squad, RaidProtocol p, RaidTarget t)
    {
        string site = t.displayName;
        switch (o)
        {
            case RaidOutcome.Perfect:
            {
                var u = Capture(t, p, o);
                return u == null
                    ? "Perfect result at " + site + ", but the facility is full - nothing captured."
                    : "PERFECT CAPTURE at " + site + "! " + u.Label + " joined the facility. No losses.";
            }
            case RaidOutcome.Standard:
            {
                var u = Capture(t, p, o);
                return u == null
                    ? "Raid on " + site + " won, but the facility is full - nothing captured."
                    : "Standard capture at " + site + ": " + u.Label + " joined the facility.";
            }
            case RaidOutcome.Costly:
            {
                var u = Capture(t, p, o);
                var hurt = Hurt(squad, 2);
                AddMood(squad, -15);
                return (u == null ? "Costly victory at " + site + " (facility full, nothing captured)"
                                  : "Costly victory at " + site + ": " + u.Label + " captured")
                       + ", but " + hurt.Label + " lost 2 power and the squad is shaken.";
            }
            case RaidOutcome.Failure:
            {
                int dmg = p == RaidProtocol.Aggressive ? 4 : 2;
                var hurt = Hurt(squad, dmg);
                return "Raid on " + site + " failed. " + hurt.Label + " lost " + dmg + " power.";
            }
            default: // Catastrophic
            {
                var a = Hurt(squad, 3);
                var rest = squad.Where(x => x != a).ToList();
                var b = rest.Count > 0 ? Hurt(rest, 3) : null;
                AddMood(squad, -30);
                CooldownLeft += 10;
                return "CATASTROPHE at " + site + "! " + a.Label + (b != null ? " and " + b.Label : "")
                       + " badly hurt (-3 power), squad morale shattered. Recovery takes longer.";
            }
        }
    }

    /// Unit hasil raid: spesial (dari drop target) atau unit biasa. null = fasilitas penuh.
    UnitData Capture(RaidTarget t, RaidProtocol p, RaidOutcome o)
    {
        if (gm.units.Count >= gm.MaxUnits) return null;

        float sc = t.specialChance * (o == RaidOutcome.Perfect ? 2f : 1f) * (p == RaidProtocol.Safe ? 1.5f : 1f);
        var special = RollSpecial(t, Mathf.Clamp01(sc));
        if (special != null) return gm.AddUnit(UnitData.CreateSpecial(special));

        int bonus = t.powerBonus
                  + (o == RaidOutcome.Perfect ? 2 : 0)
                  + (p == RaidProtocol.Safe ? 1 : 0)
                  + (t.anomalyClass == AnomalyClass.Armored ? 1 : 0);
        int power = Mathf.Clamp(Random.Range(2, 7) + RaidCount / 2 + bonus, 1, 10);
        return gm.AddUnit(UnitData.Create(t.RollRewardType(), power));
    }

    SpecialUnit RollSpecial(RaidTarget t, float chance)
    {
        if (t.specialDrops == null || t.specialDrops.Length == 0 || Random.value > chance) return null;

        var pool = t.specialDrops
            .Where(s => s != null && s.type != UnitType.Leader
                        && !(s.unique && gm.units.Exists(u => u.special == s)))
            .ToList();
        return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
    }

    static UnitData Hurt(List<UnitData> squad, int amount)
    {
        var u = squad[Random.Range(0, squad.Count)];
        u.power = Mathf.Max(1, u.power - amount);
        return u;
    }

    static void AddMood(List<UnitData> squad, int delta)
    {
        foreach (var u in squad) u.mood = Mathf.Clamp(u.mood + delta, 0, 100);
    }
}