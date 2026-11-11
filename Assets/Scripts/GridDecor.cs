using UnityEngine;

/// Dinding dekoratif di dua tepi belakang grid (kamera isometric default: kiri-belakang = tepi z=N,
/// kanan-belakang = tepi x=N). Tepi depan dibiarkan kosong supaya tidak menghalangi pandangan.
///
/// Konvensi prefab dinding:
///   - Pivot di tengah-bawah.
///   - Lebar sepanjang sumbu X lokal sebesar 'span' tile (1 tile = 1 unit).
///   - Sisi depan (yang menghadap ke dalam grid) = arah -Z lokal.
/// Tanpa prefab, dibuat dinding grey-box otomatis dengan tinggi dan warna yang bervariasi.
/// Tempel komponen ini ke objek "Managers" untuk mengisi prefab di Inspector. Jika belum ada,
/// GridManager menambahkannya otomatis (mode grey-box).
public class GridDecor : MonoBehaviour
{
    [System.Serializable]
    public class WallPiece
    {
        public GameObject prefab;
        [Min(1)] public int span = 1;
        [Min(0.01f)] public float weight = 1f;
    }

    [Header("Variasi dinding (kosong = grey-box otomatis)")]
    [Tooltip("Sertakan minimal satu piece berspan 1 supaya sisa tile di ujung tetap terisi.")]
    public WallPiece[] pieces;
    [Tooltip("Tiang sudut belakang. Kosong = pilar grey-box.")]
    public GameObject cornerPrefab;

    [Header("Tata letak")]
    public bool backLeft = true;     // tepi z = N
    public bool backRight = true;    // tepi x = N
    [Tooltip("Ketebalan dinding ke arah luar grid.")]
    public float thickness = 0.3f;
    [Tooltip("Seed yang sama = susunan dinding sama setiap game.")]
    public int seed = 12345;

    WallPiece lastPiece;  

    [Header("Grey-box")]
    public Vector2 greyHeightRange = new Vector2(0.9f, 1.6f);

    Transform decorRoot;

    void Start() { Build(); }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        if (decorRoot != null) Destroy(decorRoot.gameObject);
        decorRoot = new GameObject("Decor").transform;   // tanpa parent: posisi dunia pasti benar

        var rng = new System.Random(seed);
        int n = GameConfig.GridSize;

        if (backLeft) BuildEdge(rng, n, false);
        if (backRight) BuildEdge(rng, n, true);
        if (backLeft || backRight) PlaceCorner(n);
    }

    void BuildEdge(System.Random rng, int n, bool alongZ)
    {
        lastPiece = null;                       // baru
        int i = 0;
        while (i < n)
        {
           WallPiece piece = Pick(rng, n - i);
           lastPiece = piece;                  // baru
           int span = piece != null ? piece.span : 1;

            float mid = i + span / 2f;
            Vector3 pos = alongZ ? new Vector3(n + thickness / 2f, 0f, mid)
                                 : new Vector3(mid, 0f, n + thickness / 2f);
            Quaternion rot = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);   // sisi depan menghadap ke dalam grid

            GameObject go = piece != null ? Instantiate(piece.prefab) : MakeGreyWall(rng, span);
            go.transform.SetParent(decorRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            StripColliders(go);

            i += span;
        }
    }

    WallPiece Pick(System.Random rng, int remaining)
    {
        if (pieces == null) return null;
        for (int pass = 0; pass < 2; pass++)
        {
            bool avoid = pass == 0;   // pass 1 = longgar, jika hanya ada 1 jenis
            float total = 0f;
            foreach (var p in pieces) if (!Skip(p, remaining, avoid)) total += p.weight;
            if (total <= 0f) continue;

            double r = rng.NextDouble() * total;
            foreach (var p in pieces)
            {
                if (Skip(p, remaining, avoid)) continue;
                r -= p.weight;
                if (r <= 0.0) return p;
            }
        }
        return null;
    }

    bool Skip(WallPiece p, int remaining, bool avoid)
    => p == null || p.prefab == null || p.span > remaining || (avoid && p == lastPiece);

    void PlaceCorner(int n)
    {
        GameObject go;
        if (cornerPrefab != null)
        {
            go = Instantiate(cornerPrefab);
        }
        else
        {
            go = new GameObject("GreyPillar");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            float h = greyHeightRange.y + 0.4f;
            cube.transform.localScale = new Vector3(thickness * 1.6f, h, thickness * 1.6f);
            cube.transform.localPosition = new Vector3(0f, h / 2f, 0f);
            cube.GetComponent<Renderer>().material.color = new Color(0.18f, 0.18f, 0.2f);
        }
        go.transform.SetParent(decorRoot, false);
        go.transform.SetPositionAndRotation(new Vector3(n + thickness / 2f, 0f, n + thickness / 2f), Quaternion.identity);
        StripColliders(go);
    }

    GameObject MakeGreyWall(System.Random rng, int span)
    {
        float h = Mathf.Lerp(greyHeightRange.x, greyHeightRange.y, (float)rng.NextDouble());
        float g = Mathf.Lerp(0.22f, 0.34f, (float)rng.NextDouble());

        var root = new GameObject("GreyWall");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = new Vector3(span * 0.98f, h, thickness);
        cube.transform.localPosition = new Vector3(0f, h / 2f, 0f);   // pivot di tengah-bawah
        cube.GetComponent<Renderer>().material.color = new Color(g, g, g * 1.05f);
        return root;
    }

    // Dinding hanya visual: jangan mengganggu raycast pilih cell
    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
    }
}
