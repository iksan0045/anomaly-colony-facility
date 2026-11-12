using UnityEngine;
using static UnitArchetype;

public enum AnomalyClass { Standard, HighMobility, Armored, Psychic, Unstable }

/// Satu lokasi/target raid (hutan, laboratorium, dst). Satu asset per target.
/// Buat lewat Create > Facility > Raid Target, lalu daftarkan di UnitDatabase.raidTargets.
[CreateAssetMenu(menuName = "Facility/Raid Target", fileName = "Raid_New")]
public class RaidTarget : ScriptableObject
{
    [Header("Display")]
    public string displayName = "Unknown Site";
    [Tooltip("Import sebagai Texture Type: Sprite (2D and UI). Kosong = kotak abu-abu.")]
    public Sprite image;
    [TextArea(2, 4)] public string briefing = "Detected: anomalous activity.";
    public AnomalyClass anomalyClass = AnomalyClass.Standard;

    [Header("Appearance")]
    [Tooltip("Target baru muncul setelah raid ke-N.")]
    [Min(0)] public int minRaidCount = 0;
    [Tooltip("Bobot saat target dipilih acak.")]
    [Min(0.01f)] public float weight = 1f;

    [Header("Difficulty")]
    [Min(0.1f)] public float powerMultiplier = 1f;

    [Header("Rewards")]
    [Tooltip("Tipe unit yang mungkin didapat. Kosong = acak. Jangan isi Leader.")]
    public UnitType[] rewardTypes;
    [Tooltip("Tambahan power untuk unit biasa yang didapat.")]
    public int powerBonus = 0;
    [Tooltip("Unit spesial yang bisa didapat dari target ini. Tetap daftarkan juga di UnitDatabase.specials (dipakai save/load).")]
    public SpecialUnit[] specialDrops;
    [Tooltip("Peluang dasar dapat unit spesial saat raid sukses. Perfect x2, Safe protocol x1.5.")]
    public OffspringEntry[] rewardPool;
    [Range(0f, 1f)] public float specialChance = 0.1f;

    public UnitType RollRewardType()
    {
        if (rewardTypes != null && rewardTypes.Length > 0)
        {
            var t = rewardTypes[Random.Range(0, rewardTypes.Length)];
            if (t != UnitType.Leader) return t;
        }
        return GameManager.RandomType();
    }

    public string ClassHint()
    {
        switch (anomalyClass)
        {
            case AnomalyClass.HighMobility: return "High Mobility: Fighters below power 5 count at half strength.";
            case AnomalyClass.Armored:      return "Armored: enemy power +25%, but captured units gain +1 power.";
            case AnomalyClass.Psychic:      return "Psychic: low-mood Fighters lose up to 40% of their strength.";
            case AnomalyClass.Unstable:     return "Unstable: higher chance of both Perfect Capture and Catastrophe.";
            default:                        return "No special properties.";
        }
    }

    public static RaidTarget CreateDefault()
    {
        var t = CreateInstance<RaidTarget>();
        t.name = "Unknown Site";
        t.displayName = "Unknown Site";
        return t;
    }
}
