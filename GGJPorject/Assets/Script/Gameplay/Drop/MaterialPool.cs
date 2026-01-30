using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MaterialPoolEntry
{
    public MaterialObj MaterialPrefab;
    [Min(1)] public int Weight = 1;
}

/// <summary>
/// 材料池：按品质分组的材料列表（可选权重）。
/// </summary>
[CreateAssetMenu(menuName = "GGJ2026/Drop/MaterialPool", fileName = "MaterialPool")]
public class MaterialPool : ScriptableObject
{
    public List<MaterialPoolEntry> Common = new();
    public List<MaterialPoolEntry> Uncommon = new();
    public List<MaterialPoolEntry> Rare = new();
    public List<MaterialPoolEntry> Epic = new();
    public List<MaterialPoolEntry> Legendary = new();

    public IReadOnlyList<MaterialPoolEntry> GetList(MaterialQuality q)
    {
        return q switch
        {
            MaterialQuality.Common => Common,
            MaterialQuality.Uncommon => Uncommon,
            MaterialQuality.Rare => Rare,
            MaterialQuality.Epic => Epic,
            MaterialQuality.Legendary => Legendary,
            _ => Common
        };
    }
}


