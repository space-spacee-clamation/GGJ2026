/// <summary>
/// 持久成长结算器（Jam 版，纯代码可改公式）。
///
/// 设计目标：
/// - 材料树只负责“往 PlayerGrowthDelta 写入增量”
/// - 战后结算时由这里统一“处理增量/套公式/再实际加到玩家身上”
///
/// 为什么要单独一层：
/// - 避免每个效果器各自 Clamp/倍率/上限，导致规则分散
/// - 策划只需要改这一处的公式，就能整体调平衡
/// </summary>
public interface IJamPersistentGrowthCalculator
{
    /// <summary>
    /// 输入：本场战斗收集到的 delta 与玩家。
    /// 输出：对玩家实际应用（默认实现=直接加），并可在内部 Reset delta（避免误复用）。
    /// </summary>
    void Apply(Player player, PlayerGrowthDelta delta, FightContext fightContext);
}

/// <summary>
/// 默认实现：直接把 delta 加到玩家 ActualStats（等价于 Player.ApplyGrowth）。
/// 策划改公式就改这里。
/// </summary>
public sealed class JamPersistentGrowthCalculator_Default : IJamPersistentGrowthCalculator
{
    public void Apply(Player player, PlayerGrowthDelta delta, FightContext fightContext)
    {
        if (player == null || delta == null) return;

        // 这里就是“公式入口”，你可以在 ApplyGrowth 之前对 delta 做任意处理：
        // - 倍率、上限、衰减
        // - 基于玩家当前 ActualStats 或 battle stats（fightContext.Player）做缩放
        // - 例如：delta.AddAttack = Mathf.Floor(delta.AddAttack * 0.5f);

        player.ApplyGrowth(delta);

        // Jam 防呆：应用完清零（即使目前 delta 是临时对象，也能避免未来改成复用时踩坑）
        Reset(delta);
    }

    // ---- 辅助方法：统一写 delta（让公式代码更好读）----
    public static void AddAttack(PlayerGrowthDelta delta, float value) { if (delta != null) delta.AddAttack += value; }
    public static void AddDefense(PlayerGrowthDelta delta, float value) { if (delta != null) delta.AddDefense += value; }
    public static void AddMaxHP(PlayerGrowthDelta delta, float value) { if (delta != null) delta.AddMaxHP += value; }
    public static void AddCritChance(PlayerGrowthDelta delta, float value) { if (delta != null) delta.AddCritChance += value; }
    public static void AddCritMultiplier(PlayerGrowthDelta delta, float value) { if (delta != null) delta.AddCritMultiplier += value; }
    public static void AddSpeedRate(PlayerGrowthDelta delta, int value) { if (delta != null) delta.AddSpeedRate += value; }
    public static void AddLuck(PlayerGrowthDelta delta, int value) { if (delta != null) delta.AddLuck += value; }

    public static void Reset(PlayerGrowthDelta delta)
    {
        if (delta == null) return;
        delta.AddMaxHP = 0f;
        delta.AddAttack = 0f;
        delta.AddDefense = 0f;
        delta.AddCritChance = 0f;
        delta.AddCritMultiplier = 0f;
        delta.AddSpeedRate = 0;
        delta.AddLuck = 0;
    }
}


