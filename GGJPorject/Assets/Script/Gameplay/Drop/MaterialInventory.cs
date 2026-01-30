using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 材料库存（Jam 版：直接存 MaterialObj 实例列表）。
/// - 掉落时：Instantiate 出 MaterialObj 实例，ResetInventoryShelfLife 后加入库存
/// - “制造面具回合结束”（StartBattle 前）：对库存内每个实例调用 TickInventory()，过期则 Destroy 并移除
/// - 绑定到面具成功后：从库存移除该实例（它已经成为面具一部分，不再参与库存结算）
/// </summary>
[Serializable]
public sealed class MaterialInventory
{
    [SerializeField] private List<MaterialObj> items = new();
    public IReadOnlyList<MaterialObj> Items => items;

    public void Add(MaterialObj instance)
    {
        if (instance == null) return;
        if (!items.Contains(instance)) items.Add(instance);
    }

    /// <summary>
    /// 制造回合结束：对库存内每个材料 TickInventory()，过期则销毁并移除。
    /// </summary>
    public void TickEndOfMakePhase(Transform inventoryRoot = null)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var m = items[i];
            if (m == null)
            {
                items.RemoveAt(i);
                continue;
            }

            // 仍在库存根节点下的才算库存（保险）
            if (inventoryRoot != null && m.transform.parent != inventoryRoot) continue;

            if (m.TickInventory())
            {
                UnityEngine.Object.Destroy(m.gameObject);
                items.RemoveAt(i);
            }
        }
    }

    public bool TryGetItemsSortedByExpiry(out List<MaterialObj> sorted)
    {
        sorted = null;
        if (items == null || items.Count == 0) return false;

        var list = new List<MaterialObj>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            var m = items[i];
            if (m == null) continue;
            if (m.RemainingShelfLifeTurns <= 0) continue;
            list.Add(m);
        }

        if (list.Count == 0) return false;

        list.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.RemainingShelfLifeTurns.CompareTo(b.RemainingShelfLifeTurns);
        });

        sorted = list;
        return true;
    }

    public void Remove(MaterialObj instance)
    {
        if (instance == null) return;
        items.Remove(instance);
    }
}


