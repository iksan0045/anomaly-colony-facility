using System.Text;

/// CONTROL (1x1, merah): unlock tipe cell baru, setelah semua terbuka +max unit.
public class Cell_Control : CellBase
{
    public override CellType Type { get { return CellType.Control; } }

    public override void OnPlaced(GameManager gm) { gm.RegisterControl(); }
    public override void OnTick(GameManager gm) { }

    protected override void DescribeDetails(StringBuilder sb)
    {
        sb.AppendLine("Unlocks new cell types +" + GameConfig.ControlUnitBonus + " max units");
    }
}
