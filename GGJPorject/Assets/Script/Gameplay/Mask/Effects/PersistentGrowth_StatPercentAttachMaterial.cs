using System.Text;
using UnityEngine;

public enum PersistentGrowthSourceDomain
{
    /// <summary>读取玩家当前 ActualStats（不包含本场战斗临时增益）。</summary>
    ActualStats = 0,

    /// <summary>读取 battleContext.Player（包含战斗开始等临时增益后的 BattleStats）。</summary>
    BattleStats = 1,
}

/// <summary>
/// 战后成长：将“来源属性 * 百分比”附加到“目标属性”（写入 PlayerGrowthDelta）。
/// 例：将 最大生命 的 10% 附加到 攻击（按当前 ActualStats 计算）。
/// </summary>
public sealed class PersistentGrowth_StatPercentAttachMaterial : MonoBehaviour, IPersistentGrowthProvider, IMaterialDescriptionProvider
{
    [Header("Source")]
    [SerializeField] private PersistentGrowthSourceDomain sourceDomain = PersistentGrowthSourceDomain.ActualStats;
    [SerializeField] private StatKey sourceStat = StatKey.MaxHP;

    [Header("Target")]
    [SerializeField] private StatKey targetStat = StatKey.Attack;

    [Header("Formula")]
    [Tooltip("百分比系数：0.2=20%，1=100%，可填负数")]
    [SerializeField] private float percent = 0.1f;

    public void OnCollectPersistentGrowth(Player player, PlayerGrowthDelta delta, FightContext battleContext)
    {
        if (player == null || delta == null) return;

        float srcValue;
        switch (sourceDomain)
        {
            case PersistentGrowthSourceDomain.BattleStats:
                srcValue = StatMathUtil.GetFromCombatant(battleContext == null ? null : battleContext.Player, sourceStat);
                break;
            case PersistentGrowthSourceDomain.ActualStats:
            default:
                srcValue = StatMathUtil.GetFromPlayerStats(player.ActualStats, sourceStat);
                break;
        }

        var add = srcValue * percent;
        StatMathUtil.AddToGrowth(delta, targetStat, add);

        if (battleContext != null && battleContext.DebugVerbose)
        {
            Debug.Log(
                $"[PersistentGrowth_StatPercentAttachMaterial] {sourceDomain}.{sourceStat}({srcValue}) * {percent:P0} => add {add} to Growth.{targetStat}",
                this);
        }
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        // 去掉“战后/永久提高”等修饰；时间点交给 Gate_BattleEnd 表达
        sb.AppendLine($"将 {StatMathUtil.ToCnName(sourceStat)} 的 {percent:P0} 附加到 {StatMathUtil.ToCnName(targetStat)}");
    }
}


