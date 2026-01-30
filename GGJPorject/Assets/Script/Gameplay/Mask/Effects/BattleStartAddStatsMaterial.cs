using System.Text;
using UnityEngine;

/// <summary>
/// 战斗开始时对战斗数值做加值（可填负数）。
/// </summary>
public sealed class BattleStartAddStatsMaterial : MonoBehaviour, IMaterialBattleStartEffect, IMaterialDescriptionProvider
{
    [SerializeField] private FightSide appliesTo = FightSide.Player;

    [Header("Battle Stat Deltas")]
    [SerializeField] private float addAttack = 0f;
    [SerializeField] private float addDefense = 0f;
    [SerializeField] private float addCritChance = 0f;
    [SerializeField] private float addCritMultiplier = 0f;
    [SerializeField] private float addMaxHP = 0f;
    [SerializeField] private int addSpeedRate = 0;

    public void OnBattleStart(FightContext context)
    {
        var c = ApplyTo(appliesTo, context);
        if (c == null) return;

        if (addAttack != 0f) c.AddAttack(addAttack);
        if (addDefense != 0f) c.AddDefense(addDefense);
        if (addCritChance != 0f) c.AddCritChance(addCritChance);
        if (addCritMultiplier != 0f) c.AddCritMultiplier(addCritMultiplier);
        if (addMaxHP != 0f) c.AddMaxHP(addMaxHP, alsoHeal: true);
        if (addSpeedRate != 0) c.AddSpeedRate(addSpeedRate);
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
        var who = appliesTo == FightSide.Enemy ? "敌人" : "玩家";
        var any = false;

        if (addAttack != 0f) { sb.AppendLine($"战斗开始：{who} 攻击 {(addAttack >= 0 ? "+" : "")}{addAttack}"); any = true; }
        if (addDefense != 0f) { sb.AppendLine($"战斗开始：{who} 防御 {(addDefense >= 0 ? "+" : "")}{addDefense}"); any = true; }
        if (addCritChance != 0f) { sb.AppendLine($"战斗开始：{who} 暴击率 {(addCritChance >= 0 ? "+" : "")}{addCritChance:P0}"); any = true; }
        if (addCritMultiplier != 0f) { sb.AppendLine($"战斗开始：{who} 爆伤倍率 {(addCritMultiplier >= 0 ? "+" : "")}{addCritMultiplier}"); any = true; }
        if (addMaxHP != 0f) { sb.AppendLine($"战斗开始：{who} 最大生命 {(addMaxHP >= 0 ? "+" : "")}{addMaxHP}"); any = true; }
        if (addSpeedRate != 0) { sb.AppendLine($"战斗开始：{who} 速度成长 {(addSpeedRate >= 0 ? "+" : "")}{addSpeedRate}/秒"); any = true; }

        if (!any) sb.AppendLine($"战斗开始：{who} 属性无变化");
    }
}


