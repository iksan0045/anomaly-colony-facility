using UnityEngine;

/// Template satu jenis unit: model 3D, rentang stat, nama. Satu asset per UnitType.
/// Dibuat otomatis oleh Tools > Anomaly Facility > Setup Project (Assets/Data/Archetype_*.asset).
/// Isi modelVariants di Inspector untuk mengganti bola grey-box dengan model 3D.
[CreateAssetMenu(menuName = "Facility/Unit Archetype", fileName = "Archetype_New")]
public class UnitArchetype : ScriptableObject
{
    public UnitType type;

    [Header("Profil")]
    [Tooltip("Foto profil di daftar unit. Import sebagai Sprite (2D and UI), disarankan persegi (mis. 128x128).")]
    public Sprite portrait;
    [Header("Tampilan")]
    [Tooltip("Kosong = pakai bola grey-box. Beberapa elemen = tiap unit dapat model acak (variasi).")]
    public GameObject[] modelVariants;
    [Tooltip("Opsional. Dipakai jika Animator di model belum punya controller.")]
    public RuntimeAnimatorController animatorOverride;
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    [Header("Animasi khusus")]
    [Tooltip("Dimainkan unit ini saat berada di Breeding cell (loop). Rig harus sama dengan model.")]
    public AnimationClip breedClip;
    [Tooltip("Clip yang dimainkan LEADER saat breeding dengan unit bertipe ini. Clip untuk unit ini sendiri tetap di Breed Clip.")]
    public AnimationClip leaderBreedClip;
    [Tooltip("true = saat breeding, Leader menghadap berlawanan dengan unit ini (berhadapan). false = searah.")]
    public bool leaderFacesMe = false;
    [Header("Breeding")]
    [Tooltip("Lama breeding (tick) saat unit ini jadi pasangan Leader. 0 = pakai GameConfig.BreedTicks.")]
    [Min(0)] public int breedTicks = 10;

    [Header("Stat (inklusif)")]
    public Vector2Int powerRange = new Vector2Int(1, 10);
    public Vector2Int fertilityRange = new Vector2Int(1, 10);
    public Vector2 moveSpeedRange = new Vector2(0.8f, 1.6f);

    [Header("Nama")]
    public string[] namePool;

    public int RollPower() { return Random.Range(powerRange.x, powerRange.y + 1); }
    public int RollFertility() { return Random.Range(fertilityRange.x, fertilityRange.y + 1); }
    public float RollMoveSpeed() { return Random.Range(moveSpeedRange.x, moveSpeedRange.y); }
    public float RollScale() { return Random.Range(scaleRange.x, scaleRange.y); }

    [System.Serializable]
    public class OffspringEntry
    {
        public UnitArchetype archetype;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Entri langka: bobotnya dikali bonus jika breeding memakai Leader.")]
        public bool rare;
    }


    

    // di dalam class UnitArchetype:
    [Header("Jenis")]
    public string displayName;      // mis. "Elf Healer" (untuk UI/log, opsional)

    [Header("Turunan (jika unit jenis ini jadi Breeder induk)")]
    [Tooltip("Kosong = anak acak seperti sebelumnya.")]
    public OffspringEntry[] offspring;
    public int RollVariant()
    {
        return (modelVariants == null || modelVariants.Length == 0) ? 0 : Random.Range(0, modelVariants.Length);
    }

    public GameObject GetModel(int variant)
    {
        if (modelVariants == null || modelVariants.Length == 0) return null;
        return modelVariants[Mathf.Clamp(variant, 0, modelVariants.Length - 1)];
    }

    public string RollName()
    {
        if (namePool == null || namePool.Length == 0) return type.ToString();
        return namePool[Random.Range(0, namePool.Length)];
    }

    public UnitArchetype RollOffspring(bool withLeader) { return RollFrom(offspring, withLeader); }

    public static UnitArchetype RollFrom(OffspringEntry[] table, bool boostRare)
    {
        if (table == null || table.Length == 0) return null;
        float total = 0f;
        foreach (var o in table) total += OffspringWeight(o, boostRare);
        if (total <= 0f) return null;

        float r = Random.value * total;
        UnitArchetype last = null;
        foreach (var o in table)
        {
            float w = OffspringWeight(o, boostRare);
            if (w <= 0f) continue;
            last = o.archetype;
            r -= w;
            if (r <= 0f) return o.archetype;
        }
        return last;
    }
    

    static float OffspringWeight(OffspringEntry o, bool withLeader)
    {
        if (o == null || o.archetype == null || o.archetype.type == UnitType.Leader) return 0f;
        return o.weight * (withLeader && o.rare ? GameConfig.LeaderRareMultiplier : 1f);
    }
    
}

