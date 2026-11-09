using UnityEngine;

/// Data satu anomali. ScriptableObject; instance dibuat runtime lewat UnitData.Create().
public class UnitData : ScriptableObject
{
    static int nextId = 1;

    public int id;
    public UnitType type;
    [Range(1, 10)] public int power;
    [Range(0, 100)] public int mood;
    [Range(1, 10)] public int fertility;

    [System.NonSerialized] public CellBase cell;             // null = belum di-assign
    [System.NonSerialized] public UnitController controller; // visual (bola)

    public UnitArchetype archetype;      // tambahkan [System.NonSerialized] jika tidak mau diserialisasi
    public int variant;
    public float moveSpeed = 1.2f;
    public float scale = 1f;
    public string displayName;

    public static void ResetIds() { nextId = 1; }

    public static UnitData Create(UnitType type, int power = -1, int fertility = -1)
    {
        var gm = GameManager.Instance;
        var a = (gm != null && gm.database != null) ? gm.database.Get(type) : null;

        var u = CreateInstance<UnitData>();
        u.id = nextId++;
        u.type = type;
        u.archetype = a;
        u.power = Mathf.Clamp(power >= 0 ? power : (a != null ? a.RollPower() : Random.Range(1, 11)), 1, 10);
        u.fertility = Mathf.Clamp(fertility >= 0 ? fertility : (a != null ? a.RollFertility() : Random.Range(1, 11)), 1, 10);
        u.mood = GameConfig.MoodStart;
        if (a != null)
        {
            u.variant = a.RollVariant();
            u.moveSpeed = a.RollMoveSpeed();
            u.scale = a.RollScale();
            u.displayName = a.RollName();
        }
        u.name = u.Label;
        return u;
    }

    public string Label
    {
        get { return string.IsNullOrEmpty(displayName) ? type + " #" + id : displayName + " #" + id; }
    }

    
}
