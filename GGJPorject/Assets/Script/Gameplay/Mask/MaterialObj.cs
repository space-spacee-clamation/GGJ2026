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
    public enum MaterialType
    {
        Cost = 0,
        Attack = 1,
        Survival = 2,
        Special = 3,
    }

    [Header("Base Data")]
    [Min(0)] public int Id = 0;
    [Tooltip("材质名（用于 UI 与保存命名）。")]
    public string DisplayName;

    [Tooltip("材质类型。")]
    public MaterialType Type = MaterialType.Attack;

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

    [Header("Logic Tree (Editor)")]
    [Tooltip("材质逻辑树：条件节点可挂子节点；条件不满足时仅跳过该分支。运行时只使用树状逻辑（不再兼容旧链式）。")]
    [SerializeField] private List<MaterialLogicNode> logicTreeRoots = new();

    public IReadOnlyList<MaterialLogicNode> LogicTreeRoots => logicTreeRoots;

    private bool HasLogicTree => logicTreeRoots != null && logicTreeRoots.Count > 0;

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
        if (!HasLogicTree)
        {
            sb.Append("（该材质未配置逻辑树）");
            return;
        }

        // 树状描述：逻辑节点用红色标记，组件间用逗号分隔，每个 tree（根节点分支）结束时用句号和换行。
        // 注意：组件允许复用，因此这里不做去重，保证结构可读。
        for (int i = 0; i < logicTreeRoots.Count; i++)
        {
            var root = logicTreeRoots[i];
            if (root == null) continue;
            
            // 遍历当前根节点及其所有子节点
            BuildTreeDescriptionForNode(sb, root);
            
            // 每个 tree 结束时添加句号和换行
            if (sb.Length > 0)
            {
                // 检查末尾是否是"。\n"
                bool endsWithPeriodAndNewline = sb.Length >= 2 && 
                    sb[sb.Length - 1] == '\n' && 
                    sb[sb.Length - 2] == '。';
                
                if (!endsWithPeriodAndNewline)
                {
                    // 如果末尾是句号但没有换行，先移除句号再添加"。\n"
                    if (sb.Length > 0 && sb[sb.Length - 1] == '。')
                    {
                        sb.Length--; // 移除句号
                    }
                    sb.Append("。\n");
                }
            }
        }
    }

    private static void BuildTreeDescriptionForNode(StringBuilder sb, MaterialLogicNode node)
    {
        if (node == null) return;
        
        var c = node.Component;
        if (c != null && c is IMaterialDescriptionProvider p)
        {
            bool isLogicNode = c is IMaterialLogicNode;
            bool notNodeEffect = c as Node_Effect ==null;
            if (isLogicNode && notNodeEffect)
            {
                sb.Append($"<color=red>");
                p.AppendDescription(sb);
                sb.Append($"</color>");
            }
            else{
                p.AppendDescription(sb);
                char lastChar = sb[sb.Length - 1];
                if (lastChar != '，' && lastChar != '。' && lastChar != '\n')
                {
                    sb.Append(" ");
                }
            }
        }
        
        // 递归处理子节点
        if (node.Children != null && node.Children.Count > 0)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                BuildTreeDescriptionForNode(sb, node.Children[i]);
            }
        }
    }

    public void RunBindEffects(in BindContext context)
    {
        if (!HasLogicTree)
        {
            Debug.LogWarning($"[MaterialObj] {name} 未配置 logicTreeRoots，无法执行 Bind。", this);
            return;
        }

        var tctx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.Bind,
            mask: context.Mask,
            maskMaterials: context.Materials,
            onMaterialBound: context.OnMaterialBound,
            fight: null,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TraverseTreeBind(logicTreeRoots, in tctx);
    }

    private void TraverseTreeBind(List<MaterialLogicNode> nodes, in MaterialVommandeTreeContext tctx)
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
            if (c is IMaterialBindEffect bind) bind.OnBind(in tctx);

            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTreeBind(n.Children, in tctx);
            }
        }
    }

    public void InjectBattle(FightContext context)
    {
        if (context == null) return;
        if (!HasLogicTree)
        {
            Debug.LogWarning($"[MaterialObj] {name} 未配置 logicTreeRoots，无法注入战斗。", this);
            return;
        }

        // 每个材料注入一个“运行时执行器”，它会遍历逻辑树并触发对应阶段的逻辑节点
        var runner = new MaterialRuntimeRunner(this);
        context.AddFightComponent(runner);
        context.PlayerAttackProcessor.Add(runner);
        context.EnemyAttackProcessor.Add(runner);
    }
}


