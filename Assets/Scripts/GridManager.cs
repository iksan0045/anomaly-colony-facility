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
        for (int x = 0; x < GameConfig.GridSize; x++)
        for (int z = 0; z < GameConfig.GridSize; z++)
        {
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
        int cost = GameConfig.EnergyCost(t);
        if (!gm.SpendEnergy(cost)) { gm.Log("Not enough Energy  (need " + cost + ")"); return; }

        var cell = Instantiate(prefab, cellRoot);
        cell.Init(o, placeSize);
        cells.Add(cell);
        for (int x = o.x; x < o.x + placeSize.x; x++)
        for (int y = o.y; y < o.y + placeSize.y; y++)
            occupancy[x, y] = cell;

        gm.OnCellPlaced(cell);
    }
}
