using UnityEngine;

/// <summary>
/// GameJam 默认配置（纯代码，避免创建/维护大量 ScriptableObject 资产）。
/// 约定：
/// - 这里的值就是“当前 Jam 的默认玩法参数”，需要调参就直接改这里。
/// - 正式版再把这些拆回 SO/关卡配置/策划表。
/// </summary>
public static class JamDefaultSettings
{
    // ---- Player ----
    public static PlayerStats DefaultPlayerBaseStats => new PlayerStats
    {
        MaxHP = 80f,
        Attack = 12f,
        Defense = 3f,
        CritChance = 0.10f,
        CritMultiplier = 1.5f,
        SpeedRate = 6,
        Luck = 20
    };

    // ---- Drop ----
    /// <summary>Resources 下材质 prefab 的文件夹名（相对 Resources 根）。</summary>
    public const string ResourcesMatFolder = "Mat";

    /// <summary>每场战斗掉落次数（抽几次）。</summary>
    public const int DropCountPerBattle = 3;

    /// <summary>开局发放的 Common 材质数量（用于第一回合能做面具）。</summary>
    public const int InitialCommonMaterialCount = 4;

    // ---- Persistent Growth (Post Battle) ----
    /// <summary>
    /// 持久成长结算器（战后把 PlayerGrowthDelta 套公式后再实际加到玩家身上）。
    /// Jam 默认实现为“直接加上去”；策划要改成长公式，就改 JamPersistentGrowthCalculator_Default.Apply。
    /// </summary>
    public static readonly IJamPersistentGrowthCalculator PersistentGrowthCalculator = new JamPersistentGrowthCalculator_Default();
}


