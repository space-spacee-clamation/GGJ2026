using System.Text;
using UnityEngine;

/// <summary>
/// Wait 节点：进入该 Gate 会计数；每累计到 X 次，放行 1 次并重置计数。
/// 用途：替代“每X次行动/前X次攻击”等依赖外部计数的 Gate，改为本节点自身计数（Jam 简化）。
///
/// 约定：
/// - 在 BattleStart 时自动 Reset（避免跨战斗污染）
/// - Description 阶段不拦截（避免截断文案）
/// </summary>
[MaterialCnMeta("等待每X次触发", "等待 计数 每X 次触发 节点")]
public sealed class Gate_WaitEveryX : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)] public int EveryX = 2;
    public bool Invert = false;

    [SerializeField, Min(0)] private int _counter = 0;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;

        // 每场战斗开始重置一次（Bind 阶段也可能使用，但 Jam 默认按战斗维度重置即可）
        if (context.Phase == MaterialTraversePhase.BattleStart)
        {
            _counter = 0;
            return false;
        }

        if (EveryX <= 0) return false;

        _counter += 1;
        bool allow = (_counter >= EveryX);
        if (allow) _counter = 0;

        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.Append(Invert
            ? $"等待每 {EveryX} 次（本节点计数）不执行后续效果"
            : $"等待每 {EveryX} 次（本节点计数）执行后续效果");
    }
}


