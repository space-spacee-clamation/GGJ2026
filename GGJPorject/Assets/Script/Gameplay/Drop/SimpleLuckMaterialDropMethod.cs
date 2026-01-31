using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// V0 简单掉落方法：luck 影响品质概率（0~100），先抽品质再从该品质池按权重抽材料。
/// </summary>
[CreateAssetMenu(menuName = "GGJ2026/Drop/SimpleLuckDropMethod", fileName = "SimpleLuckDropMethod")]
public class SimpleLuckMaterialDropMethod : ScriptableObject, IMaterialDropMethod
{
    [Header("Quality Weights at Luck=0 (sum doesn't need to be 1)")]
    [Tooltip("Common/Uncommon/Rare/Epic/Legendary 的基础权重（luck=0）。")]
    [SerializeField] private float w0_common = 70f;
    [SerializeField] private float w0_uncommon = 20f;
    [SerializeField] private float w0_rare = 8f;
    [SerializeField] private float w0_epic = 2f;
    [SerializeField] private float w0_legendary = 0f;

    [Header("Quality Weights at Luck=100")]
    [Tooltip("Common/Uncommon/Rare/Epic/Legendary 的目标权重（luck=100）。")]
    [SerializeField] private float w1_common = 35f;
    [SerializeField] private float w1_uncommon = 30f;
    [SerializeField] private float w1_rare = 20f;
    [SerializeField] private float w1_epic = 12f;
    [SerializeField] private float w1_legendary = 3f;

    public IReadOnlyList<MaterialDropEntry> Roll(MaterialPool pool, int luck, int dropCount)
    {
        var results = new List<MaterialDropEntry>();
        if (pool == null) return results;

        luck = Mathf.Clamp(luck, 0, 100);
        dropCount = Mathf.Clamp(dropCount, 0, 100);
        if (dropCount <= 0) return results;

        // 先算出五档品质的权重（线性插值）
        float t = luck / 100f;
        float wc = Mathf.Lerp(w0_common, w1_common, t);
        float wu = Mathf.Lerp(w0_uncommon, w1_uncommon, t);
        float wr = Mathf.Lerp(w0_rare, w1_rare, t);
        float we = Mathf.Lerp(w0_epic, w1_epic, t);
        float wl = Mathf.Lerp(w0_legendary, w1_legendary, t);

        for (int i = 0; i < dropCount; i++)
        {
            var q = RollQuality(wc, wu, wr, we, wl);
            var prefab = RollPrefabByQuality(pool, q);
            if (prefab == null) continue;

            // 合并同一 prefab 的数量
            var existing = results.Find(x => x.MaterialPrefab == prefab);
            if (existing != null) existing.Count += 1;
            else results.Add(new MaterialDropEntry { MaterialPrefab = prefab, Count = 1 });
        }

        return results;
    }

    private static MaterialQuality RollQuality(float wc, float wu, float wr, float we, float wl)
    {
        float sum = Mathf.Max(0f, wc) + Mathf.Max(0f, wu) + Mathf.Max(0f, wr) + Mathf.Max(0f, we) + Mathf.Max(0f, wl);
        if (sum <= 0f) return MaterialQuality.Common;

        float r = Random.value * sum;
        r -= Mathf.Max(0f, wc); if (r <= 0f) return MaterialQuality.Common;
        r -= Mathf.Max(0f, wu); if (r <= 0f) return MaterialQuality.Uncommon;
        r -= Mathf.Max(0f, wr); if (r <= 0f) return MaterialQuality.Rare;
        r -= Mathf.Max(0f, we); if (r <= 0f) return MaterialQuality.Epic;
        return MaterialQuality.Legendary;
    }

    private static MaterialObj RollPrefabByQuality(MaterialPool pool, MaterialQuality q)
    {
        // 如果该品质为空，降级到更低品质（再不行就任意找一个非空池）
        for (int step = 0; step < 5; step++)
        {
            var qq = (MaterialQuality)Mathf.Clamp((int)q - step, 0, 4);
            var list = pool.GetList(qq);
            var picked = RollWeighted(list);
            if (picked != null) return picked;
        }

        // 任意非空
        return RollWeighted(pool.Common) ??
               RollWeighted(pool.Uncommon) ??
               RollWeighted(pool.Rare) ??
               RollWeighted(pool.Epic) ??
               RollWeighted(pool.Legendary);
    }

    private static MaterialObj RollWeighted(IReadOnlyList<MaterialPoolEntry> list)
    {
        if (list == null || list.Count == 0) return null;
        int total = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var w = list[i]?.Weight ?? 0;
            if (w > 0) total += w;
        }
        if (total <= 0) return null;

        int r = Random.Range(0, total);
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.Weight <= 0 || e.MaterialPrefab == null) continue;
            r -= e.Weight;
            if (r < 0) return e.MaterialPrefab;
        }
        return null;
    }
}



