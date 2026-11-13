using UnityEngine;

/// Otomasi kru (versi ringan, setelah mood menjadi milik koloni). Tidak ada lagi unit yang
/// "lelah" dan pergi istirahat sendiri; pemain mengatur siapa di Detention lewat penugasan manual.
///
///   - Producer / Breeder / Leader = peran manual: hanya bekerja di pos pilihan pemain.
///   - Fighter (non-Breeder/Producer) = otomatis: mengisi slot Production yang kosong (Material),
///     kecuali pemain melepasnya sendiri (holdFree).
///   - Unit yang punya pos tapi sedang tidak di cell (mis. pulang raid atau selesai breeding)
///     kembali ke posnya. Penghuni pos boleh mengusir unit yang diisi otomatis (autoPost).
///
/// Dipanggil GameManager.DoTick setelah cell.OnTick:  Crew.Tick(this);
public static class Crew
{
    public static bool IsManualRole(UnitData u)
    {
        return u.type == UnitType.Leader || u.Is(UnitType.Producer) || u.Is(UnitType.Breeder);
    }

    public static void Tick(GameManager gm)
    {
        var grid = GridManager.Instance;
        if (grid == null) return;

        foreach (var u in gm.units)
        {
            if (u.away || u.cell != null || gm.Tick < u.offDutyUntil) continue;

            if (u.home != null) GoHome(u);
            else if (!IsManualRole(u) && !u.holdFree) AutoWork(u, grid);
        }
    }

    static void GoHome(UnitData u)
    {
        string why;
        u.home.TryAssign(u, false, out why);   // diam-diam: gagal = coba lagi tick berikutnya
    }

    static void AutoWork(UnitData u, GridManager grid)
    {
        string why;
        foreach (var c in grid.cells)
        {
            if (c.Type != CellType.Production || !c.TryAssign(u, false, out why)) continue;
            u.home = c;
            u.autoPost = true;   // boleh diusir Producer yang ditugaskan pemain
            return;
        }
    }
}
