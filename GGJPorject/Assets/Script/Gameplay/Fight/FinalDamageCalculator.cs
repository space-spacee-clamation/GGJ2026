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

    private float defValue = 50;

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (context == null || context.CurrentDefender == null)
        {
            info.FinalDamage = Mathf.Max(0f, info.RawAttack);
            return;
        }

        float damage = info.RawAttack;

        if (EnableCrit)
        {
            bool isCrit = UnityEngine.Random.value < Mathf.Clamp01(info.CritChance);
            info.IsCrit = isCrit;
            if (isCrit)
            {
                damage *= Mathf.Max(1f, info.CritMultiplier);
            }
        }
        if (EnableDefenseReduction)
        {
            //计算有效防御
            float curDef = context.CurrentDefender.Defense*(1-info.PenetrationPercent)-info.PenetrationFixed;
            float reduction;
            if(curDef >= 0)
            {
                reduction = curDef/curDef+defValue;
            }
            else
            {
                reduction = -(1+Math.Abs(curDef)/defValue);
            }
            damage = Mathf.Max(1f,damage-damage*reduction);
            Debug.Log("damage的伤害是"+damage);
            //damage = Mathf.Max(1f, damage - context.CurrentDefender.Defense);
        }

        info.FinalDamage = damage;
    }
}


