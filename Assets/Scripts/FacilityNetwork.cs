using System.Collections.Generic;
using UnityEngine;

/// Jaringan fasilitas: cell yang bersentuhan (sisi ke sisi) membentuk graf. Jarak dihitung dalam
/// jumlah cell dari Control (hub). Makin jauh dari Control, makin rendah efisiensi cell;
/// cell yang tidak tersambung sama sekali tidak bekerja.
///
/// Panggil Recompute(grid.cells) setiap ada cell baru (GridManager.TryPlace / PlaceForLoad).
public static class FacilityNetwork
{
    public static bool HasHub { get; private set; }

    public static void Recompute(List<CellBase> cells)
    {
        foreach (var c in cells) c.Hops = -1;

        var queue = new Queue<CellBase>();
        HasHub = false;
        foreach (var c in cells)
        {
            if (c.Type != CellType.Control) continue;
            c.Hops = 0;
            queue.Enqueue(c);
            HasHub = true;
        }

        while (queue.Count > 0)
        {
            var a = queue.Dequeue();
            foreach (var b in cells)
            {
                if (b.Hops >= 0 || !Touching(a, b)) continue;
                b.Hops = a.Hops + 1;
                queue.Enqueue(b);
            }
        }
    }

    /// 0 = tidak bekerja, 1 = penuh.
    public static float Efficiency(CellBase c)
    {
        if (!HasHub) return GameConfig.NoHubEfficiency;   // belum ada Control: kerja tapi tidak optimal
        if (c.Hops < 0) return 0f;                        // tidak tersambung ke Control
        return Mathf.Max(GameConfig.MinLinkEfficiency, 1f - c.Hops * GameConfig.HopPenalty);
    }

    static bool Touching(CellBase a, CellBase b)
    {
        int ax0 = a.origin.x, ax1 = a.origin.x + a.size.x, ay0 = a.origin.y, ay1 = a.origin.y + a.size.y;
        int bx0 = b.origin.x, bx1 = b.origin.x + b.size.x, by0 = b.origin.y, by1 = b.origin.y + b.size.y;

        bool overlapY = ay0 < by1 && by0 < ay1;
        bool overlapX = ax0 < bx1 && bx0 < ax1;
        return ((ax1 == bx0 || bx1 == ax0) && overlapY)
            || ((ay1 == by0 || by1 == ay0) && overlapX);
    }
}
