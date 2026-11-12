using System.Collections.Generic;
using UnityEngine;

/// Singleton: tick loop, resource, daftar unit, unlock, breeding spawn, log.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Diisi otomatis oleh Tools > Anomaly Facility > Setup Project")]
    public UnitController[] unitPrefabs; // index = (int)UnitType
    public UnitDatabase database;  

    public int Energy { get; private set; }
    public int Materials { get; private set; }
    public int Tick { get; private set; }

    public readonly List<UnitData> units = new List<UnitData>();
    public readonly List<string> LogLines = new List<string>();
    public Transform UnitRoot { get; private set; }

    public event System.Action OnChanged;
    public event System.Action<string> OnLog;
    public int Salvage { get; private set; }
    public void AddSalvage(int n) { Salvage = Mathf.Min(MaxResource, Salvage + n); }
    public bool SpendSalvage(int n) { if (Salvage < n) return false; Salvage -= n; NotifyChanged(); return true; }
    public void SetSalvage(int n) { Salvage = n; }

    readonly HashSet<CellType> unlocked = new HashSet<CellType>();
    int storageCount, controlCount, bonusUnits;
    float accumulator;
    bool starving;

    

    public int MaxResource { get { return GameConfig.BaseMaxResource + storageCount * GameConfig.StorageBonus; } }
    public int MaxUnits { get { return GameConfig.BaseMaxUnits + bonusUnits; } }

    void Awake()
    {
        Instance = this;
        UnitData.ResetIds();
        UnitRoot = new GameObject("Units").transform;
        Salvage = GameConfig.StartSalvage;
        Energy = GameConfig.StartEnergy;
        Materials = GameConfig.StartMaterials;
        foreach (var t in GameConfig.StartUnlocked) unlocked.Add(t);

        if (!SaveManager.LoadPending) SpawnStartingUnits();   // saat load, unit datang dari save
    }

    public void SpawnStartingUnits()
    {
        AddUnit(UnitData.Create(UnitType.Producer));
        AddUnit(UnitData.Create(UnitType.Fighter));
        AddUnit(UnitData.Create(UnitType.Breeder));
        AddUnit(UnitData.Create(UnitType.Leader));
        Log("Facility ready. Build cells, then assign units.");
    }

    void Update()
    {
        accumulator += Time.deltaTime;
        int guard = 0;
        while (accumulator >= GameConfig.TickSeconds && guard++ < 10)
        {
            accumulator -= GameConfig.TickSeconds;
            DoTick();
        }
    }

    public bool Starving { get { return starving; } }   // lewati jika sudah ada dari Step 2
    public int StorageCount { get { return storageCount; } }
    public int ControlCount { get { return controlCount; } }
    public int BonusUnits { get { return bonusUnits; } }
    public IEnumerable<CellType> UnlockedTypes { get { return unlocked; } }

    public void RestoreState(int tick, int energy, int materials, int storage, int control, int bonus,
                            IEnumerable<CellType> unlockedTypes, bool wasStarving)
    {
        Tick = tick; Energy = energy; Materials = materials;
        storageCount = storage; controlCount = control; bonusUnits = bonus;
        unlocked.Clear();
        foreach (var t in unlockedTypes) unlocked.Add(t);
        starving = wasStarving;
    }

    void DoTick()
    {
        Tick++;
        foreach (var c in GridManager.Instance.cells) c.OnTick(this);
        Crew.Tick(this);
        bool anyStarving = false;
        foreach (var u in units)
        {
            // Ration
            bool fed = Materials >= GameConfig.RationPerTick;
            if (fed) Materials -= GameConfig.RationPerTick; else anyStarving = true;

            // Mood (delta ditentukan cell tempat unit berada)
            int before = u.mood;
            int delta = u.cell != null ? u.cell.MoodPerTick : -GameConfig.MoodDecayPerTick;
            int aura = 0;
            foreach (var c in GridManager.Instance.cells) aura += c.AuraMoodFor(u);
            delta += Mathf.Min(aura, GameConfig.AuraMoodCap);
            if (!fed) delta -= GameConfig.StarvationMoodPenalty;
            u.mood = Mathf.Clamp(u.mood + delta, 0, 100);

            if (before >= GameConfig.MoodLowThreshold && u.mood < GameConfig.MoodLowThreshold)
                Log("Mood low: " + u.Label);
        }
        if (anyStarving && !starving) Log("Material depleted - units starving!");
        starving = anyStarving;

        if (RaidManager.Instance != null) RaidManager.Instance.OnTick();
        NotifyChanged();
    }

    // ---------- Resource ----------

    public void AddEnergy(int n) { Energy = Mathf.Min(MaxResource, Energy + n); }
    public void AddMaterials(int n) { Materials = Mathf.Min(MaxResource, Materials + n); }

    public bool SpendEnergy(int n)
    {
        if (Energy < n) return false;
        Energy -= n;
        NotifyChanged();
        return true;
    }

    // ---------- Unit ----------

    public UnitData AddUnit(UnitData u)
    {
        units.Add(u);
        int i = (int)u.type;
        if (unitPrefabs != null && i < unitPrefabs.Length && unitPrefabs[i] != null)
            Instantiate(unitPrefabs[i]).Bind(u, UnitRoot);
        else
            Debug.LogError("Prefab unit " + u.type + " belum di-assign. Jalankan Tools > Anomaly Facility > Setup Project.");
        return u;
    }

    public bool TrySpawnBred(UnitData a, UnitData b)
    {
        if (units.Count >= MaxUnits) return false;

        bool withLeader = a.type == UnitType.Leader || b.type == UnitType.Leader;
        int spread = withLeader ? GameConfig.BreedSpreadLeader : GameConfig.BreedSpreadNormal;
        int bonus  = withLeader ? GameConfig.BreedLeaderPowerBonus : 0;
        int power = Mathf.RoundToInt((a.power + b.power) / 2f) + bonus + Random.Range(-spread, spread + 1);
        float avgMood = (a.mood + b.mood) / 2f;
        if (avgMood >= 35f) power += 1; else if (avgMood <= 12f) power -= 1;
        int fert  = Mathf.RoundToInt((a.fertility + b.fertility) / 2f) + Random.Range(-1, 2);

        // induk Breeder menentukan jenis anak
        UnitData parent = (a.Is(UnitType.Breeder) && b.Is(UnitType.Breeder)) ? (Random.value < 0.5f ? a : b)
                        : a.Is(UnitType.Breeder) ? a : b;
        UnitArchetype parentArch = parent.archetype != null ? parent.archetype
                                : (database != null ? database.Get(parent.type) : null);
        UnitArchetype childArch = parentArch != null ? parentArch.RollOffspring(withLeader) : null;

        UnitType childType = childArch != null ? childArch.type : RandomType();   // fallback lama
        if (childArch != null)
        {
            power = Mathf.Clamp(power, childArch.powerRange.x, childArch.powerRange.y);
            fert  = Mathf.Clamp(fert,  childArch.fertilityRange.x, childArch.fertilityRange.y);
        }

        var u = UnitData.Create(childType, power, fert);
        if (childArch != null) u.ApplyArchetype(childArch);   // sebelum AddUnit supaya model yang dipasang sudah benar
        AddUnit(u);

        Log("Unit bred: " + u.Label + " (P" + u.power + ")");
        NotifyChanged();
        return true;
    }

    public UnitData RollNewUnit(int power = -1, int fertility = -1)
    {
        var sp = database != null ? database.RollSpecial(units) : null;
        if (sp != null)
        {
            var s = UnitData.CreateSpecial(sp);
            Log("Special unit: " + s.Label + "!");
            return s;
        }
        return UnitData.Create(RandomType(), power, fertility);
    }

    public static UnitType RandomType() { return (UnitType)Random.Range(0, 3); }

    // ---------- Cell hooks ----------

    public bool IsUnlocked(CellType t) { return unlocked.Contains(t); }

    public void OnCellPlaced(CellBase c)
    {
        Log("Cell built: " + GameConfig.Label(c.Type));
        c.OnPlaced(this);
        NotifyChanged();
    }

    public void RegisterStorage()
    {
        storageCount++;
        Log("Max resource is now " + MaxResource);
    }

    public void RegisterControl()
    {
        int idx = controlCount++;
        if (idx < GameConfig.UnlockOrder.Length)
        {
            var t = GameConfig.UnlockOrder[idx];
            unlocked.Add(t);
            Log("Unlocked: " + GameConfig.Label(t));
        }
        else
        {
            bonusUnits += GameConfig.ControlUnitBonus;
            Log("Max units is now " + MaxUnits);
        }
    }

    // ---------- Log / event ----------

    public void Log(string msg)
    {
        string line = "[" + Tick + "] " + msg;
        LogLines.Add(line);
        if (OnLog != null) OnLog(line);
    }

    public void NotifyChanged()
    {
        if (OnChanged != null) OnChanged();
    }
}
