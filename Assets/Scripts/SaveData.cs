using System;
using System.Collections.Generic;

/// Struktur data save (JsonUtility). Hanya data murni, tanpa referensi objek Unity.
/// Referensi asset (SpecialUnit) disimpan lewat nama asset.

[Serializable]
public class UnitSave
{
    public int id;
    public int type;            // UnitType
    public int power;
    public int mood;
    public int fertility;
    public int variant;
    public float moveSpeed;
    public float scale;
    public string displayName;
    public string specialName;  // nama asset SpecialUnit, kosong = unit biasa
    public string archetypeName;
    public int homeCell;
}

[Serializable]
public class CellSave
{
    public int type;            // CellType
    public int x, y;            // origin
    public int w, h;            // ukuran (sudah termasuk rotasi)
    public int breedProgress;   // hanya Breeding
    public int[] unitIds;       // urutan = urutan slot
}

[Serializable]
public class SaveData
{
    public int version;
    public string savedAt;

    public int tick;
    public int energy;
    public int materials;
    public bool starving;

    public int storageCount;
    public int controlCount;
    public int bonusUnits;
    public int[] unlocked;      // CellType yang terbuka

    public int nextUnitId;
    public int raidCount;
    public int raidCooldown;

    public List<UnitSave> units = new List<UnitSave>();
    public List<CellSave> cells = new List<CellSave>();
    public string[] log;
    public string raidTarget;
    public int salvage;
    public RaidSave raid;
}
