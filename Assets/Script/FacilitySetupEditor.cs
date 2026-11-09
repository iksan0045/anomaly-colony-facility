#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// One-click setup MVP Anomaly Containment Facility.
/// Menu: Tools > Anomaly Facility > Setup Project
///
/// Hasil:
///   Assets/Materials/*.mat
///   Assets/Prefabs/Cells/Cell_{Detention,Production,Breeding,Storage,Control}.prefab
///   Assets/Prefabs/Units/Unit_{Producer,Fighter,Breeder}.prefab
///   Assets/Scenes/Main.unity   (Camera + Light + Managers + UI Canvas + EventSystem)
///   Semua script dirapikan ke Assets/Scripts
///
/// Aman diletakkan di mana saja (dibungkus #if UNITY_EDITOR). Disarankan: Assets/Scripts/Editor/.
public static class FacilitySetupEditor
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string OldScenePath = "Assets/Scenes/FacilityDemo.unity";
    const string CellsFolder = "Assets/Prefabs/Cells";
    const string UnitsFolder = "Assets/Prefabs/Units";
    const string MaterialsFolder = "Assets/Materials";
    const string ScriptsFolder = "Assets/Scripts";

    static readonly string[] RequiredScripts =
    {
        "GameConfig", "Enums", "UnitData", "UnitController",
        "CellBase", "Cell_Detention", "Cell_Production", "Cell_Breeding", "Cell_Storage", "Cell_Control",
        "GameManager", "GridManager", "RaidManager", "UIManager",
    };

    // ================= Menu =================

    [MenuItem("Tools/Anomaly Facility/Setup Project")]
    static void SetupMenu() { Setup(false); }

    [MenuItem("Tools/Anomaly Facility/Setup Project and Play")]
    static void SetupAndPlayMenu() { Setup(true); }

    [MenuItem("Tools/Anomaly Facility/Regenerate Prefabs Only")]
    static void RegeneratePrefabsMenu()
    {
        if (!Preflight()) return;
        EnsureAllFolders();
        BuildPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Anomaly Facility", "Prefab dan material diperbarui. Referensi di Main.unity tetap terhubung.", "OK");
    }

    [MenuItem("Tools/Anomaly Facility/Open Main Scene")]
    static void OpenMainMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            EditorUtility.DisplayDialog("Anomaly Facility", "Main.unity belum ada. Jalankan 'Setup Project' dulu.", "OK");
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    // ================= Setup =================

    static void Setup(bool play)
    {
        if (!Preflight()) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Anomaly Facility", "Main.unity sudah ada. Timpa?", "Timpa", "Batal"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureAllFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Prefab + material + database
        CellBase[] cellPrefabs;
        UnitController[] unitPrefabs;
        BuildPrefabs(out cellPrefabs, out unitPrefabs);
        UnitDatabase db = BuildDatabase();

        // 2. Isi scene
        BuildSceneObjects(cellPrefabs, unitPrefabs, db);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // 3. Build settings
        UpdateBuildSettings();

        // 4. Rapikan script (terakhir: bisa memicu recompile)
        int moved = OrganizeScripts();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("[Anomaly Facility] Setup selesai. Scene: " + ScenePath + ", script dipindah: " + moved);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        EditorUtility.DisplayDialog("Anomaly Facility",
            "Project memakai Input System package saja. Game ini memakai Input lama.\n\n" +
            "Ubah: Project Settings > Player > Other Settings > Active Input Handling = 'Both' atau 'Input Manager (Old)', lalu restart editor.",
            "OK");
#endif

        if (play) EditorApplication.isPlaying = true;
        else EditorUtility.DisplayDialog("Anomaly Facility", "Selesai. Main.unity siap, tekan Play.", "OK");
    }

    static bool Preflight()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Anomaly Facility", "Hentikan Play mode dulu.", "OK");
            return false;
        }
        var missing = RequiredScripts.Where(n => FindScriptPath(n) == null).ToArray();
        if (missing.Length > 0)
        {
            EditorUtility.DisplayDialog("Anomaly Facility",
                "Script belum lengkap. Tidak ditemukan: " + string.Join(", ", missing) +
                "\n\nCopy semua .cs ke folder Assets dulu.", "OK");
            return false;
        }
        return true;
    }

    // ================= Prefab =================

    static void BuildPrefabs()
    {
        CellBase[] c; UnitController[] u;
        BuildPrefabs(out c, out u);
    }

    static void BuildPrefabs(out CellBase[] cellPrefabs, out UnitController[] unitPrefabs)
    {
        var cellTypes = (CellType[])System.Enum.GetValues(typeof(CellType));
        var unitTypes = (UnitType[])System.Enum.GetValues(typeof(UnitType));

        cellPrefabs = new CellBase[cellTypes.Length];
        unitPrefabs = new UnitController[unitTypes.Length];

        foreach (var t in cellTypes) cellPrefabs[(int)t] = BuildCellPrefab(t);
        foreach (var t in unitTypes) unitPrefabs[(int)t] = BuildUnitPrefab(t);
    }

    static CellBase BuildCellPrefab(CellType t)
    {
        var root = new GameObject("Cell_" + t);
        switch (t)
        {
            case CellType.Detention: root.AddComponent<Cell_Detention>(); break;
            case CellType.Production: root.AddComponent<Cell_Production>(); break;
            case CellType.Breeding: root.AddComponent<Cell_Breeding>(); break;
            case CellType.Storage: root.AddComponent<Cell_Storage>(); break;
            case CellType.Control: root.AddComponent<Cell_Control>(); break;
        }

        var size = GameConfig.DefaultSize(t);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(size.x * 0.94f, CellBase.Height, size.y * 0.94f);
        body.transform.localPosition = new Vector3(0f, CellBase.Height / 2f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = GetMaterial("Cell_" + t, GameConfig.ColorOf(t));

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CellsFolder + "/Cell_" + t + ".prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<CellBase>();
    }

    static UnitController BuildUnitPrefab(UnitType t)
    {
        var root = new GameObject("Unit_" + t);
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 0.3f;
        body.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = GetMaterial("Unit_" + t, GameConfig.ColorOf(t));
        root.AddComponent<UnitController>();
        root.AddComponent<UnitVisual>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, UnitsFolder + "/Unit_" + t + ".prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<UnitController>();
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(DefaultShader());
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Shader DefaultShader()
    {
        Shader s = null;
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null) s = rp.defaultShader;
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Sprites/Default");
        return s;
    }

    // ================= Scene =================

    static void BuildSceneObjects(CellBase[] cellPrefabs, UnitController[] unitPrefabs, UnitDatabase db)
    {
        // Kamera isometric: pitch 30, yaw 45, orthographic
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<CameraController>();   // setelah kamera dibuat
        
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
        var center = new Vector3(GameConfig.GridSize / 2f, 0f, GameConfig.GridSize / 2f);
        camGo.transform.position = center - camGo.transform.forward * 30f;

        // Cahaya
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Managers
        var managers = new GameObject("Managers");
        var gm = managers.AddComponent<GameManager>();
        gm.unitPrefabs = unitPrefabs;
        gm.database = db;                         // setelah gm.unitPrefabs = unitPrefabs
        var grid = managers.AddComponent<GridManager>();
        grid.cellPrefabs = cellPrefabs;
        managers.AddComponent<RaidManager>();

        // UI (hierarki nyata di scene, bisa diedit)
        var uiGo = new GameObject("UI Canvas");
        var ui = uiGo.AddComponent<UIManager>();
        ui.BuildHierarchy(cam);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static UnitDatabase BuildDatabase()
    {
        EnsureFolder("Assets/Data");
        const string dbPath = "Assets/Data/UnitDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<UnitDatabase>(dbPath);
        if (db == null) { db = ScriptableObject.CreateInstance<UnitDatabase>(); AssetDatabase.CreateAsset(db, dbPath); }

        var list = new List<UnitArchetype>();
        foreach (UnitType t in System.Enum.GetValues(typeof(UnitType)))
        {
            string path = "Assets/Data/Archetype_" + t + ".asset";
            var a = AssetDatabase.LoadAssetAtPath<UnitArchetype>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<UnitArchetype>();
                a.type = t;
                switch (t)
                {
                    case UnitType.Producer: a.powerRange = new Vector2Int(1, 5);  a.fertilityRange = new Vector2Int(1, 6);  a.namePool = new[] { "Aria", "Nova", "Lumi" }; break;
                    case UnitType.Fighter:  a.powerRange = new Vector2Int(4, 9);  a.fertilityRange = new Vector2Int(1, 5);  a.namePool = new[] { "Kira", "Rex", "Vale" }; break;
                    case UnitType.Breeder:  a.powerRange = new Vector2Int(2, 6);  a.fertilityRange = new Vector2Int(5, 10); a.namePool = new[] { "Mira", "Sana", "Elle" }; break;
                    case UnitType.Leader:   a.powerRange = new Vector2Int(7, 10); a.fertilityRange = new Vector2Int(6, 10); a.namePool = new[] { "Queen" }; break;
                }
                AssetDatabase.CreateAsset(a, path);
            }
            list.Add(a);
        }
        db.archetypes = list.ToArray();
        EditorUtility.SetDirty(db);
        return db;
    }

    static void UpdateBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != OldScenePath).ToList();
        if (!scenes.Any(s => s.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ================= Files =================

    static void EnsureAllFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(CellsFolder);
        EnsureFolder(UnitsFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(ScriptsFolder);
    }

    static string FindScriptPath(string scriptName)
    {
        foreach (var guid in AssetDatabase.FindAssets(scriptName + " t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/") && Path.GetFileNameWithoutExtension(path) == scriptName)
                return path;
        }
        return null;
    }

    static int OrganizeScripts()
    {
        int moved = 0;
        foreach (var name in RequiredScripts)
        {
            string path = FindScriptPath(name);
            if (path == null) continue;
            if (Path.GetDirectoryName(path).Replace('\\', '/') == ScriptsFolder) continue;

            string err = AssetDatabase.MoveAsset(path, ScriptsFolder + "/" + Path.GetFileName(path));
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogWarning("[Anomaly Facility] Gagal pindah " + path + ": " + err);
        }
        return moved;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
