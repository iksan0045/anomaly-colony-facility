using System.Collections.Generic;
using System.Text;
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

    public abstract void OnTick(GameManager gm);
    public virtual void OnPlaced(GameManager gm) { }
    protected virtual void OnUnitsChanged() { }
    protected virtual void DescribeDetails(StringBuilder sb) { }

    // ---------- Placement ----------

    public void Init(Vector2Int o, Vector2Int s)
    {
        origin = o;
        size = s;
        name = "Cell_" + Type + "_" + o.x + "_" + o.y;
        transform.position = new Vector3(o.x + s.x / 2f, 0f, o.y + s.y / 2f);

        var body = transform.Find("Body");
        if (body != null)
        {
            body.localScale = new Vector3(s.x * 0.94f, Height, s.y * 0.94f);
            body.localPosition = new Vector3(0f, Height / 2f, 0f);
        }
    }

    // ---------- Unit ----------

    public virtual bool CanAccept(UnitData u, out string reason)
    {
        reason = null;
        if (Capacity == 0) { reason = GameConfig.Label(Type) + " tidak menampung unit"; return false; }
        if (units.Count >= Capacity) { reason = "Cell penuh"; return false; }
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
        GameManager.Instance.Log(u.Label + " masuk " + GameConfig.Label(Type));
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
            float offset = (i - (units.Count - 1) / 2f) * 0.45f;
            c.transform.localPosition = new Vector3(offset, Height, 0f);   // sebelumnya Height + 0.15f
            c.transform.localRotation = Quaternion.identity;
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
