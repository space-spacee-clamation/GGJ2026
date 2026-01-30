using System;
using UnityEngine;

/// <summary>
/// 角色静态属性配置（SO）。
/// 注意：战斗中不可修改此配置；战斗只改运行时实例（CombatantRuntime）。
/// </summary>
[Serializable]
public class CharacterConfig 
{
    public float HPBase;
    public float ATKBase;
    public float DEFBase;

    [Range(0f, 1f)] public float CritChance;
    [Min(1f)] public float CritMultiplier = 1.5f;

    [Min(0)] public int SpeedRate = 1;
}
