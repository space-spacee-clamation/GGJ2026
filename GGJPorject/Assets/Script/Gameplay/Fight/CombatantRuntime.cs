using UnityEngine;

/// <summary>
/// 战斗中的运行时角色实例数据（注意：不要修改 Config，战斗只改运行时实例）。
/// </summary>
public sealed class CombatantRuntime
{
    public string Name { get; }

    public float MaxHP { get; private set; }
    public float CurrentHP { get; private set; }

    public float Attack { get; private set; }
    public float Defense { get; private set; }

    /// <summary>0~1</summary>
    public float CritChance { get; private set; }

    /// <summary>暴击乘数（例如 1.5）。</summary>
    public float CritMultiplier { get; private set; }

    public int SpeedRate { get; private set; }

    /// <summary>百分比穿透（0~1）</summary>
    public float PenetrationPercent { get; private set; }

    /// <summary>固定穿透（>=0）</summary>
    public float PenetrationFixed { get; private set; }

    public bool IsDead => CurrentHP <= 0f;

    public CombatantRuntime(string name, CharacterConfig config)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Combatant" : name;
        ApplyConfig(config);
        CurrentHP = MaxHP;
    }

    public void ApplyConfig(CharacterConfig config)
    {
        if (config == null)
        {
            // Jam 容错：缺配置时给个兜底，避免崩溃
            MaxHP = 10f;
            Attack = 1f;
            Defense = 0f;
            CritChance = 0f;
            CritMultiplier = 1.5f;
            SpeedRate = 1;
            return;
        }

        MaxHP = Mathf.Max(1f, config.HPBase);
        Attack = Mathf.Max(0f, config.ATKBase);
        Defense = Mathf.Max(0f, config.DEFBase);
        CritChance = Mathf.Clamp01(config.CritChance);
        CritMultiplier = Mathf.Max(1f, config.CritMultiplier);
        SpeedRate = Mathf.Max(0, config.SpeedRate);
        PenetrationPercent = 0f;
        PenetrationFixed = 0f;
    }

    public static CombatantRuntime FromStats(string name, PlayerStats stats, PlayerGrowthDelta pendingGrowthDelta)
    {
        stats.Clamp();
        var c = new CombatantRuntime(name, null)
        {
            MaxHP = stats.MaxHP+pendingGrowthDelta.AddMaxHP,
            Attack = stats.Attack+pendingGrowthDelta.AddAttack,
            Defense = stats.Defense+pendingGrowthDelta.AddDefense,
            CritChance = stats.CritChance+pendingGrowthDelta.AddCritChance,
            CritMultiplier = stats.CritMultiplier+pendingGrowthDelta.AddCritMultiplier,
            SpeedRate = stats.SpeedRate+pendingGrowthDelta.AddSpeedRate,
            PenetrationPercent = stats.PenetrationPercent+pendingGrowthDelta.AddPenetrationPercent,
            PenetrationFixed = stats.PenetrationFixed+pendingGrowthDelta.AddPenetrationFixed,
        };
        c.CurrentHP = c.MaxHP;
        return c;
    }

    public void ResetHP() => CurrentHP = MaxHP;

    public void AddAttack(float delta)
    {
        Attack = Mathf.Max(0f, Attack + delta);
    }

    public void AddDefense(float delta)
    {
        Defense = Mathf.Max(0f, Defense + delta);
    }

    public void AddCritChance(float delta)
    {
        CritChance = Mathf.Clamp01(CritChance + delta);
    }

    public void AddCritMultiplier(float delta)
    {
        CritMultiplier = Mathf.Max(1f, CritMultiplier + delta);
    }

    public void AddSpeedRate(int delta)
    {
        SpeedRate = Mathf.Max(0, SpeedRate + delta);
    }

    public void AddMaxHP(float delta, bool alsoHeal)
    {
        if (delta == 0f) return;
        var oldMax = MaxHP;
        MaxHP = Mathf.Max(1f, MaxHP + delta);
        if (alsoHeal)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + (MaxHP - oldMax));
        }
        else
        {
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
        }
    }

    public void Damage(float amount)
    {
        if (amount <= 0f) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
    }

    /// <summary>
    /// 改变当前生命值（可正可负）：会 clamp 到 [0, MaxHP]。
    /// 用于“行动后改变当前生命值（百分比/固定值）”等材料效果。
    /// </summary>
    public void AddCurrentHP(float delta)
    {
        if (delta == 0f) return;
        CurrentHP = Mathf.Clamp(CurrentHP + delta, 0f, MaxHP);
    }

    public void AddPenetrationPercent(float delta)
    {
        PenetrationPercent = Mathf.Clamp01(PenetrationPercent + delta);
    }

    public void AddPenetrationFixed(float delta)
    {
        PenetrationFixed = Mathf.Max(0f, PenetrationFixed + delta);
    }
}


