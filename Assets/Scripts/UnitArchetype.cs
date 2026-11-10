using UnityEngine;

/// Template satu jenis unit: model 3D, rentang stat, nama. Satu asset per UnitType.
/// Dibuat otomatis oleh Tools > Anomaly Facility > Setup Project (Assets/Data/Archetype_*.asset).
/// Isi modelVariants di Inspector untuk mengganti bola grey-box dengan model 3D.
[CreateAssetMenu(menuName = "Facility/Unit Archetype", fileName = "Archetype_New")]
public class UnitArchetype : ScriptableObject
{
    public UnitType type;

    [Header("Tampilan")]
    [Tooltip("Kosong = pakai bola grey-box. Beberapa elemen = tiap unit dapat model acak (variasi).")]
    public GameObject[] modelVariants;
    [Tooltip("Opsional. Dipakai jika Animator di model belum punya controller.")]
    public RuntimeAnimatorController animatorOverride;
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    [Header("Animasi khusus")]
    [Tooltip("Dimainkan unit ini saat berada di Breeding cell (loop). Rig harus sama dengan model.")]
    public AnimationClip breedClip;
    public AnimationClip leaderClip;

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
}
