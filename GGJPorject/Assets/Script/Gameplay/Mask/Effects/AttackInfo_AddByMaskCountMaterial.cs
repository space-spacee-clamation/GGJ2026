using System.Text;
using UnityEngine;

public enum AttackInfoNumericField
{
    RawAttack = 0,
    BaseValue = 1,
    CritChance = 2,
    CritMultiplier = 3,
}

/// <summary>
/// 行动阶段（AttackModify）：按面具数量缩放数值。
/// 例：每有 1 个面具，RawAttack +2。
/// </summary>
public sealed class AttackInfo_AddByMaskCountMaterial : MonoBehaviour, IMaterialAttackInfoEffect, IMaterialDescriptionProvider
{
    [SerializeField] private AttackInfoNumericField field = AttackInfoNumericField.RawAttack;
    [Tooltip("每个面具提供的加成（可负数）。")]
    [SerializeField] private float addPerMask = 1f;

    public void Modify(ref AttackInfo info, in MaterialVommandeTreeContext context)
    {
        if (context.Fight == null) return;
        int count = Mathf.Max(0, context.Fight.MaskCount);
        if (count <= 0) return;

        var add = count * addPerMask;
        if (add == 0f) return;

        switch (field)
        {
            case AttackInfoNumericField.BaseValue:
                info.BaseValue += add;
                return;
            case AttackInfoNumericField.CritChance:
                info.CritChance = Mathf.Clamp01(info.CritChance + add);
                return;
            case AttackInfoNumericField.CritMultiplier:
                info.CritMultiplier = Mathf.Max(1f, info.CritMultiplier + add);
                return;
            case AttackInfoNumericField.RawAttack:
            default:
                info.RawAttack += add;
                return;
        }
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var name = field switch
        {
            AttackInfoNumericField.RawAttack => "攻击",
            AttackInfoNumericField.BaseValue => "基础攻击",
            AttackInfoNumericField.CritChance => "暴击率",
            AttackInfoNumericField.CritMultiplier => "爆伤倍率",
            _ => field.ToString()
        };
        sb.AppendLine($"每有 1 个面具，{name} {(addPerMask >= 0 ? "+" : "")}{addPerMask:0.##}");
    }
}


