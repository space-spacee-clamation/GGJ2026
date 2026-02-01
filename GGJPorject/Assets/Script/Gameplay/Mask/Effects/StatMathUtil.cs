using UnityEngine;

public static class StatMathUtil
{
    public static string ToCnName(StatKey key)
    {
        switch (key)
        {
            case StatKey.MaxHP: return "最大生命";
            case StatKey.Attack: return "攻击";
            case StatKey.Defense: return "防御";
            case StatKey.CritChance: return "暴击率";
            case StatKey.CritMultiplier: return "爆伤倍率";
            case StatKey.SpeedRate: return "速度";
            case StatKey.Luck: return "幸运";
            default: return key.ToString();
        }
    }

    public static float GetFromCombatant(CombatantRuntime c, StatKey key)
    {
        if (c == null) return 0f;
        switch (key)
        {
            case StatKey.MaxHP: return c.MaxHP;
            case StatKey.Attack: return c.Attack;
            case StatKey.Defense: return c.Defense;
            case StatKey.CritChance: return c.CritChance;
            case StatKey.CritMultiplier: return c.CritMultiplier;
            case StatKey.SpeedRate: return c.SpeedRate;
            case StatKey.Luck: return 0f; // 战斗运行时无 Luck
            default: return 0f;
        }
    }

    public static float GetFromPlayerStats(PlayerStats stats, StatKey key)
    {
        switch (key)
        {
            case StatKey.MaxHP: return stats.MaxHP;
            case StatKey.Attack: return stats.Attack;
            case StatKey.Defense: return stats.Defense;
            case StatKey.CritChance: return stats.CritChance;
            case StatKey.CritMultiplier: return stats.CritMultiplier;
            case StatKey.SpeedRate: return stats.SpeedRate;
            case StatKey.Luck: return stats.Luck;
            default: return 0f;
        }
    }

    public static void AddToCombatant(CombatantRuntime c, StatKey key, float delta, bool maxHpAlsoHeal)
    {
        if (c == null) return;
        if (delta == 0f) return;

        switch (key)
        {
            case StatKey.MaxHP:
                c.AddMaxHP(delta, alsoHeal: maxHpAlsoHeal);
                return;
            case StatKey.Attack:
                c.AddAttack(delta);
                return;
            case StatKey.Defense:
                c.AddDefense(delta);
                return;
            case StatKey.CritChance:
                c.AddCritChance(delta);
                return;
            case StatKey.CritMultiplier:
                c.AddCritMultiplier(delta);
                return;
            case StatKey.SpeedRate:
                c.AddSpeedRate(Mathf.RoundToInt(delta));
                return;
            case StatKey.Luck:
                // 战斗运行时无 Luck：忽略
                return;
        }
    }

    public static void AddToGrowth(PlayerGrowthDelta delta, StatKey key, float add)
    {
        if (delta == null) return;
        if (add == 0f) return;

        switch (key)
        {
            case StatKey.MaxHP: delta.AddMaxHP += add; return;
            case StatKey.Attack: delta.AddAttack += add; return;
            case StatKey.Defense: delta.AddDefense += add; return;
            case StatKey.CritChance: delta.AddCritChance += add; return;
            case StatKey.CritMultiplier: delta.AddCritMultiplier += add; return;
            case StatKey.SpeedRate: delta.AddSpeedRate += Mathf.RoundToInt(add); return;
            case StatKey.Luck: delta.AddLuck += Mathf.RoundToInt(add); return;
        }
    }
}


