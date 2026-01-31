using UnityEngine;

/// <summary>
/// 玩家信息 UI：显示玩家的各种数值（当前数值、提升数值、战斗实时数值）。
/// </summary>
public class PlayerInfoUI : MonoBehaviour
{
    public enum DisplayMode
    {
        /// <summary>玩家现在的数值（ActualStats）。</summary>
        Current = 0,
        /// <summary>提升的数值（还未运用，即 PlayerGrowthDelta）。</summary>
        PendingGrowth = 1,
        /// <summary>战斗实时的数值（FightContext.Player）。</summary>
        BattleRuntime = 2,
    }

    [Header("显示模式")]
    [Tooltip("当前显示模式：当前数值、提升数值、战斗实时数值。")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.Current;

    [Header("数值显示框")]
    [Tooltip("最大生命值")]
    [SerializeField] private SmallInfoBox maxHPBox;
    [Tooltip("攻击力")]
    [SerializeField] private SmallInfoBox attackBox;
    [Tooltip("防御力")]
    [SerializeField] private SmallInfoBox defenseBox;
    [Tooltip("暴击率")]
    [SerializeField] private SmallInfoBox critChanceBox;
    [Tooltip("爆伤倍率")]
    [SerializeField] private SmallInfoBox critMultiplierBox;
    [Tooltip("速度成长")]
    [SerializeField] private SmallInfoBox speedRateBox;
    [Tooltip("幸运")]
    [SerializeField] private SmallInfoBox luckBox;
    [Tooltip("百分比穿透")]
    [SerializeField] private SmallInfoBox penetrationPercentBox;
    [Tooltip("固定穿透")]
    [SerializeField] private SmallInfoBox penetrationFixedBox;

    /// <summary>
    /// 设置显示模式并刷新。
    /// </summary>
    public void SetDisplayMode(DisplayMode mode)
    {
        displayMode = mode;
        Refresh();
    }

    /// <summary>
    /// 刷新所有数值显示。
    /// </summary>
    public void Refresh()
    {
        switch (displayMode)
        {
            case DisplayMode.Current:
                RefreshCurrentStats();
                break;
            case DisplayMode.PendingGrowth:
                RefreshPendingGrowth();
                break;
            case DisplayMode.BattleRuntime:
                RefreshBattleRuntime();
                break;
        }
    }

    private void RefreshCurrentStats()
    {
        if (Player.I == null)
        {
            ClearAll();
            return;
        }

        var stats = Player.I.ActualStats;
        RefreshBox(maxHPBox, FormatFloat(stats.MaxHP));
        RefreshBox(attackBox, FormatFloat(stats.Attack));
        RefreshBox(defenseBox, FormatFloat(stats.Defense));
        RefreshBox(critChanceBox, FormatPercent(stats.CritChance));
        RefreshBox(critMultiplierBox, FormatFloat(stats.CritMultiplier, 2));
        RefreshBox(speedRateBox, stats.SpeedRate.ToString());
        RefreshBox(luckBox, stats.Luck.ToString());
        RefreshBox(penetrationPercentBox, FormatPercent(stats.PenetrationPercent));
        RefreshBox(penetrationFixedBox, FormatFloat(stats.PenetrationFixed));
    }

    private void RefreshPendingGrowth()
    {
        // 获取未应用的提升值（从 GameManager 或全局存储）
        // 注意：如果 GameManager 没有存储未应用的提升值，这里需要先添加
        var delta = GetPendingGrowthDelta();
        if (delta == null)
        {
            ClearAll();
            return;
        }

        RefreshBox(maxHPBox, FormatDelta(delta.AddMaxHP));
        RefreshBox(attackBox, FormatDelta(delta.AddAttack));
        RefreshBox(defenseBox, FormatDelta(delta.AddDefense));
        RefreshBox(critChanceBox, FormatDelta(delta.AddCritChance, true));
        RefreshBox(critMultiplierBox, FormatDelta(delta.AddCritMultiplier));
        RefreshBox(speedRateBox, FormatDelta(delta.AddSpeedRate));
        RefreshBox(luckBox, FormatDelta(delta.AddLuck));
        RefreshBox(penetrationPercentBox, FormatDelta(delta.AddPenetrationPercent, true));
        RefreshBox(penetrationFixedBox, FormatDelta(delta.AddPenetrationFixed));
    }

    private void RefreshBattleRuntime()
    {
        var ctx = FightManager.I?.Context;
        if (ctx == null || ctx.Player == null)
        {
            ClearAll();
            return;
        }

        var player = ctx.Player;
        RefreshBox(maxHPBox, FormatFloat(player.MaxHP));
        RefreshBox(attackBox, FormatFloat(player.Attack));
        RefreshBox(defenseBox, FormatFloat(player.Defense));
        RefreshBox(critChanceBox, FormatPercent(player.CritChance));
        RefreshBox(critMultiplierBox, FormatFloat(player.CritMultiplier, 2));
        RefreshBox(speedRateBox, player.SpeedRate.ToString());
        // 战斗运行时无 Luck
        RefreshBox(luckBox, "-");
        RefreshBox(penetrationPercentBox, FormatPercent(player.PenetrationPercent));
        RefreshBox(penetrationFixedBox, FormatFloat(player.PenetrationFixed));
    }

    private void ClearAll()
    {
        RefreshBox(maxHPBox, "-");
        RefreshBox(attackBox, "-");
        RefreshBox(defenseBox, "-");
        RefreshBox(critChanceBox, "-");
        RefreshBox(critMultiplierBox, "-");
        RefreshBox(speedRateBox, "-");
        RefreshBox(luckBox, "-");
        RefreshBox(penetrationPercentBox, "-");
        RefreshBox(penetrationFixedBox, "-");
    }

    private void RefreshBox(SmallInfoBox box, string text)
    {
        if (box != null)
        {
            box.Refresh(text);
        }
    }

    private string FormatFloat(float value, int decimals = 1)
    {
        return value.ToString($"F{decimals}");
    }

    private string FormatPercent(float value)
    {
        return (value * 100f).ToString("F1") + "%";
    }

    private string FormatDelta(float delta, bool isPercent = false)
    {
        if (delta == 0f) return "0";
        string sign = delta > 0f ? "+" : "";
        if (isPercent)
        {
            return sign + (delta * 100f).ToString("F1") + "%";
        }
        return sign + delta.ToString("F1");
    }

    private string FormatDelta(int delta)
    {
        if (delta == 0) return "0";
        string sign = delta > 0 ? "+" : "";
        return sign + delta.ToString();
    }

    /// <summary>
    /// 获取未应用的提升值。
    /// </summary>
    private PlayerGrowthDelta GetPendingGrowthDelta()
    {
        if (GameManager.I != null)
        {
            return GameManager.I.PendingGrowthDelta;
        }
        return null;
    }

    // Unity 生命周期：在 Inspector 中修改 displayMode 时自动刷新
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Refresh();
        }
    }
    private void Update(){
        Refresh();
    }
}

