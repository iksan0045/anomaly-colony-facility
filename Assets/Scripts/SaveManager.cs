using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Save/Load satu slot + autosave. Dibuat otomatis di scene game (tidak perlu dipasang manual).
///
/// Alur Load: set LoadPending lalu reload scene. GameManager.Awake melewati spawn unit awal,
/// lalu SaveManager.Start (urutan eksekusi 100, setelah semua Start lain) menerapkan data.
/// Posisi roaming unit tidak disimpan (unit bebas muncul di titik acak), progres tick parsial juga tidak.
[DefaultExecutionOrder(100)]
public class SaveManager : MonoBehaviour
{
    public const int Version = 1;

    /// true = saat Play/start game otomatis lanjut dari save jika ada.
    public static bool AutoContinue = true;
    public static bool LoadPending;
    public static SaveManager Instance { get; private set; }

    public float autoSaveSeconds = 30f;   // 0 = autosave mati (tetap save saat keluar)

    public static string FilePath { get { return Path.Combine(Application.persistentDataPath, "facility_save.json"); } }
    public static bool HasSave { get { return File.Exists(FilePath); } }

    GameManager gm;
    GridManager grid;
    RaidManager raid;
    bool ready;
    float timer;

    // ================= Boot =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        LoadPending = AutoContinue && HasSave;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Berlaku untuk load pertama maupun reload scene
        if (GameManager.Instance != null && Instance == null)
            new GameObject("SaveManager").AddComponent<SaveManager>();
    }

    void Awake() { Instance = this; }

    void Start()
    {
        gm = GameManager.Instance;
        grid = GridManager.Instance;
        raid = RaidManager.Instance;

        if (LoadPending)
        {
            LoadPending = false;
            string when;
            if (TryLoad(out when))
                gm.Log("Game dimuat (" + when + ")");
            else
            {
                gm.SpawnStartingUnits();
                gm.Log("Save gagal dimuat, mulai baru.");
            }
        }
        ready = true;
    }

    void Update()
    {
        if (!ready || autoSaveSeconds <= 0f) return;
        timer += Time.unscaledDeltaTime;
        if (timer >= autoSaveSeconds) { timer = 0f; Save(false); }
    }

    void OnApplicationPause(bool paused) { if (paused) Save(false); }
    void OnApplicationQuit() { Save(false); }

    // ================= API (dipanggil UI) =================

    public void SaveNow() { Save(true); }

    public void LoadGame()
    {
        if (!HasSave) { gm.Log("Belum ada save."); return; }
        Reload(true);
    }

    /// Mulai baru. Save lama disalin ke .bak dulu.
    public void NewGame()
    {
        if (!CanReload()) return;
        if (HasSave) File.Copy(FilePath, FilePath + ".bak", true);
        File.Delete(FilePath);
        Reload(false);
    }

    bool CanReload()
    {
        if (SceneManager.GetActiveScene().buildIndex >= 0) return true;
        Debug.LogError("Scene belum ada di Build Settings (Tools > Anomaly Facility > Setup Project menambahkannya).");
        return false;
    }

    void Reload(bool load)
    {
        if (!CanReload()) return;
        ready = false;               // cegah autosave di tengah reload
        LoadPending = load;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ================= Save =================

    public bool Save(bool announce)
    {
        if (!ready || gm == null || grid == null || raid == null) return false;
        try
        {
            var d = new SaveData
            {
                version = Version,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                tick = gm.Tick,
                energy = gm.Energy,
                materials = gm.Materials,
                starving = gm.Starving,
                storageCount = gm.StorageCount,
                controlCount = gm.ControlCount,
                bonusUnits = gm.BonusUnits,
                unlocked = gm.UnlockedTypes.Select(t => (int)t).ToArray(),
                nextUnitId = UnitData.NextId,
                raidCount = raid.RaidCount,
                raidCooldown = raid.CooldownLeft,
                log = gm.LogLines.ToArray(),
            };

            foreach (var u in gm.units)
            {
                d.units.Add(new UnitSave
                {
                    id = u.id,
                    type = (int)u.type,
                    power = u.power,
                    mood = u.mood,
                    fertility = u.fertility,
                    variant = u.variant,
                    moveSpeed = u.moveSpeed,
                    scale = u.scale,
                    displayName = u.displayName,
                    specialName = u.special != null ? u.special.name : "",
                });
            }

            foreach (var c in grid.cells)
            {
                var cs = new CellSave
                {
                    type = (int)c.Type,
                    x = c.origin.x, y = c.origin.y,
                    w = c.size.x, h = c.size.y,
                    unitIds = c.units.Select(u => u.id).ToArray(),
                };
                var br = c as Cell_Breeding;
                if (br != null) cs.breedProgress = br.breedProgress;
                d.cells.Add(cs);
            }

            WriteAtomic(JsonUtility.ToJson(d, true));
            if (announce) gm.Log("Game disimpan.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] Gagal menyimpan: " + e);
            if (announce) gm.Log("Gagal menyimpan!");
            return false;
        }
    }

    static void WriteAtomic(string json)
    {
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
        File.Copy(tmp, FilePath, true);
        File.Delete(tmp);
    }

    // ================= Load =================

    bool TryLoad(out string savedAt)
    {
        savedAt = "";
        SaveData d;
        try
        {
            d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Save] File save tidak terbaca: " + e.Message);
            return false;
        }
        if (d == null || d.version != Version)
        {
            Debug.LogWarning("[Save] Versi save tidak cocok atau kosong.");
            return false;
        }

        try
        {
            savedAt = d.savedAt;
            var db = gm.database;

            // 1. State global
            gm.RestoreState(d.tick, d.energy, d.materials, d.storageCount, d.controlCount, d.bonusUnits,
                            d.unlocked.Select(i => (CellType)i), d.starving);
            raid.Restore(d.raidCount, d.raidCooldown);

            // 2. Cell
            var cells = new List<CellBase>();
            foreach (var cs in d.cells)
                cells.Add(grid.PlaceForLoad((CellType)cs.type, new Vector2Int(cs.x, cs.y), new Vector2Int(cs.w, cs.h)));

            // 3. Unit
            var map = new Dictionary<int, UnitData>();
            int maxId = 0;
            foreach (var us in d.units)
            {
                var u = ScriptableObject.CreateInstance<UnitData>();
                u.id = us.id;
                u.type = (UnitType)us.type;
                u.power = us.power;
                u.mood = us.mood;
                u.fertility = us.fertility;
                u.variant = us.variant;
                u.moveSpeed = us.moveSpeed;
                u.scale = us.scale;
                u.displayName = us.displayName;
                u.special = FindSpecial(db, us.specialName);
                u.archetype = db != null ? db.Get(u.type) : null;
                u.name = u.Label;

                gm.AddUnit(u);
                map[u.id] = u;
                if (u.id > maxId) maxId = u.id;
            }
            UnitData.SetNextId(Mathf.Max(d.nextUnitId, maxId + 1));

            // 4. Assign ke cell (urutan = urutan slot), lalu progres breeding
            for (int i = 0; i < d.cells.Count; i++)
            {
                if (cells[i] == null || d.cells[i].unitIds == null) continue;
                foreach (int id in d.cells[i].unitIds)
                {
                    UnitData u;
                    if (map.TryGetValue(id, out u)) cells[i].AssignUnit(u);
                }
                var br = cells[i] as Cell_Breeding;
                if (br != null) br.breedProgress = d.cells[i].breedProgress; // setelah assign (assign mereset progres)
            }

            // 5. Log
            gm.LogLines.Clear();
            if (d.log != null) gm.LogLines.AddRange(d.log);

            gm.NotifyChanged();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] Gagal menerapkan save: " + e + "\nBackup sebelumnya: " + FilePath + ".bak");
            return false;
        }
    }

    static SpecialUnit FindSpecial(UnitDatabase db, string assetName)
    {
        if (db == null || db.specials == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var s in db.specials)
            if (s != null && s.name == assetName) return s;
        return null;
    }
}
