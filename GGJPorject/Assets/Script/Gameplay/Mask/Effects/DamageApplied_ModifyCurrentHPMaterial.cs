using System.Text;
using UnityEngine;

public enum CurrentHpModifyTarget
{
    Attacker = 0,
    Defender = 1,
}

public enum CurrentHpModifyMode
{
    Flat = 0,
    PercentOfMaxHP = 1,
}

/// <summary>
/// 行动结算后（已扣血）：改变某一方的当前生命值（可正可负）。
/// - Flat：直接加/减固定值
/// - Percent：按目标 MaxHP 的百分比加/减
/// </summary>
public sealed class DamageApplied_ModifyCurrentHPMaterial : MonoBehaviour, IMaterialDamageAppliedEffect, IMaterialDescriptionProvider
{
    [SerializeField] private CurrentHpModifyTarget target = CurrentHpModifyTarget.Attacker;
    [SerializeField] private CurrentHpModifyMode mode = CurrentHpModifyMode.Flat;

    [Tooltip("固定值变化（可负数）。")]
    [SerializeField] private float flatDelta = 0f;

    [Tooltip("按 MaxHP 的百分比变化（可负数）。0.1=+10%，-0.2=-20%。")]
    [SerializeField] private float percentOfMaxHpDelta = 0f;

    public void OnDamageApplied(FightContext context, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage)
    {
        if (context == null) return;

        CombatantRuntime t;
        if (target == CurrentHpModifyTarget.Defender)
        {
            t = defenderSide == FightSide.Player ? context.Player : context.Enemy;
        }
        else
        {
            t = attackerSide == FightSide.Player ? context.Player : context.Enemy;
        }

        if (t == null) return;

        float delta = 0f;
        switch (mode)
        {
            case CurrentHpModifyMode.PercentOfMaxHP:
                delta = t.MaxHP * percentOfMaxHpDelta;
                break;
            case CurrentHpModifyMode.Flat:
            default:
                delta = flatDelta;
                break;
        }

        if (delta != 0f) t.AddCurrentHP(delta);
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var who = target == CurrentHpModifyTarget.Defender ? "受击者" : "攻击者";
        if (mode == CurrentHpModifyMode.PercentOfMaxHP)
        {
            sb.AppendLine($"{who} 当前生命 {(percentOfMaxHpDelta >= 0 ? "+" : "")}{percentOfMaxHpDelta:P0}（按最大生命）");
        }
        else
        {
            sb.AppendLine($"{who} 当前生命 {(flatDelta >= 0 ? "+" : "")}{flatDelta:0.##}");
        }
    }
}


