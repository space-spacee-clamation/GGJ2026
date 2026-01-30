using System;

[Serializable]
public struct AttackInfo
{
    /// <summary>基础攻击值（来源于攻击者静态属性 + 其它加成）。</summary>
    public float BaseValue;

    /// <summary>暴击概率（0~1）。</summary>
    public float CritChance;

    /// <summary>暴击乘数（例如 1.5 表示 150%）。</summary>
    public float CritMultiplier;

    /// <summary>未结算暴击的“实际攻击”（主要用于处理链修改）。</summary>
    public float RawAttack;

    /// <summary>本次是否暴击（由数值计算器写入）。</summary>
    public bool IsCrit;

    /// <summary>最终伤害（由数值计算器写入；若未写入则 FightManager 可回退使用 RawAttack）。</summary>
    public float FinalDamage;
}


