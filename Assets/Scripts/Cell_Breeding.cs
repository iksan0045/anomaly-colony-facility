using System.Text;
using UnityEngine;

/// BREEDING (2x2, ungu): 2 Breeder -> countdown 10 tick -> spawn 1 unit baru.
public class Cell_Breeding : CellBase
{
    public int breedProgress;

    public override CellType Type { get { return CellType.Breeding; } }

    public override bool CanAccept(UnitData u, out string reason)
    {
        if (!base.CanAccept(u, out reason)) return false;

        bool isLeader = u.type == UnitType.Leader;
        bool hasLeader = units.Exists(x => x.type == UnitType.Leader);
        bool hasOther  = units.Exists(x => x.type != UnitType.Leader);

        if (isLeader && hasLeader)  { reason = "Sudah ada Leader"; return false; }
        if (!isLeader && hasOther)  { reason = "Pasangan harus Leader"; return false; } // blok sesama non-Leader
        return true;
    }

    public override void OnTick(GameManager gm)
    {
        // wajib 1 Leader + 1 unit lain
        if (units.Count < 2 || !units.Exists(u => u.type == UnitType.Leader)) { breedProgress = 0; return; }

        if (breedProgress < GameConfig.BreedTicks) breedProgress++;
        if (breedProgress < GameConfig.BreedTicks) return;

        if (gm.units.Count >= gm.MaxUnits) return;   // facility penuh: tunggu, roll belum dipakai

        var partner = units.Find(u => u.type != UnitType.Leader);
        float chance = partner.type == UnitType.Breeder ? GameConfig.BreedChanceBreeder
                                                        : GameConfig.BreedChanceOther;
        if (Random.value < chance) gm.TrySpawnBred(units[0], units[1]);
        else gm.Log("Breeding gagal");
        breedProgress = 0;
    }

    bool PairValid { get { return units.Count == 2 && units.Exists(u => u.type == UnitType.Leader); } }

    // ganti OnUnitsChanged yang sudah ada:
    protected override void OnUnitsChanged()
    {
        breedProgress = 0;
        //SetUnitsBreeding(PairValid);
    }

    protected override AnimationClip ClipFor(UnitData u) { return u.BreedClip; }

    

    protected override void DescribeDetails(StringBuilder sb)
    {
        if (units.Count < 2) sb.AppendLine("Butuh 1 Leader + 1 unit lain (Breeder: 90%, lainnya: 40%)");
        else if (breedProgress >= GameConfig.BreedTicks) sb.AppendLine("Siap, menunggu slot unit (facility penuh)");
        else sb.AppendLine("Breeding: " + breedProgress + "/" + GameConfig.BreedTicks + " tick");
    }
}
