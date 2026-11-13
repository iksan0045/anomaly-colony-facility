using System.Text;
using UnityEngine;

/// Mood koloni (satu angka 0..100 untuk seluruh fasilitas), menggantikan mood per unit.
///
/// Mood bergerak pelan menuju TARGET yang dihitung dari kondisi fasilitas:
///   Base + (unit di Detention) + (Breeder) + (Leader) - (kelaparan)
/// Kejadian besar (raid, breeding) memberi GUNCANGAN langsung lewat Shock().
/// Efek: pengali output Production, kekuatan raid, dan peluang breeding memakai GameConfig.MoodFactor(Value).
public class ColonyMood
{
    public float Value = GameConfig.ColonyMoodStart;      // 0..100
    public float Target { get; private set; }
    public string Breakdown { get; private set; }

    public int Rounded { get { return Mathf.RoundToInt(Value); } }

    public string Trend
    {
        get
        {
            if (Value < Target - 0.5f) return "rising";
            if (Value > Target + 0.5f) return "falling";
            return "stable";
        }
    }

    /// Guncangan sesaat: positif = naik, negatif = turun. Dipakai raid dan breeding.
    public void Shock(float delta)
    {
        Value = Mathf.Clamp(Value + delta, 0f, 100f);
    }

    /// Dipanggil GameManager.DoTick sekali per tick.
    public void Tick(GameManager gm)
    {
        int resting = 0, breeders = 0;
        bool leader = false;
        foreach (var u in gm.units)
        {
            if (u.away) continue;
            if (u.cell != null && u.cell.Type == CellType.Detention && u.Arrived) resting++;
            if (u.Is(UnitType.Breeder)) breeders++;
            if (u.type == UnitType.Leader) leader = true;
        }

        float rest = Mathf.Min(resting * GameConfig.MoodPerDetentionUnit, GameConfig.DetentionMoodCap);
        float calm = Mathf.Min(breeders * GameConfig.BreederCalm, GameConfig.BreederCalmCap);
        float lead = leader ? GameConfig.LeaderPresence : 0f;
        float hunger = gm.Starving ? GameConfig.ColonyStarvationPenalty : 0f;

        Target = Mathf.Clamp(GameConfig.ColonyMoodBase + rest + calm + lead - hunger, 0f, 100f);
        Value = Mathf.MoveTowards(Value, Target, Value < Target ? GameConfig.ColonyMoodUp : GameConfig.ColonyMoodDown);

        var sb = new StringBuilder();
        sb.AppendLine("<b>Colony mood: " + Rounded + "</b>  (" + Trend + ", target " + Mathf.RoundToInt(Target) + ")");
        sb.AppendLine("Base  " + Signed(GameConfig.ColonyMoodBase));
        if (rest > 0f)  sb.AppendLine("Units resting in Detention (" + resting + ")  " + Signed(rest));
        if (calm > 0f)  sb.AppendLine("Breeders (" + breeders + ")  " + Signed(calm));
        if (lead > 0f)  sb.AppendLine("Leader present  " + Signed(lead));
        if (hunger > 0f) sb.AppendLine("<color=#ff6666>Starvation  " + Signed(-hunger) + "</color>");
        sb.AppendLine("Production, raids and breeding x" + GameConfig.MoodFactor(Value).ToString("0.00"));
        Breakdown = sb.ToString();
    }

    static string Signed(float v) { return (v >= 0f ? "+" : "") + Mathf.RoundToInt(v); }
}
