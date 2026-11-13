using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RaidProtocol { Safe, Aggressive }
public enum RaidOutcome { Perfect, Standard, Costly, Failure, Catastrophic }

/// Status raid yang sedang berjalan. Disimpan SaveManager supaya hasil yang sudah diundi
/// tidak bisa di-reroll lewat Load.
[System.Serializable]
public class RaidSave
{
    public bool inProgress;
    public float elapsed, duration, chance;
    public int protocol, outcome;
    public int[] squadIds;
}

/// Raid berjalan di latar belakang: squad pergi (hilang dari cell dan dunia), progres jalan
/// sendiri, hasil siap dilihat lewat tombol RAID. Hasil diundi di awal, diterapkan di akhir.
///
/// Rumus win chance: 50% saat power efektif squad = power musuh, +/-50% per selisih rasio,
/// lalu Safe -10% atau Aggressive +15%, dibatasi 5%..95%. Power efektif tiap Fighter dikali
/// GameConfig.MoodFactor(mood). Target dan RaidCount hanya maju saat MENANG.
public class RaidManager : MonoBehaviour
{
    public static RaidManager Instance { get; private set; }

    public int RaidCount { get; private set; }
    public int CooldownLeft { get; private set; }
    public string LastResult { get; private set; }

    public bool InProgress { get; private set; }
    public bool ResultPending { get; private set; }       // hasil siap dilihat, belum dibuka
    public RaidOutcome LastOutcome { get; private set; }
    public UnitData LastCaptured { get; private set; }
    public int LastSalvage { get; private set; }
    public readonly List<UnitData> LastHurt = new List<UnitData>();

    public List<UnitData> CurrentSquad { get { return pendingSquad; } }
    public float Progress { get { return InProgress && duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f; } }

    RaidTarget current;
    RaidTarget fallback;

    RaidTarget pendingTarget;
    List<UnitData> pendingSquad = new List<UnitData>();
    RaidProtocol pendingProtocol;
    RaidOutcome pendingOutcome;
    float pendingChance, elapsed, duration;

    GameManager gm { get { return GameManager.Instance; } }

    void Awake() { Instance = this; }

    void Update()
    {
        if (!InProgress) return;
        elapsed += Time.deltaTime;
        if (elapsed >= duration) Finish();
    }

    /// Dipanggil GameManager tiap tick.
    public void OnTick() { if (CooldownLeft > 0) CooldownLeft--; }

    public void AckResult() { ResultPending = false; }

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

    // ================= Save =================

    public RaidSave Export()
    {
        var s = new RaidSave();
        if (!InProgress) return s;
        s.inProgress = true;
        s.elapsed = elapsed;
        s.duration = duration;
        s.chance = pendingChance;
        s.protocol = (int)pendingProtocol;
        s.outcome = (int)pendingOutcome;
        s.squadIds = pendingSquad.Select(u => u.id).ToArray();
        return s;
    }

    /// Panggil setelah Restore() dan setelah semua unit dibuat (map = id -> UnitData).
    public void Import(RaidSave s, Dictionary<int, UnitData> map)
    {
        InProgress = false;
        if (s == null || !s.inProgress || s.squadIds == null) return;

        var team = new List<UnitData>();
        foreach (int id in s.squadIds)
        {
            UnitData u;
            if (map.TryGetValue(id, out u)) { team.Add(u); SendAway(u); }
        }
        if (team.Count == 0) return;

        pendingSquad = team;
        pendingTarget = CurrentTarget;
        pendingProtocol = (RaidProtocol)s.protocol;
        pendingOutcome = (RaidOutcome)s.outcome;
        pendingChance = s.chance;
        elapsed = s.elapsed;
        duration = s.duration;
        InProgress = true;
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

    /// Total power mentah semua Fighter yang ada di fasilitas (tanpa efek mood/tipe anomali).
    public int FighterPower
    {
        get { return gm.units.Where(u => u.Is(UnitType.Fighter) && !u.away).Sum(u => u.power); }
    }

    /// Fighter boleh dikirim: peran Fighter, ada di fasilitas, dan mood cukup.
    public bool CanDeploy(UnitData u)
    {
        return u.Is(UnitType.Fighter) && !u.away;
    }

    /// Kontribusi satu Fighter terhadap target saat ini (mood dan tipe anomali ikut menghitung).
    public float Contribution(UnitData u)
    {
        float p = u.power * gm.MoodFactor;
        switch (CurrentTarget.anomalyClass)
        {
            case AnomalyClass.HighMobility:
                if (u.power < 5) p *= 0.5f;
                break;
            case AnomalyClass.Psychic:
                p *= gm.MoodFactor;   // sensitif ganda terhadap mood
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

    // ================= Alur: Begin -> (latar belakang) -> Finish =================

    /// Berangkatkan squad. Hasil diundi sekarang, diterapkan saat progres selesai.
    public bool Begin(List<UnitData> squad, RaidProtocol protocol)
    {
        if (InProgress || CooldownLeft > 0 || squad == null) return false;
        if (gm.units.Count >= gm.MaxUnits)
        {
            gm.Log("Facility full - free a unit slot before raiding.");
            return false;
        }
        if (gm.Mood.Value < GameConfig.MinRaidMood) { gm.Log("Morale too low to launch a raid."); return false; }
        var team = squad.Where(CanDeploy).ToList();
        if (team.Count == 0) return false;

        pendingTarget = CurrentTarget;
        pendingSquad = team;
        pendingProtocol = protocol;
        pendingChance = WinChance(SquadPower(team), protocol);
        pendingOutcome = RollOutcome(pendingChance, protocol, pendingTarget);

        duration = GameConfig.RaidDurationSeconds * (protocol == RaidProtocol.Aggressive ? 0.7f : 1f);
        elapsed = 0f;
        ResultPending = false;

        foreach (var u in team) SendAway(u);
        InProgress = true;

        gm.Log("Squad of " + team.Count + " sent to " + pendingTarget.displayName + ".");
        gm.NotifyChanged();
        return true;
    }

    void Finish()
    {
        if (!InProgress) return;
        InProgress = false;
        LastCaptured = null;
        LastSalvage = 0;
        LastHurt.Clear();

        var t = pendingTarget;
        var squad = pendingSquad;
        foreach (var u in squad) Recall(u);

        bool won = pendingOutcome == RaidOutcome.Perfect
                || pendingOutcome == RaidOutcome.Standard
                || pendingOutcome == RaidOutcome.Costly;

        CooldownLeft = GameConfig.RaidCooldownTicks;
        string msg = Apply(pendingOutcome, squad, pendingProtocol, t);   // bisa menambah cooldown (Catastrophic)
        AddMood(squad, -GameConfig.RaidMoodCost);                        // setiap raid melelahkan

        LastSalvage = SalvageReward(pendingOutcome, t, LastCaptured != null);
        if (LastSalvage > 0)
        {
            gm.AddSalvage(LastSalvage);
            msg += " +" + LastSalvage + " Salvage.";
        }

        LastOutcome = pendingOutcome;
        LastResult = msg
            + "\n\nSite: " + t.displayName + "  (" + t.anomalyClass + ")"
            + "\nProtocol: " + pendingProtocol + "   Win chance: " + Mathf.RoundToInt(pendingChance * 100f) + "%"
            + (won ? "" : "\nThe target is still active. Try again after recovery.");
        gm.Log(msg);

        if (won)
        {
            int mat = RaidTarget.RaidMaterialReward + RaidCount;
            gm.AddMaterials(mat);
            msg += " +" + mat + " Material.";
        }

        ResultPending = true;
        gm.NotifyChanged();
        // Disarankan: panggil autosave di sini supaya hasil raid tidak bisa dibatalkan lewat Load.
    }

    void SendAway(UnitData u)
    {
        if (u.cell != null) u.cell.RemoveUnit(u, false);
        u.away = true;
        if (u.controller != null) u.controller.gameObject.SetActive(false);
    }

    void Recall(UnitData u)
    {
        u.away = false;
        if (u.controller != null) u.controller.gameObject.SetActive(true);
    }

    // ================= Hasil =================

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

    /// Salvage = bahan bangun cell. Hanya didapat dari raid yang menang.
    int SalvageReward(RaidOutcome o, RaidTarget t, bool captured)
    {
        int s;
        switch (o)
        {
            case RaidOutcome.Perfect:  s = 8; break;
            case RaidOutcome.Standard: s = 5; break;
            case RaidOutcome.Costly:   s = 3; break;
            default: return 0;
        }
        s += RaidCount / 3;                                    // target makin jauh = loot makin banyak
        if (t.anomalyClass == AnomalyClass.Armored) s += 2;
        if (!captured) s += 2;                                 // fasilitas penuh saat selesai: ganti rugi
        return s;
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
        if (special != null)
        {
            LastCaptured = gm.AddUnit(UnitData.CreateSpecial(special));
            return LastCaptured;
        }

        int bonus = t.powerBonus
                  + (o == RaidOutcome.Perfect ? 2 : 0)
                  + (p == RaidProtocol.Safe ? 1 : 0)
                  + (t.anomalyClass == AnomalyClass.Armored ? 1 : 0);
        int power = Mathf.Clamp(Random.Range(2, 7) + RaidCount / 2 + bonus, 1, 10);
        var arch = UnitArchetype.RollFrom(t.rewardPool, p == RaidProtocol.Safe || o == RaidOutcome.Perfect);
        var type = arch != null ? arch.type : t.RollRewardType();
        if (arch != null) power = Mathf.Clamp(power, arch.powerRange.x, arch.powerRange.y);

        var unit = UnitData.Create(type, power);
        if (arch != null) unit.ApplyArchetype(arch);       // model sama, portrait/nama/stat berbeda
        LastCaptured = gm.AddUnit(unit);
        return LastCaptured;
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

    UnitData Hurt(List<UnitData> squad, int amount)
    {
        var u = squad[Random.Range(0, squad.Count)];
        u.power = Mathf.Max(1, u.power - amount);
        if (!LastHurt.Contains(u)) LastHurt.Add(u);
        return u;
    }

    static void AddMood(List<UnitData> squad, int delta)
    {
        foreach (var u in squad) u.mood = Mathf.Clamp(u.mood + delta, 0, 100);
    }
}
