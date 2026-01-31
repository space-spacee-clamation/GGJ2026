using UnityEngine;

/// <summary>
/// 玩家单例：持有 BaseStats / ActualStats，并可构建 BattleStats。
/// </summary>
public sealed class Player
{
    public static Player I { get; private set; }

    public PlayerStats BaseStats { get; private set; }
    public PlayerStats ActualStats { get; private set; }

    public static void CreateSingleton(PlayerStats baseStats)
    {
        if (I != null) return;
        baseStats.Clamp();
        I = new Player
        {
            BaseStats = baseStats,
            ActualStats = baseStats
        };
    }

    public void ApplyGrowth(PlayerGrowthDelta delta)
    {
        if (delta == null) return;

        var a = ActualStats;
        a.MaxHP += delta.AddMaxHP;
        a.Attack += delta.AddAttack;
        a.Defense += delta.AddDefense;
        a.CritChance += delta.AddCritChance;
        a.CritMultiplier += delta.AddCritMultiplier;
        a.SpeedRate += delta.AddSpeedRate;
        a.Luck += delta.AddLuck;
        a.PenetrationPercent += delta.AddPenetrationPercent;
        a.PenetrationFixed += delta.AddPenetrationFixed;
        a.Clamp();
        ActualStats = a;
    }

    public PlayerStats BuildBattleStats()
    {
        // BattleStats = ActualStats 的复制；材料战斗增益在开战时叠加到 BattleStats（由战斗系统/材料组件修改）。
        return ActualStats;
    }
}


