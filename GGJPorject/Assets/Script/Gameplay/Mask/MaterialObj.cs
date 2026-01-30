using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 材料对象（挂在“材料 Prefab”根节点上）。
/// 在 Awake 中自动 GetComponents 并缓存，后续由 MaskObj 直接调用 MaterialObj 完成：
/// - 绑定阶段即时效果
/// - 进入战斗前注入 FightContext（回调/处理链）
/// </summary>
public class MaterialObj : MonoBehaviour
{
    [Header("Base Data")]
    [Min(0)] public int Id = 0;
    [Tooltip("材质名（用于 UI 与保存命名）。")]
    public string DisplayName;

    [Min(0)] public int ManaCost = 0;

    public MaterialQuality Quality = MaterialQuality.Common;

    [Tooltip("用于 UI 显示的基础图像。")]
    public Sprite BaseSprite;

    [Header("Inventory")]
    [Tooltip("保质期（回合）：从获取开始算，经过多少个“制造面具回合结束”后会在材料库中销毁。")]
    [Min(1)] public int ShelfLifeTurns = 1;

    [SerializeField, Min(0)] private int remainingShelfLifeTurns = 0;
    public int RemainingShelfLifeTurns => remainingShelfLifeTurns;

    /// <summary>
    /// 入库时调用：把 RemainingShelfLifeTurns 重置为 ShelfLifeTurns。
    /// </summary>
    public void ResetInventoryShelfLife()
    {
        remainingShelfLifeTurns = Mathf.Max(1, ShelfLifeTurns);
    }

    /// <summary>
    /// 库存回合结算：Remaining--，返回是否过期（<=0）。
    /// 注意：仅库存中的材料会被调用；已绑定到面具的材料不参与库存结算。
    /// </summary>
    public bool TickInventory()
    {
        remainingShelfLifeTurns = Mathf.Max(0, remainingShelfLifeTurns - 1);
        return remainingShelfLifeTurns <= 0;
    }

    [Header("Ordered Components (Editor)")]
    [Tooltip("材质组件执行顺序（由编辑器/材质编辑器维护）。用于跳出 Gate 与顺序触发。")]
    [SerializeField] private List<MonoBehaviour> orderedComponents = new();

    public IReadOnlyList<MonoBehaviour> OrderedComponents => orderedComponents;

    private IReadOnlyList<MonoBehaviour> GetOrderedComponentsRuntime()
    {
        // Jam 容错：老 prefab 未配置顺序时，退化为 GetComponents 顺序
        if (orderedComponents == null || orderedComponents.Count == 0)
        {
            var list = new List<MonoBehaviour>();
            var bs = GetComponents<MonoBehaviour>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                if (bs[i] is MaterialObj) continue;
                list.Add(bs[i]);
            }
            return list;
        }

        // 清理空引用
        for (int i = orderedComponents.Count - 1; i >= 0; i--)
        {
            if (orderedComponents[i] == null) orderedComponents.RemoveAt(i);
        }
        return orderedComponents;
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Ordered Components From Current")]
    private void EditorRebuildOrderedComponentsFromCurrent()
    {
        orderedComponents ??= new List<MonoBehaviour>();
        orderedComponents.Clear();
        var bs = GetComponents<MonoBehaviour>();
        for (int i = 0; i < bs.Length; i++)
        {
            if (bs[i] == null) continue;
            if (bs[i] is MaterialObj) continue;
            orderedComponents.Add(bs[i]);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void EditorAppendOrderedComponent(MonoBehaviour comp)
    {
        if (comp == null || comp is MaterialObj) return;
        orderedComponents ??= new List<MonoBehaviour>();
        if (!orderedComponents.Contains(comp)) orderedComponents.Add(comp);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>
    /// 按“配置好的顺序”生成描述：依次调用 IMaterialDescriptionProvider；遇到 Gate 跳出则提前结束。
    /// </summary>
    public string BuildDescription()
    {
        var sb = new StringBuilder(128);
        BuildDescription(sb);
        return sb.ToString();
    }

    public void BuildDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var comps = GetOrderedComponentsRuntime();
        var ctx = new MaterialTraverseContext(MaterialTraversePhase.Description, null, FightSide.None, 0, 0);
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            // 描述阶段不做“跳出”，避免 Gate 把后续词条描述截断；
            // Gate 自己可以在 AppendDescription 里输出“战斗开始时/战斗结束时”等前缀文案。
            if (c is IMaterialDescriptionProvider p) p.AppendDescription(sb);
        }
    }

    public void RunBindEffects(in BindContext context)
    {
        var comps = GetOrderedComponentsRuntime();
        var tctx = new MaterialTraverseContext(MaterialTraversePhase.Bind, null, FightSide.None, 0, 0);
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx)) break;
            if (c is IMaterialAutoInit init) init.Initialize(this);
            if (c is IMaterialBindEffect bind) bind.OnBind(in context);
        }
    }

    public void InjectBattle(FightContext context)
    {
        if (context == null) return;

        // 每个材料注入一个“运行时执行器”，它会按 orderedComponents 顺序执行，并支持 Gate 跳出
        var runner = new MaterialRuntimeRunner(this);
        context.AddFightComponent(runner);
        context.PlayerAttackProcessor.Add(runner);
        context.EnemyAttackProcessor.Add(runner);
    }
}


