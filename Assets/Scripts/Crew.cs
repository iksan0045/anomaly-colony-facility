using UnityEngine;

/// Otomasi kru. Pemain menentukan POS unit (UnitData.home) lewat penugasan manual;
/// Crew hanya memindahkan unit untuk sementara (istirahat) lalu mengembalikannya ke pos.
///
///   - Producer / Breeder / Leader = peran manual: hanya bekerja di pos yang ditentukan pemain.
///   - Fighter (non-Breeder/Producer) = otomatis: mengisi slot Production yang kosong (Material),
///     kecuali pemain melepasnya sendiri (holdFree).
///   - Unit lelah (mood <= CrewTiredMood) pergi ke Detention; Fighter juga boleh ke Breeding
///     yang berisi satu Breeder (sesi santai: hanya menaikkan mood, tidak menghasilkan unit).
///   - Setelah pulih (mood >= CrewRecoverMood) unit kembali ke pos. Penghuni pos boleh
///     mengusir "tamu" (unit yang sedang istirahat di pos itu) bila slot penuh.
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
            if (u.away) continue;

            bool visitor = u.cell != null && u.cell != u.home;
            bool inDetention = u.cell != null && u.cell.Type == CellType.Detention;
            bool relaxing = visitor && u.cell is Cell_Breeding;

            if (u.mood <= GameConfig.CrewTiredMood)
            {
                if (!inDetention && !relaxing) TryRest(u, grid, gm);
            }
            else if (visitor)
            {
                if (u.mood >= GameConfig.CrewRecoverMood && gm.Tick >= u.offDutyUntil) GoHome(u);
            }
            else if (u.cell == null && gm.Tick >= u.offDutyUntil && u.mood >= GameConfig.CrewWorkMood)
            {
                if (u.home != null) GoHome(u);
                else if (!IsManualRole(u) && !u.holdFree) AutoWork(u, grid);
            }
        }
    }

    // ---------- istirahat ----------

    static void TryRest(UnitData u, GridManager grid, GameManager gm)
    {
        string why;
        CellBase best = null;
        float bestDist = float.MaxValue;

        // 1) Detention terdekat yang masih muat
        foreach (var c in grid.cells)
        {
            if (c.Type != CellType.Detention || c == u.cell || !c.TryAssignCheck(u, out why)) continue;
            float d = Dist(u, c);
            if (d < bestDist) { bestDist = d; best = c; }
        }

        // 2) Fighter: Breeding dengan satu Breeder penghuni (sesi santai)
        if (best == null && !IsManualRole(u))
        {
            foreach (var c in grid.cells)
            {
                var br = c as Cell_Breeding;
                if (br == null || !br.CanHostRecreation || c == u.cell || !c.TryAssignCheck(u, out why)) continue;
                float d = Dist(u, c);
                if (d < bestDist) { bestDist = d; best = c; }
            }
        }

        if (best == null) return;   // tidak ada tempat: unit tetap lelah (tekanan untuk membangun Detention)
        if (best.TryAssign(u, false, out why)) gm.Log(u.Label + " is exhausted and went to rest.");
    }

    static float Dist(UnitData u, CellBase c)
    {
        Vector2Int from = u.cell != null ? u.cell.origin : (u.home != null ? u.home.origin : Vector2Int.zero);
        return Vector2Int.Distance(from, c.origin);
    }

    // ---------- kembali bekerja ----------

    static void GoHome(UnitData u)
    {
        string why;
        if (u.home == null) return;
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
