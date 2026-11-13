using System.Text;

/// PRODUCTION (1x2, hijau): PRODUCER -> Energy, FIGHTER -> Material, per tick.
public class Cell_Production : CellBase
{
    public override CellType Type { get { return CellType.Production; } }

    public override bool CanAccept(UnitData u, out string reason)
    {
        if (!base.CanAccept(u, out reason)) return false;
        if (!u.Is(UnitType.Producer) && !u.Is(UnitType.Fighter))
        { reason = "This unit cannot produce"; return false; }
        return true;
    }

    void Output(UnitData u, out int energy, out int material)
    {
        energy = material = 0;
        if (u.mood <= 0) return;
        if (u.Is(UnitType.Producer)) energy   += GameConfig.EnergyPerProducer;
        if (u.Is(UnitType.Fighter))  material += GameConfig.MaterialPerFighter;
    }

    float carryE, carryM;

    public override void OnTick(GameManager gm)
    {
        float f = gm.MoodFactor * Efficiency;   // hapus '* Efficiency' jika jaringan belum dipasang
        foreach (var u in units)
        {
            if (!u.Arrived) continue;
            int e, m;
            Output(u, out e, out m);
            carryE += e * f;
            carryM += m * f;
        }
        int ie = (int)carryE, im = (int)carryM;
        if (ie > 0) { gm.AddEnergy(ie);    carryE -= ie; }
        if (im > 0) { gm.AddMaterials(im); carryM -= im; }
    }

    protected override void DescribeDetails(StringBuilder sb)
    {
        int e = 0, m = 0;
        foreach (var u in units)
        {
            int ue, um;
            Output(u, out ue, out um);
            e += ue; m += um;
        }
        sb.AppendLine("Output: +" + e + " Energy, +" + m + " Material /tick");
    }

    protected override bool ShouldAnimate(UnitData u)
    {
        return u.mood > 0; //!GameManager.Instance.Starving && 
    }
}
