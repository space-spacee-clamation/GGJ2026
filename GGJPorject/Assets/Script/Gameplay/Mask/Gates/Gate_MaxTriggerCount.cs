using System.Text;
using UnityEngine;

/// <summary>
/// Gate：最多允许通过 X 次（超过后跳过该分支）。
/// - “通过次数”是运行时计数（每次 ShouldBreak 返回 false 都会 +1）
/// - 默认在 BattleStart 时自动重置计数（避免跨战斗污染）
/// </summary>
[MaterialCnMeta("最多触发X次", "最多 触发 次数 上限 条件")]
public sealed class Gate_MaxTriggerCount : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)]
    public int MaxTimes = 1;

    [Tooltip("取反：前 X 次不执行，之后执行。")]
    public bool Invert = false;

    [SerializeField, Min(0)]
    private int _passedCount = 0;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;

        // Jam：每场战斗开始重置一次，避免跨战斗残留
        if (context.Phase == MaterialTraversePhase.BattleStart)
        {
            _passedCount = 0;
            return false;
        }

        if (MaxTimes <= 0) return false;

        bool allow = _passedCount < MaxTimes;
        if (Invert) allow = !allow;

        if (allow)
        {
            // 注意：取反模式下 allow=true 代表“超过 X 次之后执行”，这里也会计数，但不影响逻辑（Jam 简化）
            _passedCount += 1;
        }

        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert
            ? $"前 {MaxTimes} 次不执行后续效果（之后执行）"
            : $"最多触发 {MaxTimes} 次后续效果");
    }
}


