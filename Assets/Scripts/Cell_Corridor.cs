/// CORRIDOR (1x1, abu-abu): tidak menampung unit, tidak berproduksi. Hanya menyambungkan cell
/// ke jaringan fasilitas (lihat FacilityNetwork). Murah, tapi tiap corridor menambah jarak ke Control.
public class Cell_Corridor : CellBase
{
    public override CellType Type { get { return CellType.Corridor; } }

    public override void OnTick(GameManager gm) { }
}
