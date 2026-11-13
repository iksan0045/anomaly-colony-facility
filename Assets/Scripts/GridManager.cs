using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// Grid 8x8: state, placement, raycast. Mouse only:
/// klik kiri = taruh/pilih, klik kanan = rotasi saat place (Esc = batal).
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Diisi otomatis oleh Tools > Anomaly Facility > Setup Project")]
    public CellBase[] cellPrefabs; // index = (int)CellType

    public GameObject[] tilePrefabs;  
    public GameObject lantaiGrid;
    public readonly List<CellBase> cells = new List<CellBase>();
    public CellType? Placing { get; private set; }
    public CellBase Selected { get; private set; }
    public event Action OnModeChanged;

    readonly CellBase[,] occupancy = new CellBase[GameConfig.GridSize, GameConfig.GridSize];
    static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

    Vector2Int placeSize;
    Transform cellRoot;
    GameObject ghost;
    Renderer ghostRenderer;
    Camera cam;

    void Awake()
    {
        Instance = this;
        cellRoot = new GameObject("Cells").transform;
        BuildGround();

        ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "PlacementGhost";
        Destroy(ghost.GetComponent<Collider>());
        ghostRenderer = ghost.GetComponent<Renderer>();
        ghost.SetActive(false);
    }

    void BuildGround()
    {
        var root = new GameObject("Ground").transform;
        var rng = new System.Random(777);
        bool useTiles = tilePrefabs != null && tilePrefabs.Length > 0;

        if (!useTiles)
            Debug.LogWarning("tilePrefabs kosong di GridManager, memakai lantai grey-box.");

        for (int x = 0; x < GameConfig.GridSize; x++)
        for (int z = 0; z < GameConfig.GridSize; z++)
        {
            GameObject prefab = useTiles ? tilePrefabs[rng.Next(tilePrefabs.Length)] : null;

            if (prefab != null)
            {
                var tile = Instantiate(prefab, root);
                tile.name = "Tile_" + x + "_" + z;
                tile.transform.SetPositionAndRotation(
                    new Vector3(x + 0.5f, 0f, z + 0.5f),
                    Quaternion.Euler(0f, 90f * rng.Next(4), 0f));
                foreach (var c in tile.GetComponentsInChildren<Collider>()) Destroy(c);
                continue;
            }

            // Fallback grey-box (hanya jika tilePrefabs kosong)
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = "Tile_" + x + "_" + z;
            Destroy(t.GetComponent<Collider>());
            t.transform.SetParent(root, false);
            t.transform.localScale = new Vector3(0.96f, 0.1f, 0.96f);
            t.transform.position = new Vector3(x + 0.5f, -0.05f, z + 0.5f);
            float g = ((x + z) % 2 == 0) ? 0.38f : 0.32f;
            t.GetComponent<Renderer>().material.color = new Color(g, g, g);
        }
    }

    public CellBase PrefabOf(CellType t)
    {
        int i = (int)t;
        if (cellPrefabs == null || i >= cellPrefabs.Length || cellPrefabs[i] == null)
        {
            Debug.LogError("Prefab cell " + t + " belum di-assign. Jalankan Tools > Anomaly Facility > Setup Project.");
            return null;
        }
        return cellPrefabs[i];
    }

    // ---------- Mode ----------

    public void BeginPlace(CellType t)
    {
        if (!GameManager.Instance.IsUnlocked(t)) return;
        if (Placing.HasValue && Placing.Value == t) { CancelPlace(); return; }
        var prefab = PrefabOf(t);
        if (prefab == null) return;

        Placing = t;
        placeSize = prefab.DefaultSize;
        Selected = null;
        if (OnModeChanged != null) OnModeChanged();
    }

    public void SeedStartingFacility()
    {
        var ctrl = PlaceSeed(CellType.Control, new Vector2Int(3, 3));
        if (ctrl == null) return;
        PlaceSeed(CellType.Production, new Vector2Int(ctrl.origin.x + ctrl.size.x, ctrl.origin.y));
        PlaceSeed(CellType.Detention,  new Vector2Int(ctrl.origin.x, ctrl.origin.y + ctrl.size.y));
    }

    CellBase PlaceSeed(CellType t, Vector2Int o)
    {
        var prefab = PrefabOf(t);
        if (prefab == null || !CanPlace(o, prefab.DefaultSize)) return null;
        var cell = PlaceForLoad(t, o, prefab.DefaultSize);
        if (cell != null) cell.OnPlaced(GameManager.Instance);   // Control awal membuka Storage; Control kedua membuka Breeding
        FacilityNetwork.Recompute(cells);                        // hapus jika jaringan belum dipasang
        return cell;
    }
    public void CancelPlace()
    {
        if (!Placing.HasValue) return;
        Placing = null;
        ghost.SetActive(false);
        if (OnModeChanged != null) OnModeChanged();
    }

    public void Select(CellBase c)
    {
        Selected = c;
        if (OnModeChanged != null) OnModeChanged();
    }

    // ---------- Update ----------

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (Input.GetKeyDown(KeyCode.Escape)) CancelPlace();

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Placing.HasValue)
        {
            Vector2Int tile;
            bool hasTile = TryGetTile(ray, out tile);
            ghost.SetActive(hasTile && !overUI);
            if (!hasTile) return;

            CellType t = Placing.Value;
            bool valid = CanPlace(tile, placeSize) &&
                         GameManager.Instance.Energy >= GameConfig.EnergyCost(t);

            ghost.transform.localScale = new Vector3(placeSize.x * 0.94f, 0.2f, placeSize.y * 0.94f);
            ghost.transform.position = new Vector3(tile.x + placeSize.x / 2f, 0.1f, tile.y + placeSize.y / 2f);
            ghostRenderer.material.color = valid ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.3f, 0.3f);

            if (overUI) return;
            //if (Input.GetMouseButtonDown(1) && placeSize.x != placeSize.y)
             //   placeSize = new Vector2Int(placeSize.y, placeSize.x);
            if (Input.GetMouseButtonDown(0)) TryPlace(t, tile);
        }
        else if (Input.GetMouseButtonDown(0) && !overUI)
        {
            RaycastHit hit;
            CellBase c = null;
            if (Physics.Raycast(ray, out hit)) c = hit.collider.GetComponentInParent<CellBase>();
            Select(c);
        }
    }

    static bool TryGetTile(Ray ray, out Vector2Int tile)
    {
        tile = default(Vector2Int);
        float d;
        if (!Ground.Raycast(ray, out d)) return false;
        Vector3 p = ray.GetPoint(d);
        tile = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        return true;
    }

    // ---------- Place ----------

    bool CanPlace(Vector2Int o, Vector2Int s)
    {
        for (int x = o.x; x < o.x + s.x; x++)
        for (int y = o.y; y < o.y + s.y; y++)
        {
            if (x < 0 || y < 0 || x >= GameConfig.GridSize || y >= GameConfig.GridSize) return false;
            if (occupancy[x, y] != null) return false;
        }
        return true;
    }

    public bool IsFree(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
        return x >= 0 && z >= 0 && x < GameConfig.GridSize && z < GameConfig.GridSize
            && occupancy[x, z] == null;
    }

    public CellBase PlaceForLoad(CellType t, Vector2Int o, Vector2Int s)
    {
        var prefab = PrefabOf(t);
        if (prefab == null) return null;

        var cell = Instantiate(prefab, cellRoot);
        cell.Init(o, s);
        cells.Add(cell);
        for (int x = o.x; x < o.x + s.x; x++)
        for (int y = o.y; y < o.y + s.y; y++)
            occupancy[x, y] = cell;
        return cell;
    }

    void TryPlace(CellType t, Vector2Int o)
    {
        var gm = GameManager.Instance;
        var prefab = PrefabOf(t);
        if (prefab == null) return;

        if (!CanPlace(o, placeSize)) { gm.Log("Can't place here"); return; }

        int energy = GameConfig.EnergyCost(t);
        int salvage = GameConfig.SalvageCost(t);
        if (gm.Energy < energy)   { gm.Log("Not enough Energy  (need " + energy + ")"); return; }
        if (gm.Salvage < salvage) { gm.Log("Not enough Salvage  (need " + salvage + ")"); return; }

        var cell = Instantiate(prefab, cellRoot);
        cell.Init(o, placeSize);
        cells.Add(cell);
        for (int x = o.x; x < o.x + placeSize.x; x++)
        for (int y = o.y; y < o.y + placeSize.y; y++)
            occupancy[x, y] = cell;

        // bayar hanya setelah cell benar-benar terpasang
        gm.SpendEnergy(energy);
        gm.SpendSalvage(salvage);

        FacilityNetwork.Recompute(cells);
        gm.OnCellPlaced(cell);
    }

    public List<Vector3> FindPath(Vector3 from, CellBase target)
    {
        if (target == null) return null;
        int n = GameConfig.GridSize;
        var start = new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(from.x), 0, n - 1),
                                Mathf.Clamp(Mathf.FloorToInt(from.z), 0, n - 1));
        Vector3 center = new Vector3(target.transform.position.x, 0f, target.transform.position.z);
        if (InCell(target, start)) return null;

        var prev = new Vector2Int?[n, n];
        var seen = new bool[n, n];
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        seen[start.x, start.y] = true;

        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        Vector2Int? goal = null;

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (InCell(target, p)) { goal = p; break; }
            foreach (var d in dirs)
            {
                var nx = p + d;
                if (nx.x < 0 || nx.y < 0 || nx.x >= n || nx.y >= n || seen[nx.x, nx.y]) continue;
                if (!InCell(target, nx) && occupancy[nx.x, nx.y] != null) continue;
                seen[nx.x, nx.y] = true;
                prev[nx.x, nx.y] = p;
                q.Enqueue(nx);
            }
        }

        if (!goal.HasValue) return new List<Vector3> { center };   // buntu: jalan lurus

        var path = new List<Vector3>();
        for (Vector2Int? p = goal; p.HasValue; p = prev[p.Value.x, p.Value.y])
            path.Add(new Vector3(p.Value.x + 0.5f, 0f, p.Value.y + 0.5f));
        path.Reverse();
        path.RemoveAt(0);                          // tile awal: unit sudah di sana
        path[path.Count - 1] = center;             // tujuan akhir = pusat cell
        return path;
    }

    static bool InCell(CellBase c, Vector2Int t)
    {
        return t.x >= c.origin.x && t.x < c.origin.x + c.size.x
            && t.y >= c.origin.y && t.y < c.origin.y + c.size.y;
    }
}
