using System;
using UnityEngine;

/// <summary>
/// Final damage settlement. This modifier MUST be executed last in the pipeline.
/// </summary>
[Serializable]
public sealed class FinalDamageCalculator : IAttackInfoModifier
{
    [Tooltip("Enable defense reduction: damage = max(0, raw - defenderDef)")]
    public bool EnableDefenseReduction = true;

    [Tooltip("Enable critical: rand < CritChance => damage *= CritMultiplier")]
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


