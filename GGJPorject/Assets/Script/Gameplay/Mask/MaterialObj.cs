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

    [Header("Logic Tree (Editor)")]
    [Tooltip("材质逻辑树：条件节点可挂子节点；条件不满足时仅跳过该分支。若此列表非空，运行时将优先使用树状逻辑，而不是 orderedComponents 链式逻辑。")]
    [SerializeField] private List<MaterialLogicNode> logicTreeRoots = new();

    public IReadOnlyList<MaterialLogicNode> LogicTreeRoots => logicTreeRoots;

    private bool HasLogicTree => logicTreeRoots != null && logicTreeRoots.Count > 0;

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
        if (HasLogicTree)
        {
            // 树状描述：使用 \t 表达层级；不做“跳出”（描述阶段不应截断）。
            // 注意：组件允许复用，因此这里不做去重，保证结构可读。
            BuildTreeDescription(sb, logicTreeRoots, depth: 0);
            return;
        }

        // 旧链式描述（兼容）
        var comps = GetOrderedComponentsRuntime();
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            if (c is IMaterialDescriptionProvider p) p.AppendDescription(sb);
        }
    }

    private static void BuildTreeDescription(StringBuilder sb, List<MaterialLogicNode> nodes, int depth)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;
            if (c != null && c is IMaterialDescriptionProvider p)
            {
                var tmp = new StringBuilder(128);
                p.AppendDescription(tmp);
                var text = tmp.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // 给每一行加上层级缩进
                    var lines = text.Replace("\r", "").Split('\n');
                    for (int li = 0; li < lines.Length; li++)
                    {
                        var line = lines[li];
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        for (int t = 0; t < depth; t++) sb.Append('\t');
                        sb.AppendLine(line);
                    }
                }
            }
            if (n.Children != null && n.Children.Count > 0)
            {
                BuildTreeDescription(sb, n.Children, depth + 1);
            }
        }
    }

    public void RunBindEffects(in BindContext context)
    {
        if (HasLogicTree)
        {
            var tctx = new MaterialTraverseContext(MaterialTraversePhase.Bind, null, FightSide.None, 0, 0);
            TraverseTreeBind(logicTreeRoots, in tctx, in context);
            return;
        }

        // 旧链式（兼容）
        var comps = GetOrderedComponentsRuntime();
        var tctx2 = new MaterialTraverseContext(MaterialTraversePhase.Bind, null, FightSide.None, 0, 0);
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx2)) break;
            if (c is IMaterialAutoInit init) init.Initialize(this);
            if (c is IMaterialBindEffect bind) bind.OnBind(in context);
        }
    }

    private void TraverseTreeBind(List<MaterialLogicNode> nodes, in MaterialTraverseContext tctx, in BindContext bindCtx)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            // 条件节点：break 仅影响该分支（跳过 Children），不影响兄弟节点
            if (c is IMaterialTraversalGate gate)
            {
                if (gate.ShouldBreak(in tctx)) continue;
            }

            if (c is IMaterialAutoInit init) init.Initialize(this);
            if (c is IMaterialBindEffect bind) bind.OnBind(in bindCtx);

            if (n.Children != null && n.Children.Count > 0)
            {
                TraverseTreeBind(n.Children, in tctx, in bindCtx);
            }
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


