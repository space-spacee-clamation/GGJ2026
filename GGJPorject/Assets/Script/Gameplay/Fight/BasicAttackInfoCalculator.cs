using System;
using UnityEngine;

/// <summary>
/// V0 最简数值计算器：支持防御减伤与暴击（公式后续可替换）。
/// 注意：此类为“可序列化的纯数据对象”，用于 Odin 接口序列化注入；不是 MonoBehaviour。
/// </summary>
[Serializable]
public sealed class BasicAttackInfoCalculator : IAttackInfoModifier
{
    [Tooltip("是否启用防御减伤：damage = max(0, raw - defenderDef)")]
    public bool EnableDefenseReduction = true;

    [Tooltip("如果开启暴击：随机 < CritChance 则 damage *= CritMultiplier")]
    public bool EnableCrit = true;

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (context == null || context.CurrentDefender == null)
        {
            info.FinalDamage = Mathf.Max(0f, info.RawAttack);
            return;
        }

        float damage = info.RawAttack;

        if (EnableDefenseReduction)
        {
            damage = Mathf.Max(0f, damage - context.CurrentDefender.Defense);
        }

        if (EnableCrit)
        {
            bool isCrit = UnityEngine.Random.value < Mathf.Clamp01(info.CritChance);
            info.IsCrit = isCrit;
            if (isCrit)
            {
                damage *= Mathf.Max(1f, info.CritMultiplier);
            }
        }

        info.FinalDamage = damage;
    }
}


