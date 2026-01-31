using System;

/// <summary>
/// 角色属性枚举：用于“将 A 的百分之 X 附加到 B”这类词条。
/// 注意：战斗运行时（CombatantRuntime）不包含 Luck；若在战斗效果里选择 Luck 会被忽略并输出提示。
/// </summary>
[Serializable]
public enum StatKey
{
    MaxHP = 0,
    Attack = 1,
    Defense = 2,
    CritChance = 3,
    CritMultiplier = 4,
    SpeedRate = 5,
    Luck = 6,
}


