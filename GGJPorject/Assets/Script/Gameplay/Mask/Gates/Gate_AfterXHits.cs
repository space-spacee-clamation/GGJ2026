using System.Text;
using UnityEngine;

/// <summary>
/// Gate：某一方“受击次数”达到 X 次后才允许执行后续效果（否则跳过该分支）。
/// - 受击次数来源：FightContext 的攻击计数（受击方=对方攻击次数）
/// - 在 DamageApplied/AttackModify 等“当前动作存在受击方”阶段，会把本次受击计入判定（使用 context.AttackerAttackNumber）
/// </summary>
[MaterialCnMeta("受击X次后触发", "受击 挨打 被攻击 X次 后触发 条件")]
public sealed class Gate_AfterXHits : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Tooltip("要统计受击次数的一方。")]
    public FightSide Target = FightSide.Player;

    [Min(1)]
    public int AfterXHits = 2;

    [Tooltip("取反：受击次数达到 X 次后不执行（未达到时执行）。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;
        if (AfterXHits <= 0) return false;
        if (context.Fight == null) return true; // 没有战斗上下文无法判断：保守跳过

        int received = GetReceivedHitsSoFar(in context);
        bool allow = received >= AfterXHits;
        if (Invert) allow = !allow;
        return !allow;
    }

    private int GetReceivedHitsSoFar(in MaterialVommandeTreeContext context)
    {
        // 基础：受击次数 = “对方攻击次数”
        int baseReceived = 0;
        if (Target == FightSide.Player) baseReceived = context.Fight.EnemyAttackCount;
        else if (Target == FightSide.Enemy) baseReceived = context.Fight.PlayerAttackCount;

        // 若当前动作正好击中 Target，则把“本次攻击序号”计入（因为 FightManager 在 RaiseDamageApplied 前还没累加计数）
        bool isCurrentHitTarget = (context.DefenderSide == Target);
        if (isCurrentHitTarget && context.AttackerAttackNumber > baseReceived)
        {
            baseReceived = context.AttackerAttackNumber;
        }

        return Mathf.Max(0, baseReceived);
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var who = Target == FightSide.Enemy ? "敌人" : "玩家";
        sb.AppendLine(Invert
            ? $"{who} 受击达到 {AfterXHits} 次后不执行后续效果"
            : $"{who} 受击达到 {AfterXHits} 次后执行后续效果");
    }
}


