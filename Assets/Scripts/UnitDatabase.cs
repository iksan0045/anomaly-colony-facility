using UnityEngine;
using System.Collections.Generic;

/// Kumpulan UnitArchetype. Dirujuk GameManager.database.
[CreateAssetMenu(menuName = "Facility/Unit Database", fileName = "UnitDatabase")]
public class UnitDatabase : ScriptableObject
{
    public UnitArchetype[] archetypes;

    [Header("Unit spesial")]
    public SpecialUnit[] specials;
    [Range(0f, 1f)] public float specialChance = 0.1f;  
    [Header("Raid")]
    public RaidTarget[] raidTargets;

    public UnitArchetype Get(UnitType t)
    {
        if (archetypes == null) return null;
        foreach (var a in archetypes)
            if (a != null && a.type == t) return a;
        return null;
    }

    public SpecialUnit RollSpecial(List<UnitData> owned)
    {
        if (specials == null || specials.Length == 0 || Random.value > specialChance) return null;

        var pool = new List<SpecialUnit>();
        foreach (var s in specials)
        {
            if (s == null || s.type == UnitType.Leader) continue;
            if (s.unique && owned.Exists(u => u.special == s)) continue;   // unik: sudah dimiliki
            pool.Add(s);
        }
        return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
    }
}
