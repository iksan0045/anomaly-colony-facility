using System.Collections.Generic;
using System.Text;
using UnityEngine.Serialization;   
using UnityEngine;

/// Basis semua cell. Subclass menentukan Type + perilaku tick.
/// Prefab cell = root (komponen subclass) + child "Body" (Cube + collider).
public abstract class CellBase : MonoBehaviour
{
    public const float Height = 0.6f;

    [HideInInspector] public Vector2Int origin;
    [HideInInspector] public Vector2Int size = Vector2Int.one;
    public readonly List<UnitData> units = new List<UnitData>();

    public abstract CellType Type { get; }
    public Vector2Int DefaultSize { get { return GameConfig.DefaultSize(Type); } }
    public int Capacity { get { return GameConfig.Capacity(Type); } }
    public int EnergyCost { get { return GameConfig.EnergyCost(Type); } }

    /// Perubahan mood per tick untuk unit di cell ini.
    public virtual int MoodPerTick { get { return -GameConfig.MoodDecayPerTick; } }

    [Header("Visual (opsional)")]
    [Tooltip("true = Body cube di-scale ke ukuran cell (placeholder). Matikan jika pakai model 3D.")]
    public bool scaleBodyToSize = true;
    [Tooltip("Titik berdiri unit, berurutan: slot 0, 1, ... Pivot unit ada di kaki.")]
    public Transform[] unitSlots;
    public abstract void OnTick(GameManager gm);
    public virtual void OnPlaced(GameManager gm) { }
    protected virtual void OnUnitsChanged() { }
    protected virtual void DescribeDetails(StringBuilder sb) { }

    [Header("Animasi unit")]
    [Tooltip("Clip (loop) yang dimainkan unit yang ditugaskan di cell ini.")]
    [FormerlySerializedAs("activeClip")]   // clip yang sudah kamu drag di prefab tidak hilang
    public AnimationClip unitClip;

    protected virtual Vector3 UnitOffset(UnitData u) { return Vector3.zero; }
    ClipPlayer cellPlayer;
    [Tooltip("true = semua unit di cell ini memakai Slot_0 (posisi dan rotasi sama), mis. untuk animasi breeding berpasangan.")]
    public bool shareFirstSlot = false;

    /// Subclass override: kapan animasi cell berjalan.
    protected virtual bool IsActive { get { return false; } }
    protected virtual float UnitYaw(UnitData u) { return 0f; }

    // ---------- Placement ----------

    
    protected virtual AnimationClip ClipFor(UnitData u) { return unitClip; }

    /// Kapan clip itu dimainkan. Default: selama unit ada di cell.
    protected virtual bool ShouldAnimate(UnitData u) { return true; }

    void Awake()
    {
        int needed = shareFirstSlot ? 1 : Capacity;
        if (unitSlots != null && unitSlots.Length > 0 && unitSlots.Length < needed)
        Debug.LogWarning(name + ": unitSlots (" + unitSlots.Length + ") kurang dari yang dibutuhkan (" + needed + ")", this);   
    }


    void Update()
    {
        foreach (var u in units)
        {
            if (u.controller == null || u.controller.visual == null) continue;
            u.controller.visual.SetClip(ShouldAnimate(u) ? ClipFor(u) : null);
        }
    }



    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        var col = gameObject.AddComponent<BoxCollider>();
        col.center = transform.InverseTransformPoint(b.center);
        col.size = b.size;
    }

    void OnDrawGizmosSelected()   // titik slot terlihat di Scene view saat prefab dipilih
    {
        if (unitSlots == null) return;
        Gizmos.color = Color.cyan;
        foreach (var s in unitSlots)
        {
            if (s == null) continue;
            Gizmos.DrawWireSphere(s.position, 0.12f);
            Gizmos.DrawRay(s.position, s.forward * 0.3f);
        }
    }
    public void Init(Vector2Int o, Vector2Int s)
    {
        origin = o;
        size = s;
        name = "Cell_" + Type + "_" + o.x + "_" + o.y;
        transform.position = new Vector3(o.x + s.x / 2f, 0f, o.y + s.y / 2f);

        if (scaleBodyToSize)
        {
            var body = transform.Find("Body");
            if (body != null)
            {
                body.localScale = new Vector3(s.x * 0.94f, Height, s.y * 0.94f);
                body.localPosition = new Vector3(0f, Height / 2f, 0f);
            }
        }
        else
        {
            // Model custom: putar 90° kalau cell dirotasi (mis. 1x2 jadi 2x1). Slot ikut berputar.
            transform.rotation = (s != DefaultSize) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        }
    }

    // ---------- Unit ----------

    public virtual bool CanAccept(UnitData u, out string reason)
    {
        reason = null;
        if (Capacity == 0) { reason = GameConfig.Label(Type) + " cannot hold units"; return false; }
        if (units.Count >= Capacity) { reason = "Cell is full"; return false; }
        return true;
    }

    public bool AssignUnit(UnitData u)
    {
        string reason;
        if (!CanAccept(u, out reason)) { GameManager.Instance.Log(reason); return false; }

        if (u.cell != null) u.cell.RemoveUnit(u);
        units.Add(u);
        u.cell = this;
        if (u.controller != null) u.controller.AttachTo(this);

        LayoutUnits();
        OnUnitsChanged();
        // CellBase.AssignUnit, sebelum NotifyChanged()
        GameManager.Instance.Log(u.Label + " assigned to " + GameConfig.Label(Type));
        GameManager.Instance.NotifyChanged();
        return true;
    }

    public void RemoveUnit(UnitData u)
    {
        if (!units.Remove(u)) return;
        u.cell = null;
        if (u.controller != null) u.controller.Detach();

        LayoutUnits();
        OnUnitsChanged();
        GameManager.Instance.NotifyChanged();
    }

    void LayoutUnits()
    {
        for (int i = 0; i < units.Count; i++)
        {
            var c = units[i].controller;
            if (c == null) continue;

            Transform slot = null;
            if (unitSlots != null && unitSlots.Length > 0)
                slot = shareFirstSlot ? unitSlots[0] : (i < unitSlots.Length ? unitSlots[i] : null);

            if (slot != null)
            {
                c.transform.SetPositionAndRotation(slot.position, slot.rotation);
            }
            else   // fallback lama
            {
                float offset = (i - (units.Count - 1) / 2f) * 0.45f;
                c.transform.localPosition = new Vector3(offset, Height, 0f);
                c.transform.localRotation = Quaternion.identity;
            }
            var baseRot = c.transform.rotation;                                   // rotasi slot
            c.transform.position += baseRot * UnitOffset(units[i]);               // geser di sumbu lokal slot
            c.transform.rotation = baseRot * Quaternion.Euler(0f, UnitYaw(units[i]), 0f);
        }
    }

    // ---------- Info panel kanan ----------

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>" + GameConfig.Label(Type) + "</b>  (" + size.x + "x" + size.y + ")");
        DescribeDetails(sb);
        if (Capacity > 0)
        {
            sb.AppendLine("Unit: " + units.Count + "/" + Capacity);
            foreach (var u in units)
                sb.AppendLine("- " + u.Label + "  P" + u.power + "  F" + u.fertility + "  Mood " + u.mood);
        }
        return sb.ToString();
    }
}
