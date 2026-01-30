using System.Text;
using UnityEngine;

/// <summary>
/// 战后对玩家 ActualStats 的持久增值（可填负数；最终会 clamp）。
/// </summary>
public sealed class PersistentGrowthAddStatsMaterial : MonoBehaviour, IPersistentGrowthProvider, IMaterialDescriptionProvider
{
    [Header("Growth Deltas")]
    [SerializeField] private float addMaxHP = 0f;
    [SerializeField] private float addAttack = 0f;
    [SerializeField] private float addDefense = 0f;
    [SerializeField] private float addCritChance = 0f;
    [SerializeField] private float addCritMultiplier = 0f;
    [SerializeField] private int addSpeedRate = 0;
    [SerializeField] private int addLuck = 0;

    public void OnCollectPersistentGrowth(Player player, PlayerGrowthDelta delta, FightContext battleContext)
    {
        if (delta == null) return;
        delta.AddMaxHP += addMaxHP;
        delta.AddAttack += addAttack;
        delta.AddDefense += addDefense;
        delta.AddCritChance += addCritChance;
        delta.AddCritMultiplier += addCritMultiplier;
        delta.AddSpeedRate += addSpeedRate;
        delta.AddLuck += addLuck;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        // 去掉“战后/永久提高”等修饰；时间点交给 Gate_BattleEnd 表达
        var any = false;
        if (addMaxHP != 0f) { sb.AppendLine($"最大生命 {(addMaxHP >= 0 ? "+" : "")}{addMaxHP}"); any = true; }
        if (addAttack != 0f) { sb.AppendLine($"攻击 {(addAttack >= 0 ? "+" : "")}{addAttack}"); any = true; }
        if (addDefense != 0f) { sb.AppendLine($"防御 {(addDefense >= 0 ? "+" : "")}{addDefense}"); any = true; }
        if (addCritChance != 0f) { sb.AppendLine($"暴击率 {(addCritChance >= 0 ? "+" : "")}{addCritChance:P0}"); any = true; }
        if (addCritMultiplier != 0f) { sb.AppendLine($"爆伤倍率 {(addCritMultiplier >= 0 ? "+" : "")}{addCritMultiplier}"); any = true; }
        if (addSpeedRate != 0) { sb.AppendLine($"速度成长 {(addSpeedRate >= 0 ? "+" : "")}{addSpeedRate}/秒"); any = true; }
        if (addLuck != 0) { sb.AppendLine($"幸运 {(addLuck >= 0 ? "+" : "")}{addLuck}"); any = true; }
        if (!any) sb.AppendLine("无变化");
    }
}


