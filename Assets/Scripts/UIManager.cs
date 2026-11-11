using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class PaletteEntry
{
    public CellType type;
    public Button button;
    public Text label;
}

/// HUD, palette, panel detail, tombol RAID, log. UGUI (Canvas Screen Space - Camera).
/// Hierarki dibuat di EDITOR oleh Tools > Anomaly Facility > Setup Project (lewat BuildHierarchy),
/// jadi bisa kamu edit/atur ulang di scene. Kalau referensi kosong, otomatis dibuat saat Play.
public class UIManager : MonoBehaviour
{
    [Header("Referensi (diisi oleh BuildHierarchy)")]
    public Text topText;
    public Text selectedInfo;
    public Text raidLabel;
    public Text logText;
    public Button raidButton;
    public ScrollRect logScroll;
    public RectTransform rightPanel;
    public PaletteEntry[] palette;

    [Header("Tema")]
    public UITheme theme;

    GameManager gm;
    GridManager grid;
    RaidManager raid;
    Font font;
    string rightSig = "";
    bool logDirty;

    Font F
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // ================= Runtime =================

    void Awake()
    {
        if (theme != null) UITheme.Active = theme;
    }
    void Start()
    {
        gm = GameManager.Instance;
        grid = GridManager.Instance;
        raid = RaidManager.Instance;

        if (topText == null) BuildHierarchy(Camera.main); // fallback
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var rp = new GameObject("RaidPanel", typeof(RectTransform)).AddComponent<RaidPanel>();
        rp.transform.SetParent(transform, false);
        rp.Build();
        raidButton.onClick.AddListener(() => rp.Open());
        foreach (var p in palette)
        {
            CellType type = p.type;
            p.button.onClick.AddListener(() => grid.BeginPlace(type));
        }

        gm.OnChanged += Refresh;
        gm.OnLog += OnLog;
        grid.OnModeChanged += Refresh;

        RebuildLogText();
        Refresh();
    }

    void OnDestroy()
    {
        if (gm != null) { gm.OnChanged -= Refresh; gm.OnLog -= OnLog; }
        if (grid != null) grid.OnModeChanged -= Refresh;
    }

    void LateUpdate()
    {
        if (!logDirty) return;
        Canvas.ForceUpdateCanvases();
        logScroll.verticalNormalizedPosition = 0f;
        logDirty = false;
    }

    // ================= Refresh =================

    void Refresh()
    {
        if (topText == null) return;

        topText.text = "Energy: " + gm.Energy + "/" + gm.MaxResource
            + "     Material: " + gm.Materials + "/" + gm.MaxResource
            + "     Units: " + gm.units.Count + "/" + gm.MaxUnits
            + "     Tick: " + gm.Tick;

        foreach (var p in palette)
        {
            bool open = gm.IsUnlocked(p.type);
            p.button.interactable = open;
            p.label.text = open
                ? GameConfig.Label(p.type) + " (" + GameConfig.EnergyCost(p.type) + "E)"
                  + (grid.Placing.HasValue && grid.Placing.Value == p.type ? "  *" : "")
                : GameConfig.Label(p.type) + " (locked)";
        }

        raidButton.interactable = raid.CooldownLeft <= 0;
        raidLabel.text = raid.CooldownLeft > 0
            ? "RAID  (facility recovering " + raid.CooldownLeft + ")"
            : "RAID  -  " + raid.CurrentTarget.displayName;

        var sel = grid.Selected;
        if (sel == null)
            selectedInfo.text = grid.Placing.HasValue
                ? "Placing: click the grid.\nEsc / click the button again = cancel."
                : "Click a cell to see its details."; //\nRight-click = rotate.
        else
            selectedInfo.text = sel.Describe();

        // Rebuild daftar tombol hanya jika struktur berubah (supaya klik tidak hilang tiap tick)
        var sb = new StringBuilder();
        sb.Append(sel != null ? sel.GetInstanceID() : 0).Append('|');
        if (sel != null) foreach (var u in sel.units) sb.Append(u.id).Append(',');
        sb.Append('|');
        foreach (var u in gm.units) if (u.cell == null) sb.Append(u.id).Append(':').Append(u.power).Append(',');
        string sig = sb.ToString();
        if (sig != rightSig)
        {
            rightSig = sig;
            RebuildRight(sel);
        }
    }

    void EnsureSaveButtons()
    {
        var bar = topText.transform.parent as RectTransform;
        string[] names = { "Save", "Load", "New" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Text lbl;
            var b = MakeButton(bar, names[i], 26, null, out lbl);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(60, 26);
            rt.anchoredPosition = new Vector2(-8 - (2 - i) * 66, 0);
            b.onClick.AddListener(() =>
            {
                var s = SaveManager.Instance;
                if (s == null) return;
                if (index == 0) s.SaveNow();
                else if (index == 1) s.LoadGame();
                else s.NewGame();
            });
        }
    }

    void RebuildRight(CellBase sel)
    {
        for (int i = rightPanel.childCount - 1; i >= 0; i--)
        {
            var ch = rightPanel.GetChild(i);
            if (ch != selectedInfo.transform) Destroy(ch.gameObject);
        }

        Text unused;
        if (sel != null && sel.units.Count > 0)
        {
            Label(rightPanel, "H1", 15, TextAnchor.MiddleLeft, "<b>Inside cell</b>");
            foreach (var u in new List<UnitData>(sel.units))
            {
                UnitData unit = u;
                Text lbl;
                var b = MakeButton(rightPanel, "Release: " + u.Label, 26, () => sel.RemoveUnit(unit), out lbl);
                UnitPortraitUI.Attach(b, lbl, unit);
            }
        }

        var free = new List<UnitData>();
        foreach (var u in gm.units) if (u.cell == null) free.Add(u);

        Label(rightPanel, "H2", 15, TextAnchor.MiddleLeft, "<b>Free units (" + free.Count + ")</b>");
        foreach (var u in free)
        {
            UnitData unit = u;
            string text = u.Label + "  P" + u.power;

            if (sel == null)
            {
                Label(rightPanel, "Free", 13, TextAnchor.MiddleLeft, "- " + text);
            }
            else
            {
                Text lbl;
                var b = MakeButton(rightPanel, "Assign: " + text, 26, () => sel.AssignUnit(unit), out lbl);
                UnitPortraitUI.Attach(b, lbl, unit);
            }
        }
    }

    // ================= Log =================

    void OnLog(string line)
    {
        if (logText == null) return;
        RebuildLogText();
        logDirty = true;
    }

    void RebuildLogText()
    {
        var sb = new StringBuilder();
        int start = Mathf.Max(0, gm.LogLines.Count - 100);
        for (int i = start; i < gm.LogLines.Count; i++) sb.AppendLine(gm.LogLines[i]);
        logText.text = sb.ToString();
    }

    // ================= Build hierarchy (dipanggil editor tool) =================

    public void BuildHierarchy(Camera cam)
    {
        if (theme != null) UITheme.Active = theme;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var g = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }

        var canvas = GetOrAdd<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        var scaler = GetOrAdd<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>();

        Transform root = transform;
        var dark = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        // HUD atas
        var top = Panel("TopBar", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), Vector2.zero, dark);
        topText = Label(top, "HUD", 16, TextAnchor.MiddleCenter);
        Stretch(topText.rectTransform, 0);

        // Panel kiri: palette cell
        var left = Panel("Palette", root, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 200), new Vector2(180, -36), dark);
        VList(left, 6, 4);
        Label(left, "Title", 15, TextAnchor.MiddleLeft, "<b>CELL</b>");
        var entries = new List<PaletteEntry>();
        foreach (CellType t in System.Enum.GetValues(typeof(CellType)))
        {
            Text lbl;
            var b = MakeButton(left, GameConfig.Label(t), 34, null, out lbl);
            b.GetComponent<Image>().color = Color.Lerp(GameConfig.ColorOf(t), Color.black, 0.45f);
            entries.Add(new PaletteEntry { type = t, button = b, label = lbl });
        }
        palette = entries.ToArray();

        // Log kiri bawah (scroll)
        var logPanel = Panel("Log", root, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(380, 190), dark);
        var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        vp.SetParent(logPanel, false);
        Stretch(vp, 4);
        logText = Label(vp, "Content", 13, TextAnchor.UpperLeft);
        var crt = logText.rectTransform;
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0, 1);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;
        logText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        logScroll = logPanel.gameObject.AddComponent<ScrollRect>();
        logScroll.viewport = vp;
        logScroll.content = crt;
        logScroll.horizontal = false;
        logScroll.movementType = ScrollRect.MovementType.Clamped;

        // Panel kanan: detail cell
        rightPanel = Panel("Detail", root, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-280, 0), new Vector2(0, -36), dark);
        VList(rightPanel, 8, 4);
        selectedInfo = Label(rightPanel, "Info", 14, TextAnchor.UpperLeft, "Click a cell to see its details.");

        // Tombol RAID
        Text rl;
        raidButton = MakeButton(root, "RAID", 50, null, out rl);
        raidLabel = rl;
        raidLabel.fontSize = 18;
        var rrt = raidButton.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0);
        rrt.pivot = new Vector2(0.5f, 0);
        rrt.sizeDelta = new Vector2(320, 50);
        rrt.anchoredPosition = new Vector2(0, 12);
    }

    // ================= Helpers =================

    T GetOrAdd<T>() where T : Component
    {
        var c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    [ContextMenu("Apply Theme To Scene UI")]
    public void ApplyThemeToScene()
    {
        var th = theme != null ? theme : UITheme.Active;
        UITheme.Active = th;

        foreach (var panelName in new[] { "TopBar", "Palette", "Log", "Detail" })
        {
            var p = transform.Find(panelName);
            if (p != null) th.StylePanel(p.GetComponent<Image>());
        }
        foreach (var b in GetComponentsInChildren<Button>(true)) th.StyleButton(b);
        foreach (var t in GetComponentsInChildren<Text>(true)) th.StyleText(t);

        if (palette != null)   // tint tombol cell mengikuti warna cell
            foreach (var e in palette)
                e.button.GetComponent<Image>().color = Color.Lerp(GameConfig.ColorOf(e.type), Color.black, 0.45f);

    #if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    #endif
    }

    RectTransform Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
        UITheme.Active.StylePanel(go.GetComponent<Image>());
        return rt;
    }

    Text Label(Transform parent, string name, int size, TextAnchor anchor, string text = "")
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        UITheme.Active.StyleText(t);
        t.fontSize = size;
        t.alignment = anchor;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(Transform parent, string label, float height, UnityAction onClick, out Text labelText)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var b = go.GetComponent<Button>();
        UITheme.Active.StyleButton(b);
        if (onClick != null) b.onClick.AddListener(onClick);
        var t = Label(go.transform, "Text", 14, TextAnchor.MiddleCenter, label);
        Stretch(t.rectTransform, 2);
        labelText = t;
        return b;
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    static void VList(RectTransform rt, int pad, int spacing)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(pad, pad, pad, pad);
        v.spacing = spacing;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
    }
}
