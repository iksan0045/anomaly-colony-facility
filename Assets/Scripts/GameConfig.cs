using UnityEngine;

/// Semua angka balancing ada di sini. Ubah di satu tempat.
public static class GameConfig
{
    // Grid & tick
    public const int GridSize = 8;
    public const float TickSeconds = 1f;

    // Resource
    public const int StartEnergy = 30;
    public const int StartMaterials = 20;
    public const int BaseMaxResource = 50;
    public const int StorageBonus = 25;

    public const int RaidCooldownTicks = 20;
    

    // Unit
    public const int BaseMaxUnits = 12;
    public const int ControlUnitBonus = 2;
    public const int MoodStart = 60;
    public const int MoodDecayPerTick = 2;
    public const int MoodDetentionPerTick = 5;
    public const int MoodLowThreshold = 20;
    public const int RationPerTick = 1;
    public const int StarvationMoodPenalty = 2;

    // Breeding
    public const int BreedTicks = 10;

    public const int RaidBasePower = 3;
    public const int RaidPowerPerRaid = 2;
    public const int FighterPowerLoss = 2;

    public const float BreedChanceBreeder = 0.6f;   // pasangan Breeder
    public const float BreedChanceOther   = 0.2f;   // pasangan Producer/Fighter

    // di ColorOf(UnitType), tambah sebelum default:

    // Cell yang terbuka dari awal; CONTROL membuka sisanya sesuai urutan
    public static readonly CellType[] StartUnlocked = { CellType.Detention, CellType.Production, CellType.Control };
    public static readonly CellType[] UnlockOrder = { CellType.Storage, CellType.Breeding };

    public static Vector2Int DefaultSize(CellType t)
    {
        switch (t)
        {
            case CellType.Production: return new Vector2Int(1, 2);
            case CellType.Breeding: return new Vector2Int(2, 2);
            default: return new Vector2Int(1, 1);
        }
    }

    public static int Capacity(CellType t)
    {
        switch (t)
        {
            case CellType.Detention: return 1;
            case CellType.Production: return 2;
            case CellType.Breeding: return 2;
            default: return 0;
        }
    }

    public static int EnergyCost(CellType t)
    {
        switch (t)
        {
            case CellType.Detention: return 5;
            case CellType.Production: return 10;
            case CellType.Breeding: return 20;
            case CellType.Storage: return 10;
            case CellType.Control: return 15;
            default: return 0;
        }
    }

    public static string Label(CellType t)
    {
        return t.ToString();
    }

    public static Color ColorOf(CellType t)
    {
        switch (t)
        {
            case CellType.Detention: return new Color(0.20f, 0.40f, 0.90f);
            case CellType.Production: return new Color(0.20f, 0.75f, 0.30f);
            case CellType.Breeding: return new Color(0.60f, 0.30f, 0.80f);
            case CellType.Storage: return new Color(0.95f, 0.85f, 0.20f);
            default: return new Color(0.85f, 0.20f, 0.20f);
        }
    }

    public static Color ColorOf(UnitType t)
    {
        switch (t)
        {
            case UnitType.Producer: return Color.white;
            case UnitType.Fighter: return new Color(1f, 0.55f, 0.10f);
            case UnitType.Leader: return new Color(0.3f, 0.9f, 1f);
            default: return new Color(1f, 0.50f, 0.70f);
        }
    }
}
