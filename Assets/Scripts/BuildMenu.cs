using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Menu bangun: jendela berisi daftar cell (nama, deskripsi, biaya, status). Dibuat lewat kode
/// seperti RaidPanel. Tombol BUILD memanggil Toggle(); memilih cell menutup menu dan memulai penempatan.
/// Buat di tempat yang sama dengan RaidPanel:
///     var go = new GameObject("BuildMenu", typeof(RectTransform), typeof(BuildMenu));
///     go.transform.SetParent(canvasRoot, false);
///     buildMenu = go.GetComponent<BuildMenu>(); buildMenu.Build();
///     buildButton.onClick.AddListener(buildMenu.Toggle);
public class BuildMenu : MonoBehaviour
{
    GameObject root;
    RectTransform list;
    Font font;
    GameManager gm;
    GridManager grid;

    public void Build()
    {
        font = UITheme.Active.GetFont();
        Stretch((RectTransform)transform);

        var dim = Box("Dim", transform, UITheme.Active.dimColor);
        Stretch(dim);
        root = dim.gameObject;
        var dimBtn = root.AddComponent<Button>();            // klik di luar jendela = tutup
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var win = Box("Window", dim, UITheme.Active.windowColor);
        UITheme.Active.StylePanel(win.GetComponent<Image>(), true);
        win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
        win.sizeDelta = new Vector2(560, 470);
        win.anchoredPosition = Vector2.zero;
        var v = win.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 12, 12);
        v.spacing = 8;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        Txt(win, "<b>BUILD</b>", 20, TextAnchor.MiddleCenter, 30);

        var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        listGo.transform.SetParent(win, false);
        var lv = listGo.GetComponent<VerticalLayoutGroup>();
        lv.spacing = 6;
        lv.childControlWidth = lv.childControlHeight = true;
        lv.childForceExpandWidth = true;
        lv.childForceExpandHeight = false;
        listGo.GetComponent<LayoutElement>().preferredHeight = 340;
        list = (RectTransform)listGo.transform;

        Text t;
        Btn(win, "Close", 36, Close, out t);

        root.SetActive(false);
    }

    void Update()
    {
        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Toggle()
    {
        if (root != null && root.activeSelf) Close(); else Open();
    }

    public void Open()
    {
        gm = GameManager.Instance;
        grid = GridManager.Instance;
        if (gm == null || grid == null) return;

        if (grid.Placing.HasValue) grid.CancelPlace();
        gm.OnChanged -= Rebuild;
        gm.OnChanged += Rebuild;
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        if (gm != null) gm.OnChanged -= Rebuild;
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (gm != null) gm.OnChanged -= Rebuild;
    }

    // ================= Daftar =================

    void Rebuild()
    {
        if (root == null || !root.activeSelf) return;

        foreach (Transform ch in list)
        {
            ch.gameObject.SetActive(false);
            Destroy(ch.gameObject);
        }

        foreach (CellType t in System.Enum.GetValues(typeof(CellType)))
        {
            int i = (int)t;
            if (grid.cellPrefabs == null || i >= grid.cellPrefabs.Length || grid.cellPrefabs[i] == null) continue;
            AddRow(t);
        }
    }

    void AddRow(CellType t)
    {
        bool unlocked = gm.IsUnlocked(t);
        int e = GameConfig.EnergyCost(t);
        int s = GameConfig.SalvageCost(t);
        bool afford = gm.Energy >= e && gm.Salvage >= s;

        var row = Box("Row_" + t, list, new Color(0f, 0f, 0f, 0.25f));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 6, 6);
        h.spacing = 10;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        var sw = Box("Swatch", row, GameConfig.ColorOf(t));
        sw.gameObject.AddComponent<LayoutElement>().preferredWidth = 10;

        var info = Txt(row, "<b>" + GameConfig.Label(t) + "</b>\n" + Describe(t), 13, TextAnchor.MiddleLeft, 0);
        info.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var cost = Txt(row, e + " E\n" + s + " S", 14, TextAnchor.MiddleCenter, 0);
        cost.GetComponent<LayoutElement>().preferredWidth = 56;
        cost.color = afford ? UITheme.Active.textColor : new Color(1f, 0.45f, 0.45f);

        Text lbl;
        var b = Btn(row, !unlocked ? "Locked" : (afford ? "Build" : "Need more"), 0, () => Pick(t), out lbl);
        b.GetComponent<LayoutElement>().preferredWidth = 100;
        b.interactable = unlocked && afford;
    }

    void Pick(CellType t)
    {
        Close();
        grid.BeginPlace(t);
    }

    static string Describe(CellType t)
    {
        switch (t)
        {
            case CellType.Detention:  return "Units rest here: +mood every tick. Tired units go here automatically.";
            case CellType.Production: return "Producers make Energy. Fighters make Material.";
            case CellType.Breeding:   return "Breeders (+ Leader for better results) make new units. A Fighter here only relaxes.";
            case CellType.Storage:    return "Raises the maximum of every resource.";
            case CellType.Control:    return "Raises the unit limit. Hub of the facility.";
            default:                  return "";
        }
    }

    // ================= UI helpers =================

    static RectTransform Box(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = c;
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Text Txt(Transform parent, string s, int size, TextAnchor anchor, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = UITheme.Active.textColor;
        t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return t;
    }

    Button Btn(Transform parent, string label, float height, UnityAction click, out Text text)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var b = go.GetComponent<Button>();
        UITheme.Active.StyleButton(b);
        b.onClick.AddListener(click);
        text = Txt(go.transform, label, 14, TextAnchor.MiddleCenter, 0);
        Stretch(text.rectTransform);
        return b;
    }
}
