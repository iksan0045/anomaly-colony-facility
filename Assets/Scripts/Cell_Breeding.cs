using System.Text;
using UnityEngine;

/// BREEDING (2x2, ungu): 2 Breeder -> countdown 10 tick -> spawn 1 unit baru.
public class Cell_Breeding : CellBase
{
    public int breedProgress;

    public override CellType Type { get { return CellType.Breeding; } }
    [Tooltip("Geser Leader pada sumbu Z (forward) Slot_0.")]
    public float leaderOffsetZ = 0.133f;

    public override bool CanAccept(UnitData u, out string reason)
    {
        if (!base.CanAccept(u, out reason)) return false;

        bool isLeader = u.type == UnitType.Leader;
        bool hasLeader = units.Exists(x => x.type == UnitType.Leader);
        bool hasOther  = units.Exists(x => x.type != UnitType.Leader);

        if (isLeader && hasLeader)  { reason = "Leader already inside"; return false; }
        if (!isLeader && hasOther)  { reason = "Partner must be the Leader"; return false; } // blok sesama non-Leader
        return true;
    }

    int Duration
    {
        get
        {
            var partner = units.Find(u => u.type != UnitType.Leader);
            return partner != null ? partner.BreedTicks : GameConfig.BreedTicks;
        }
    }

    public override void OnTick(GameManager gm)
    {
        if (!PairValid) { breedProgress = 0; return; }

        int duration = Duration;
        if (breedProgress < duration) breedProgress++;
        if (breedProgress < duration) return;

        if (gm.units.Count >= gm.MaxUnits) return;   // facility penuh: tunggu, jangan keluar dulu

        var partner = units.Find(u => u.type != UnitType.Leader);
        float chance = partner.type == UnitType.Breeder ? GameConfig.BreedChanceBreeder
                                                        : GameConfig.BreedChanceOther;
        if (Random.value < chance) gm.TrySpawnBred(units[0], units[1]);
        else gm.Log("Breeding fail no child");

        gm.Log("Breeding succed child born, units released.");
        EjectAll();
    }


    void EjectAll()
    {
        foreach (var u in units.ToArray()) RemoveUnit(u);   // salinan: list diubah saat iterasi
    }

    protected override void DescribeDetails(StringBuilder sb)
    {
        if (!PairValid) sb.AppendLine("Needs 1 Leader + 1 other unit (Breeder: 90%, others: 40%)");
        else if (breedProgress >= Duration) sb.AppendLine("Ready, waiting for a free unit slot (facility full)");
        else sb.AppendLine("Breeding: " + breedProgress + "/" + Duration + " tick");
    }


    // ganti OnUnitsChanged yang sudah ada:
    protected override void OnUnitsChanged()
    {
        breedProgress = 0;
        //SetUnitsBreeding(PairValid);
    }

    bool PairValid
    {
        get { return units.Count == 2 && units.Exists(u => u.type == UnitType.Leader); }
    }

    protected override bool ShouldAnimate(UnitData u)
    {
        return PairValid;
    }

    protected override AnimationClip ClipFor(UnitData u)
    {
        if (!PairValid) return null;                          // tanpa pasangan lengkap: tidak ada animasi

        if (u.type != UnitType.Leader) return u.BreedClip;    // pasangan: clip miliknya sendiri
        var partner = units.Find(x => x != u);
        return partner != null ? partner.LeaderBreedClip : null;
    }

    bool IsFlipped(UnitData leader)
    {
        if (leader.type != UnitType.Leader) return false;
        var partner = units.Find(x => x != leader);
        return partner != null && partner.LeaderFacesMe;
    }

    protected override float UnitYaw(UnitData u)
    {
        return IsFlipped(u) ? 180f : 0f;
    }

    protected override Vector3 UnitOffset(UnitData u)
    {
        if (u.type != UnitType.Leader) return Vector3.zero;
        float z = IsFlipped(u) ? -leaderOffsetZ : leaderOffsetZ;   // berhadapan: jarak dibalik
        return new Vector3(0f, 0f, z);
    }

    

}
