using System.Text;

/// DETENTION (1x1, biru): unit "diam", mood naik.
public class Cell_Detention : CellBase
{
    public override CellType Type { get { return CellType.Detention; } }
    public override int MoodPerTick { get { return GameConfig.MoodDetentionPerTick; } }

    public override void OnTick(GameManager gm) { }
    protected override bool IsActive { get { return units.Count > 0; } }

    protected override void DescribeDetails(StringBuilder sb)
    {
        sb.AppendLine("Mood +" + GameConfig.MoodDetentionPerTick + "/tick untuk unit di dalam");
    }
}
