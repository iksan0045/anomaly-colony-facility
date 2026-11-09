using UnityEngine;

/// Kumpulan UnitArchetype. Dirujuk GameManager.database.
[CreateAssetMenu(menuName = "Facility/Unit Database", fileName = "UnitDatabase")]
public class UnitDatabase : ScriptableObject
{
    public UnitArchetype[] archetypes;

    public UnitArchetype Get(UnitType t)
    {
        if (archetypes == null) return null;
        foreach (var a in archetypes)
            if (a != null && a.type == t) return a;
        return null;
    }
}
