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
    public SpecialUnit special;

    public static int NextId { get { return nextId; } }
    public static void SetNextId(int n) { nextId = n; }
    public static void ResetIds() { nextId = 1; }

    public bool IsFemale
    {
        get { return special != null ? special.isFemale
                                    : (type == UnitType.Producer || type == UnitType.Breeder); }
    }

    /// True jika unit punya peran ini (tipe utama, atau tipe tambahan untuk unit spesial multiclass).
    public bool Is(UnitType t)
    {
        if (type == t) return true;
        return t != UnitType.Leader && special != null && special.extraTypes != null
            && System.Array.IndexOf(special.extraTypes, t) >= 0;
    }

    /// Jadikan unit ini jenis tertentu: tipe, model, skala, kecepatan, nama. Panggil SEBELUM AddUnit().
    public void ApplyArchetype(UnitArchetype a)
    {
        if (a == null) return;
        archetype = a;
        type = a.type;
        variant = a.RollVariant();
        scale = a.RollScale();
        moveSpeed = a.RollMoveSpeed();
        displayName = a.RollName();
        name = Label;
    }
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

    public AnimationClip BreedClip
    {
        get
        {
            if (special != null && special.breedClip != null) return special.breedClip;
            return archetype != null ? archetype.breedClip : null;
        }
    }

    public AnimationClip LeaderBreedClip
    {
        get
        {
            if (special != null && special.leaderBreedClip != null) return special.leaderBreedClip;
            return archetype != null ? archetype.leaderBreedClip : null;
        }
    }

    public bool LeaderFacesMe
    {
        get
        {
            if (special != null) return special.leaderFacesMe;
            return archetype != null && archetype.leaderFacesMe;
        }
    }

    public int BreedTicks
    {
        get
        {
            if (special != null && special.breedTicks > 0) return special.breedTicks;
            if (archetype != null && archetype.breedTicks > 0) return archetype.breedTicks;
            return GameConfig.BreedTicks;
        }
    }

    public Sprite Portrait
    {
        get
        {
            if (special != null && special.portrait != null) return special.portrait;
            return archetype != null ? archetype.portrait : null;
        }
    }

    public static UnitData CreateSpecial(SpecialUnit s)
    {
        var gm = GameManager.Instance;
        var u = CreateInstance<UnitData>();
        u.id = nextId++;
        u.type = s.type;
        u.special = s;
        u.archetype = (gm != null && gm.database != null) ? gm.database.Get(s.type) : null; // fallback clip/controller
        u.power = s.power;
        u.fertility = s.fertility;
        u.moveSpeed = s.moveSpeed;
        u.scale = s.scale;
        u.displayName = s.displayName;
        u.mood = GameConfig.MoodStart;
        u.name = u.Label;
        return u;
    }

    public string Label
    {
        get
        {
            string n = displayName + $"({type})" + " #" + id ;
            return special != null ? "* " + n : n;
        }
    }

    
}
