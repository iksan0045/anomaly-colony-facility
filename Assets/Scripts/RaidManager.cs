using System.Linq;
using UnityEngine;

/// Raid auto-resolve: total power semua FIGHTER vs musuh (3 + raidCount*2).
public class RaidManager : MonoBehaviour
{
    public static RaidManager Instance { get; private set; }

    public int RaidCount { get; private set; }
    public int CooldownLeft { get; private set; }

    public int EnemyPower { get { return GameConfig.RaidBasePower + RaidCount * GameConfig.RaidPowerPerRaid; } }

    public int FighterPower
    {
        get { return GameManager.Instance.units.Where(u => u.type == UnitType.Fighter).Sum(u => u.power); }
    }

    void Awake() { Instance = this; }

    /// Dipanggil GameManager tiap tick.
    public void OnTick()
    {
        if (CooldownLeft > 0) CooldownLeft--;
    }

    public void DoRaid()
    {
        if (CooldownLeft > 0) return;
        var gm = GameManager.Instance;
        CooldownLeft = GameConfig.RaidCooldownTicks;

        int enemy = EnemyPower;
        RaidCount++;
        var fighters = gm.units.Where(u => u.type == UnitType.Fighter).ToList();
        int total = fighters.Sum(f => f.power);

        if (total > enemy)
        {
            if (gm.units.Count >= gm.MaxUnits)
            {
                gm.Log("Raid won (" + total + " vs " + enemy + ") tapi facility penuh, tidak ada capture");
            }
            else
            {
                int power = Mathf.Clamp(Random.Range(2, 7) + RaidCount / 2, 1, 10);
                var u = gm.AddUnit(UnitData.Create(GameManager.RandomType(), power));
                gm.Log("Raid won (" + total + " vs " + enemy + "). Unit captured: " + u.Label + " (P" + u.power + ")");
            }
        }
        else
        {
            string msg = "Raid lost (" + total + " vs " + enemy + ")";
            if (fighters.Count > 0)
            {
                var f = fighters[Random.Range(0, fighters.Count)];
                f.power = Mathf.Max(1, f.power - GameConfig.FighterPowerLoss);
                msg += ". " + f.Label + " power -" + GameConfig.FighterPowerLoss;
            }
            gm.Log(msg);
        }
        gm.NotifyChanged();
    }
}
