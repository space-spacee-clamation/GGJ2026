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
    }

    public static CombatantRuntime FromStats(string name, PlayerStats stats)
    {
        stats.Clamp();
        var c = new CombatantRuntime(name, null)
        {
            MaxHP = stats.MaxHP,
            Attack = stats.Attack,
            Defense = stats.Defense,
            CritChance = stats.CritChance,
            CritMultiplier = stats.CritMultiplier,
            SpeedRate = stats.SpeedRate,
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

    public void Damage(float amount)
    {
        if (amount <= 0f) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
    }
}


