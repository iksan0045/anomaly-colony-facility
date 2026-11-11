using System.Text;
using UnityEngine;

/// DETENTION (1x1, biru): unit "diam", mood naik.
public class Cell_Detention : CellBase
{
    public override CellType Type { get { return CellType.Detention; } }
    public override int MoodPerTick { get { return GameConfig.MoodDetentionPerTick; } }

    public override void OnTick(GameManager gm) { }
    protected override bool IsActive { get { return units.Count > 0; } }

    public override int AuraMoodFor(UnitData target)
    {
        if (target.cell == null) return 0;
        if (Vector2.Distance(Center, target.cell.Center) > GameConfig.BreederAuraRadius) return 0;

        int n = 0;
        foreach (var u in units)
            if (u.type == UnitType.Breeder && u != target) n++;   // Breeder tidak menguatkan dirinya sendiri
        return n * GameConfig.BreederAuraMood;
    }
    protected override void DescribeDetails(StringBuilder sb)
    {
        sb.AppendLine("Mood +" + GameConfig.MoodDetentionPerTick + "/tick for units inside");
        sb.AppendLine("Aura Breeder: +" + GameConfig.BreederAuraMood + " mood (radius " + GameConfig.BreederAuraRadius + ")");
    }
}
