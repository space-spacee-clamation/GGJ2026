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
        bool allow = IsPhaseMatch(context.Phase, Phase);
        if (Invert) allow = !allow;
        return !allow;
    }

    private static bool IsPhaseMatch(MaterialTraversePhase actual, MaterialTraversePhase configured)
    {
        if (actual == configured) return true;

        // Legacy 兼容：
        // - 旧配置 AttackModify 视为“玩家/敌人攻击前”
        // - 旧配置 DamageApplied 视为“玩家/敌人攻击后”
        if (configured == MaterialTraversePhase.AttackModify)
        {
            return actual == MaterialTraversePhase.PlayerAttackBefore || actual == MaterialTraversePhase.EnemyAttackBefore;
        }
        if (configured == MaterialTraversePhase.DamageApplied)
        {
            return actual == MaterialTraversePhase.PlayerAttackAfter || actual == MaterialTraversePhase.EnemyAttackAfter;
        }

        // 反向兼容：如果运行时仍在某些节点里用 Legacy 阶段构造上下文，也应当能命中“新阶段 Gate”
        if (actual == MaterialTraversePhase.AttackModify)
        {
            return configured == MaterialTraversePhase.PlayerAttackBefore || configured == MaterialTraversePhase.EnemyAttackBefore;
        }
        if (actual == MaterialTraversePhase.DamageApplied)
        {
            return configured == MaterialTraversePhase.PlayerAttackAfter || configured == MaterialTraversePhase.EnemyAttackAfter;
        }

        return false;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var name = Phase switch
        {
            MaterialTraversePhase.Bind => "绑定时",
            MaterialTraversePhase.BattleStart => "战斗开始时",
            MaterialTraversePhase.AttackModify => "攻击前（Legacy：等价于玩家攻击前/敌人攻击前）",
            MaterialTraversePhase.DamageApplied => "攻击后（Legacy：等价于玩家攻击后/敌人攻击后）",
            MaterialTraversePhase.PlayerAttackBefore => "玩家攻击前",
            MaterialTraversePhase.PlayerAttackAfter => "玩家攻击后",
            MaterialTraversePhase.EnemyAttackBefore => "敌人攻击前",
            MaterialTraversePhase.EnemyAttackAfter => "敌人攻击后",
            MaterialTraversePhase.BattleEnd => "战斗结束时",
            MaterialTraversePhase.PersistentGrowth => "持久成长结算时",
            _ => Phase.ToString()
        };

        sb.Append(Invert ? $"{name}不执行后续效果" : $"{name}：");
    }
}


