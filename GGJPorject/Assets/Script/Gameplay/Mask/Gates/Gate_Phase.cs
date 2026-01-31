using System.Text;
using UnityEngine;

/// <summary>
/// 通用阶段 Gate：用于把“战斗/绑定/结算”等回调显式做成逻辑树节点。
/// 约定：
/// - 描述阶段不跳出（避免截断文案）
/// - 其它阶段：仅当 context.Phase == Phase 时允许继续执行本分支
/// </summary>
public sealed class Gate_Phase : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Tooltip("当遍历上下文处于该阶段时，允许执行该分支。")]
    public MaterialTraversePhase Phase = MaterialTraversePhase.BattleStart;

    [Tooltip("取反：当处于该阶段时不执行（其它阶段执行）。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;
        bool allow = context.Phase == Phase;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var name = Phase switch
        {
            MaterialTraversePhase.Bind => "绑定时",
            MaterialTraversePhase.BattleStart => "战斗开始时",
            MaterialTraversePhase.AttackModify => "行动前（AttackModify）",
            MaterialTraversePhase.DamageApplied => "行动后（DamageApplied）",
            MaterialTraversePhase.BattleEnd => "战斗结束时",
            MaterialTraversePhase.PersistentGrowth => "持久成长结算时",
            _ => Phase.ToString()
        };

        sb.AppendLine(Invert ? $"{name}不执行后续效果" : $"{name}：");
    }
}


