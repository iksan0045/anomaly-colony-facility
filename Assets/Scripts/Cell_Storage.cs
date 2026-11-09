using System.Text;

/// STORAGE (1x1, kuning): +25 max resource.
public class Cell_Storage : CellBase
{
    public override CellType Type { get { return CellType.Storage; } }

    public override void OnPlaced(GameManager gm) { gm.RegisterStorage(); }
    public override void OnTick(GameManager gm) { }

    protected override void DescribeDetails(StringBuilder sb)
    {
        sb.AppendLine("+" + GameConfig.StorageBonus + " max resource");
    }
}
