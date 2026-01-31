using System.Text;
using UnityEngine;

/// <summary>
/// 统一“加属性”效果器：
/// - ApplyAsPersistentGrowth=false：战斗开始时把数值加到 BattleStats（CombatantRuntime）
/// - ApplyAsPersistentGrowth=true：战斗结束结算时把数值写入 PlayerGrowthDelta（持久成长）
///
/// 注意：
/// - 触发时机建议由逻辑树的 Gate_Phase / Gate_BattleStart / Gate_BattleEnd 决定；
///   本组件自身不负责“阶段逻辑”，只负责“效果”。
/// </summary>
[MaterialCnMeta("加属性", "加属性 增加属性 攻击 防御 血量 暴击 爆伤 速度 幸运 成长")]
public sealed class Effect_AddStatsMaterial : MonoBehaviour, IMaterialEffect, IMaterialDescriptionProvider
{
    [Header("Mode")]
    [Tooltip(
        "运行模式（重要）：\n" +
        "- 不勾选：把数值加到 FightContext 的 CombatantRuntime（战斗临时值）。(只有战斗相关流程生效)\n" +
        "- 勾选：把数值写入 MaterialVommandeTreeContext.GrowthDelta（战后持久成长）。\n" +
        "注意：本组件本身不做阶段判断；阶段由逻辑树的 Gate_Phase 控制。"
    )]
    public bool ApplyAsPersistentGrowth = false;

    [Header("Battle Target (when NOT persistent)")]
    [SerializeField] private FightSide appliesTo = FightSide.Player;

    [Header("Stat Deltas")]
    [SerializeField] private float addAttack = 0f;
    [SerializeField] private float addDefense = 0f;
    [SerializeField] private float addCritChance = 0f;
    [SerializeField] private float addCritMultiplier = 0f;
    [SerializeField] private float addMaxHP = 0f;
    [SerializeField] private int addSpeedRate = 0;
    [SerializeField] private int addLuck = 0;

    public void Execute(in MaterialVommandeTreeContext context)
    {
        if (!ApplyAsPersistentGrowth)
        {
            if (context.Fight == null) return;

            var c = ApplyTo(appliesTo, context.Fight);
            if (c == null) return;

            if (addAttack != 0f) c.AddAttack(addAttack);
            if (addDefense != 0f) c.AddDefense(addDefense);
            if (addCritChance != 0f) c.AddCritChance(addCritChance);
            if (addCritMultiplier != 0f) c.AddCritMultiplier(addCritMultiplier);
            if (addMaxHP != 0f) c.AddMaxHP(addMaxHP, alsoHeal: true);
            if (addSpeedRate != 0) c.AddSpeedRate(addSpeedRate);

            // Luck 不属于 CombatantRuntime（战斗内不支持）
            if (addLuck != 0 && context.Fight.DebugVerbose)
            {
                context.Fight.DebugLogger?.Invoke("[Effect_AddStatsMaterial] 战斗增益模式下忽略 Luck（CombatantRuntime 不包含 Luck）。");
            }

            return;
        }

        // PersistentGrowth（写入 GrowthDelta；阶段由 Gate_Phase(PersistentGrowth) 控制）
        if (context.GrowthDelta == null) return;

        context.GrowthDelta.AddMaxHP += addMaxHP;
        context.GrowthDelta.AddAttack += addAttack;
        context.GrowthDelta.AddDefense += addDefense;
        context.GrowthDelta.AddCritChance += addCritChance;
        context.GrowthDelta.AddCritMultiplier += addCritMultiplier;
        context.GrowthDelta.AddSpeedRate += addSpeedRate;
        context.GrowthDelta.AddLuck += addLuck;
    }

    private static CombatantRuntime ApplyTo(FightSide side, FightContext context)
    {
        if (context == null) return null;
        if (side == FightSide.Player) return context.Player;
        if (side == FightSide.Enemy) return context.Enemy;
        return null;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;

        // 这里不写“战斗开始/战斗结束”，交给 Gate_Phase 等逻辑节点表达
        var who = ApplyAsPersistentGrowth ? "玩家" : (appliesTo == FightSide.Enemy ? "敌人" : "玩家");

        var any = false;
        if (addMaxHP != 0f) { sb.AppendLine($"{who} 最大生命 {(addMaxHP >= 0 ? "+" : "")}{addMaxHP}"); any = true; }
        if (addAttack != 0f) { sb.AppendLine($"{who} 攻击 {(addAttack >= 0 ? "+" : "")}{addAttack}"); any = true; }
        if (addDefense != 0f) { sb.AppendLine($"{who} 防御 {(addDefense >= 0 ? "+" : "")}{addDefense}"); any = true; }
        if (addCritChance != 0f) { sb.AppendLine($"{who} 暴击率 {(addCritChance >= 0 ? "+" : "")}{addCritChance:P0}"); any = true; }
        if (addCritMultiplier != 0f) { sb.AppendLine($"{who} 爆伤倍率 {(addCritMultiplier >= 0 ? "+" : "")}{addCritMultiplier}"); any = true; }
        if (addSpeedRate != 0) { sb.AppendLine($"{who} 速度成长 {(addSpeedRate >= 0 ? "+" : "")}{addSpeedRate}/秒"); any = true; }
        if (ApplyAsPersistentGrowth && addLuck != 0) { sb.AppendLine($"{who} 幸运 {(addLuck >= 0 ? "+" : "")}{addLuck}"); any = true; }

        if (!any) sb.AppendLine($"{who} 属性无变化");
    }
}


