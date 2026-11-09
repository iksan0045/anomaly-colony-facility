using System.Text;

/// PRODUCTION (1x2, hijau): PRODUCER -> Energy, FIGHTER -> Material, per tick.
public class Cell_Production : CellBase
{
    public override CellType Type { get { return CellType.Production; } }

    public override bool CanAccept(UnitData u, out string reason)
    {
        if (!base.CanAccept(u, out reason)) return false;
        if (u.type == UnitType.Breeder || u.type == UnitType.Leader) { reason = "Unit ini tidak bisa produksi"; return false; }
        if (u.type == UnitType.Breeder) { reason = "Breeder tidak bisa produksi"; return false; }
        return true;
    }

    public override void OnTick(GameManager gm)
    {
        foreach (var u in units)
        {
            if (u.mood <= 0) continue; // mood habis = mogok
            if (u.type == UnitType.Producer) gm.AddEnergy(1);
            else if (u.type == UnitType.Fighter) gm.AddMaterials(1);
        }
    }

    protected override void DescribeDetails(StringBuilder sb)
    {
        int e = 0, m = 0;
        foreach (var u in units)
        {
            if (u.mood <= 0) continue;
            if (u.type == UnitType.Producer) e++;
            else if (u.type == UnitType.Fighter) m++;
        }
        sb.AppendLine("Output: +" + e + " Energy, +" + m + " Material /tick");
    }
}
