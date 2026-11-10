using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Window persiapan raid (UGUI dibangun lewat kode, dibuat oleh UIManager).
/// Mode 1 (prep): gambar target, briefing, pilih Fighter, pilih protokol, estimasi win chance.
/// Mode 2 (result): ringkasan hasil raid + status recovery.
public class RaidPanel : MonoBehaviour
{
    static readonly Color Normal = new Color(0.25f, 0.27f, 0.32f, 1f);
    static readonly Color Highlight = new Color(0.20f, 0.45f, 0.75f, 1f);

    GameManager gm;
    RaidManager raid;
    Font font;

    GameObject root, fighterSection, protocolRow;
    Image targetImage;
    Text targetTitle, briefingText, statsText, protocolDesc;
    Button safeBtn, aggressiveBtn, launchBtn;
    RectTransform fighterGrid;

    readonly HashSet<int> selected = new HashSet<int>();
    readonly Dictionary<int, Text> rowLabels = new Dictionary<int, Text>();
    readonly Dictionary<int, UnitData> rowUnits = new Dictionary<int, UnitData>();
    RaidProtocol protocol = RaidProtocol.Safe;
    bool showingResult;

    // ================= Build =================

    public void Build()
    {
        gm = GameManager.Instance;
        raid = RaidManager.Instance;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var self = (RectTransform)transform;
        Stretch(self);

        // Latar gelap (menghalangi klik ke grid)
        var dim = Box("Dim", transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim);
        root = dim.gameObject;

        var win = Box("Window", dim, new Color(0.10f, 0.11f, 0.14f, 1f));
        win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
        win.sizeDelta = new Vector2(720, 630);
        win.anchoredPosition = Vector2.zero;
        var v = win.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 12, 12);
        v.spacing = 8;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        Txt(win, "<b>RAID PREPARATION</b>", 20, TextAnchor.MiddleCenter, 30);

        // Baris atas: gambar target (kiri), briefing dan statistik (kanan)
        var top = Row(win, 176, 12);
        var left = Column(top, 8, 260);
        var imgGo = new GameObject("TargetImage", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        imgGo.transform.SetParent(left, false);
        imgGo.GetComponent<LayoutElement>().preferredHeight = 140;
        targetImage = imgGo.GetComponent<Image>();
        targetImage.preserveAspect = true;
        targetTitle = Txt(left, "", 15, TextAnchor.MiddleCenter, 28);

        var right = Column(top, 6, -1);
        briefingText = Txt(right, "", 13, TextAnchor.UpperLeft, 100);
        statsText = Txt(right, "", 14, TextAnchor.UpperLeft, 70);

        // Daftar Fighter
        fighterSection = new GameObject("Fighters", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        fighterSection.transform.SetParent(win, false);
        var fv = fighterSection.GetComponent<VerticalLayoutGroup>();
        fv.spacing = 4;
        fv.childControlWidth = fv.childControlHeight = true;
        fv.childForceExpandWidth = true;
        fv.childForceExpandHeight = false;
        fighterSection.GetComponent<LayoutElement>().preferredHeight = 214;
        Txt(fighterSection.transform, "<b>Select Fighters to deploy</b>", 14, TextAnchor.MiddleLeft, 22);

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(fighterSection.transform, false);
        var g = gridGo.GetComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(325, 28);
        g.spacing = new Vector2(6, 4);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;
        fighterGrid = gridGo.GetComponent<RectTransform>();

        // Protokol
        var pRow = Row(win, 34, 8);
        protocolRow = pRow.gameObject;
        Text l1, l2;
        safeBtn = Btn(pRow, "Safe Capture", 34, () => SetProtocol(RaidProtocol.Safe), out l1);
        aggressiveBtn = Btn(pRow, "Aggressive Extraction", 34, () => SetProtocol(RaidProtocol.Aggressive), out l2);
        protocolDesc = Txt(win, "", 13, TextAnchor.UpperLeft, 40);

        // Tombol bawah
        var bottom = Row(win, 40, 8);
        Text l3, l4;
        launchBtn = Btn(bottom, "LAUNCH RAID", 40, OnLaunch, out l3);
        Btn(bottom, "Close", 40, () => root.SetActive(false), out l4);

        gm.OnChanged += Refresh;
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (gm != null) gm.OnChanged -= Refresh;
    }

    // ================= Alur =================

    public void Open()
    {
        if (raid.CooldownLeft > 0) return;

        showingResult = false;
        protocol = RaidProtocol.Safe;

        var t = raid.CurrentTarget;
        targetImage.sprite = t.image;
        targetImage.color = t.image != null ? Color.white : new Color(0.20f, 0.22f, 0.26f);
        targetTitle.text = "<b>" + t.displayName + "</b>  -  " + t.anomalyClass;
        briefingText.text = t.briefing + "\n\n" + t.ClassHint();

        selected.Clear();
        foreach (var u in gm.units) if (u.type == UnitType.Fighter) selected.Add(u.id);
        RebuildFighters();

        SetMode(false);
        root.SetActive(true);
        Refresh();
    }

    void SetMode(bool result)
    {
        fighterSection.SetActive(!result);
        protocolRow.SetActive(!result);
        protocolDesc.gameObject.SetActive(!result);
        launchBtn.gameObject.SetActive(!result);
    }

    void SetProtocol(RaidProtocol p)
    {
        protocol = p;
        Refresh();
    }

    void OnLaunch()
    {
        var squad = Squad();
        if (squad.Count == 0) return;
        raid.Launch(squad, protocol);
        showingResult = true;
        SetMode(true);
        Refresh();
    }

    List<UnitData> Squad()
    {
        return gm.units.Where(u => selected.Contains(u.id)).ToList();
    }

    // ================= Fighter list =================

    void RebuildFighters()
    {
        foreach (Transform ch in fighterGrid)
        {
            ch.gameObject.SetActive(false);   // supaya tidak dihitung layout sebelum Destroy
            Destroy(ch.gameObject);
        }
        rowLabels.Clear();
        rowUnits.Clear();

        foreach (var u in gm.units.Where(x => x.type == UnitType.Fighter))
        {
            var unit = u;
            Text lbl;
            var btn = Btn(fighterGrid, "", 28, () => Toggle(unit), out lbl);
            UnitPortraitUI.Attach(btn, lbl, unit, 24f);
            lbl.fontSize = 13;
            lbl.fontSize = 13;
            rowLabels[unit.id] = lbl;
            rowUnits[unit.id] = unit;
            UpdateRow(unit);
        }
    }

    void Toggle(UnitData u)
    {
        if (!selected.Remove(u.id)) selected.Add(u.id);
        Refresh();
    }

    void UpdateRow(UnitData u)
    {
        Text lbl;
        if (!rowLabels.TryGetValue(u.id, out lbl)) return;
        lbl.text = (selected.Contains(u.id) ? "[x] " : "[   ] ") + u.Label
                 + "   P" + u.power + "   Mood " + u.mood
                 + "   eff " + raid.Contribution(u).ToString("0.#");
    }

    // ================= Refresh =================

    void Refresh()
    {
        if (root == null || !root.activeSelf) return;

        if (showingResult)
        {
            briefingText.text = raid.LastResult;
            statsText.text = "Facility recovering from raid (" + raid.CooldownLeft + ")";
            return;
        }

        foreach (var kv in rowUnits) UpdateRow(kv.Value);

        var squad = Squad();
        float power = raid.SquadPower(squad);
        float chance = raid.WinChance(power, protocol);

        statsText.text = "Enemy power: " + raid.EnemyPower
            + "\nSquad: " + squad.Count + " Fighter(s), effective power " + power.ToString("0.#")
            + "\nWin chance: <b>" + Mathf.RoundToInt(chance * 100f) + "%</b>";

        launchBtn.interactable = squad.Count > 0;
        safeBtn.GetComponent<Image>().color = protocol == RaidProtocol.Safe ? Highlight : Normal;
        aggressiveBtn.GetComponent<Image>().color = protocol == RaidProtocol.Aggressive ? Highlight : Normal;
        protocolDesc.text = protocol == RaidProtocol.Safe
            ? "Safe Capture: -10% win chance. Captured units are stronger (+1 power), more Perfect results, better odds of a special unit."
            : "Aggressive Extraction: +15% win chance. More Costly results, failures cost -4 power, higher catastrophe risk.";
    }

    // ================= UI helpers =================

    RectTransform Box(string name, Transform parent, Color c)
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
        t.color = Color.white;
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
        go.GetComponent<Image>().color = Normal;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var b = go.GetComponent<Button>();
        b.onClick.AddListener(click);
        text = Txt(go.transform, label, 14, TextAnchor.MiddleCenter, 0);
        Stretch(text.rectTransform);
        return b;
    }

    RectTransform Row(Transform parent, float height, float spacing)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return go.GetComponent<RectTransform>();
    }

    RectTransform Column(Transform parent, float spacing, float width)
    {
        var go = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        var le = go.GetComponent<LayoutElement>();
        if (width > 0f) le.preferredWidth = width; else le.flexibleWidth = 1f;
        return go.GetComponent<RectTransform>();
    }
}
