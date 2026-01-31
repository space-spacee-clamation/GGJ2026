using System.Text;
using UnityEngine;

/// <summary>
/// 战斗开始时：将“来源属性 * 百分比”附加到“目标属性”。
/// 例：将 玩家攻击 的 50% 附加到 玩家防御。
/// </summary>
public sealed class Battle_StatPercentAttachMaterial : MonoBehaviour, IMaterialEffect, IMaterialDescriptionProvider
{
    [Header("Source")]
    [SerializeField] private FightSide sourceSide = FightSide.Player;
    [SerializeField] private StatKey sourceStat = StatKey.Attack;

    [Header("Target")]
    [SerializeField] private FightSide targetSide = FightSide.Player;
    [SerializeField] private StatKey targetStat = StatKey.Defense;

    [Header("Formula")]
    [Tooltip("百分比系数：0.2=20%，1=100%，可填负数")]
    [SerializeField] private float percent = 0.5f;

    [Tooltip("当目标为 MaxHP 时，是否同时治疗（保持血量比例/补差值）。")]
    [SerializeField] private bool maxHpAlsoHeal = true;

    public void Execute(in MaterialVommandeTreeContext context)
    {
        if (context.Fight == null) return;

        var src = ApplyTo(sourceSide, context.Fight);
        var dst = ApplyTo(targetSide, context.Fight);
        if (src == null || dst == null) return;

        if (sourceStat == StatKey.Luck || targetStat == StatKey.Luck)
        {
            if (context.Fight.DebugVerbose)
                Debug.LogWarning($"[BattleStart_StatPercentAttachMaterial] 战斗运行时不支持 Luck；source={sourceStat}, target={targetStat}", this);
            return;
        }

        var srcValue = StatMathUtil.GetFromCombatant(src, sourceStat);
        var add = srcValue * percent;
        StatMathUtil.AddToCombatant(dst, targetStat, add, maxHpAlsoHeal);

        if (context.Fight.DebugVerbose)
        {
            Debug.Log(
                $"[BattleStart_StatPercentAttachMaterial] {sourceSide}.{sourceStat}({srcValue}) * {percent:P0} => add {add} to {targetSide}.{targetStat}",
                this);
        }
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

        var srcWho = sourceSide == FightSide.Enemy ? "敌人" : "玩家";
        var dstWho = targetSide == FightSide.Enemy ? "敌人" : "玩家";

        if (sourceStat == StatKey.Luck || targetStat == StatKey.Luck)
        {
            sb.Append("（战斗内不支持 幸运 属性转换）");
            return;
        }

        sb.Append($"将 {srcWho}{StatMathUtil.ToCnName(sourceStat)} 的 {percent:P0} 附加到 {dstWho}{StatMathUtil.ToCnName(targetStat)}");
    }
}


