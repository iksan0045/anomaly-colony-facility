using System.Text;
using UnityEngine;

/// BREEDING (2x2, ungu): 2 Breeder -> countdown 10 tick -> spawn 1 unit baru.
public class Cell_Breeding : CellBase
{
    public int breedProgress;
    public GameObject vfxBreed;

    UnitData LeaderUnit  { get { return units.Find(u => u.type == UnitType.Leader); } }
    UnitData BreederUnit { get { return units.Find(u => u.Is(UnitType.Breeder)); } }

    public override CellType Type { get { return CellType.Breeding; } }
    [Tooltip("Geser Leader pada sumbu Z (forward) Slot_0.")]
    public float leaderOffsetZ = 0.133f;
    [Tooltip("Jarak kedua unit pada sumbu X jika breeding tanpa Leader.")]
    public float pairSpacing = 0.3f;
    float speedAcc;
    
    
    void SetVfx(bool on)
    {
        //if (vfxBreed != null && vfxBreed.activeSelf != on) vfxBreed.SetActive(on);
    }

    
    

    public override bool CanAccept(UnitData u, out string reason)
    {
        if (!base.CanAccept(u, out reason)) return false;

        if (u.type == UnitType.Leader && LeaderUnit != null)
        { reason = "Leader already inside"; return false; }

        // unit kedua harus melengkapi: minimal 1 Breeder di dalam cell
        if (units.Count > 0 && !u.Is(UnitType.Breeder) && BreederUnit == null)
        { reason = "Needs at least 1 Breeder"; return false; }

        return true;
    }

    bool IsRecreation
    {
        get { return units.Count == 2 && BreederUnit != null
                    && units.Exists(u => u.Is(UnitType.Fighter) && !u.Is(UnitType.Breeder)); }
    }
    bool PairValid   // pasangan yang menghasilkan anak
    {
        get { return units.Count == 2 && BreederUnit != null && !IsRecreation; }
    }
    public bool CanHostRecreation
    {
        get { return units.Count == 1 && units[0].home == this && units[0].Is(UnitType.Breeder); }
    }
    public override int MoodPerTick
    {
        get { return IsRecreation ? GameConfig.RecreationMoodPerTick : base.MoodPerTick; }
    }

    int Duration
    {
        get { var b = BreederUnit; return b != null ? b.BreedTicks : GameConfig.BreedTicks; }
    }
        

    public override void OnTick(GameManager gm)
    {
        bool rec = IsRecreation;
        if (!PairValid && !rec) { breedProgress = 0; return; }

        float eff = Efficiency;
        float avgMood = GameManager.Instance.Mood.Value;
        if (eff <= 0f || (!rec && avgMood < GameConfig.MinBreedMood)) return;
        if (eff <= 0f || avgMood < GameConfig.MinBreedMood) return;   // tidak tersambung / terlalu stres

        int duration = Duration;
        if (breedProgress < duration)
        {
            speedAcc += eff;                                          // makin jauh dari Control, makin lambat
            if (speedAcc >= 1f) { speedAcc -= 1f; breedProgress++; }
        }
        if (breedProgress < duration) return;

        if (gm.units.Count >= gm.MaxUnits) return;                    // facility penuh: tunggu
        if (!gm.SpendEnergy(GameConfig.BreedEnergyCost)) return;      // butuh Energy tiap siklus
        
        float chance = Mathf.Clamp01(GameConfig.BreedChanceBreeder * GameConfig.MoodFactor(avgMood));
        float roll = Random.value;
        bool success = roll < chance && gm.TrySpawnBred(units[0], units[1]);
        if (rec)
        {
            foreach (var u in units) u.mood = Mathf.Clamp(u.mood + GameConfig.BreedMoodBonus, 0, 100);
            gm.Log("Rest session finished: units relaxed, no offspring.");
            Release(false);
            return;
        }
        if (success)
        {
            gm.Log("Breeding succeeded, child born.");
            foreach (var u in units) u.mood = Mathf.Clamp(u.mood + GameConfig.BreedMoodBonus, 0, 100);   // bonus mood hanya jika berhasil
        }
        else gm.Log("Breeding failed.");
        Release(true);
    }


    void Release(bool all)
    {
        int rest = GameManager.Instance.Tick + GameConfig.BreedRestTicks;
        foreach (var u in units.ToArray())
        {
            if (!all && u.home == this) continue;      // sesi santai: hanya tamu yang pergi
            if (all) u.offDutyUntil = rest;
            RemoveUnit(u, false);                      // false: pos tidak terhapus
        }
        breedProgress = 0;
    }

    protected override void DescribeDetails(StringBuilder sb)
    {
        if (!PairValid) sb.AppendLine("Needs at least 1 Breeder + 1 partner. Leader = better offspring.");
        else if (breedProgress >= Duration) sb.AppendLine("Ready, waiting for a free unit slot (facility full)");
        else sb.AppendLine("Breeding: " + breedProgress + "/" + Duration + " tick"
                        + (LeaderUnit != null ? "  (Leader bonus)" : ""));
    }


    // ganti OnUnitsChanged yang sudah ada:
    protected override void OnUnitsChanged()
    {
        breedProgress = 0;
        SetVfx(PairValid);   // nyala saat pasangan lengkap, mati saat ada yang keluar
    }


    protected override bool ShouldAnimate(UnitData u) { return PairValid && LeaderUnit != null; }

    protected override AnimationClip ClipFor(UnitData u)
    {
        if (!ShouldAnimate(u)) return null;
        if (u.type != UnitType.Leader) return u.BreedClip;
        var partner = units.Find(x => x != u);
        return partner != null ? partner.LeaderBreedClip : null;
    }

    protected override Vector3 UnitOffset(UnitData u)
    {
        if (u.type == UnitType.Leader)
            return new Vector3(0f, 0f, IsFlipped(u) ? -leaderOffsetZ : leaderOffsetZ);

        if (LeaderUnit != null) return Vector3.zero;                 // pasangan Leader: pakai slot bersama

        // tanpa Leader: pisahkan dua unit supaya tidak menumpuk di Slot_0
        return new Vector3(units.IndexOf(u) == 0 ? -pairSpacing : pairSpacing, 0f, 0f);
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


}
