using System.Text;
using UnityEngine;

/// <summary>
/// 逻辑节点：负责“注入接口/触发”，并把统一上下文转发给纯效果器（IMaterialEffect / IMaterialAttackInfoEffect）。
/// 该组件应当作为树节点被引用；纯效果器本身不应直接进入树。
/// </summary>
public sealed class Node_Effect : MonoBehaviour,
    IMaterialLogicNode,
    IMaterialBindEffect,
    IMaterialBattleStartEffect,
    IMaterialBattleEndEffect,
    IMaterialDamageAppliedEffect,
    IPersistentGrowthProvider,
    IAttackInfoModifier,
    IMaterialDescriptionProvider
{
    [Tooltip("要执行的纯效果器（必须挂在同一个 MaterialObj 上）。")]
    public MonoBehaviour Effect;

    public void OnBind(in MaterialVommandeTreeContext context)
    {
        TryExecute(in context);
    }

    public void OnBattleStart(FightContext context)
    {
        var ctx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.BattleStart,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: context,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TryExecute(in ctx);
    }

    public void OnBattleEnd(FightContext context)
    {
        var ctx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.BattleEnd,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: context != null ? context.BattleActionCount : 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TryExecute(in ctx);
    }

    public void OnDamageApplied(FightContext context, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage)
    {
        var ctx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.DamageApplied,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: attackerSide,
            defenderSide: defenderSide,
            actionNumber: context != null ? context.CurrentActionNumber : 0,
            attackerAttackNumber: context != null ? context.CurrentAttackerAttackNumber : 0,
            attackInfo: info,
            damage: damage,
            player: null,
            growthDelta: null
        );
        TryExecute(in ctx);
    }

    public void OnCollectPersistentGrowth(Player player, PlayerGrowthDelta delta, FightContext battleContext)
    {
        var ctx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.PersistentGrowth,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: battleContext != null ? battleContext.BattleActionCount : 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: delta
        );
        TryExecute(in ctx);
    }

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (Effect == null) return;

        // 攻击前上下文：把当前 info 快照塞进 ctx（便于 gate/描述/日志），真正修改走 ref 参数
        // 注意：这里使用“新阶段（玩家/敌人攻击前）”，同时保持 Gate_Phase 对 Legacy 的兼容映射。
        var phase = context != null && context.CurrentAttackerSide == FightSide.Enemy
            ? MaterialTraversePhase.EnemyAttackBefore
            : MaterialTraversePhase.PlayerAttackBefore;

        var ctx = new MaterialVommandeTreeContext(
            phase,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: context,
            side: context != null ? context.CurrentAttackerSide : FightSide.None,
            defenderSide: context != null
                ? (context.CurrentAttackerSide == FightSide.Player ? FightSide.Enemy : FightSide.Player)
                : FightSide.None,
            actionNumber: context != null ? context.CurrentActionNumber : 0,
            attackerAttackNumber: context != null ? context.CurrentAttackerAttackNumber : 0,
            attackInfo: info,
            damage: 0f,
            player: null,
            growthDelta: null
        );

        if (Effect is IMaterialAttackInfoEffect atk)
        {
            atk.Modify(ref info, in ctx);
            return;
        }

        // 允许在“攻击前”触发不修改 AttackInfo 的效果（例如播特效/SFX/计数等）
        // 这能让 Gate_Phase.PlayerAttackBefore 下挂普通 IMaterialEffect 正常工作。
        if (Effect is IMaterialEffect e)
        {
            e.Execute(in ctx);
        }
    }

    private void TryExecute(in MaterialVommandeTreeContext context)
    {
        if (Effect == null) return;
        if (Effect is IMaterialEffect e)
        {
            e.Execute(in context);
        }
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        if (Effect is IMaterialDescriptionProvider p)
        {
            p.AppendDescription(sb);
            return;
        }
        if (Effect != null)
        {
            sb.Append($"执行效果：{Effect.GetType().Name}");
        }
        else
        {
            sb.Append("（未绑定效果器）");
        }
    }
}


