using System;
using System.Collections.Generic;

/// <summary>
/// 战斗上下文（BattleContext）。
/// 承载战斗共享数据、回调入口、以及玩家/敌人的 AttackInfo 处理链。
/// </summary>
public sealed class FightContext
{
    // ---- Debug ----
    /// <summary>是否输出详细调试日志（GameJam 期间可常开）。</summary>
    public bool DebugVerbose { get; internal set; }

    /// <summary>调试日志输出函数（由 FightManager 注入）。</summary>
    public Action<string> DebugLogger { get; internal set; }

    // ---- Runtime References ----
    public CombatantRuntime Player { get; internal set; }
    public CombatantRuntime Enemy { get; internal set; }

    // ---- Speed Battle ----
    public int ArenaSpeedThreshold { get; internal set; } = 10;
    public float PlayerSpeedValue { get; internal set; }
    public float EnemySpeedValue { get; internal set; }

    /// <summary>
    /// 本场战斗参与生效的面具数量（面具库 + 当前面具）。
    /// 用于“按面具数量缩放”的词条。
    /// </summary>
    public int MaskCount { get; internal set; }

    /// <summary>可选：面具实例（后续材料系统用）。</summary>
    public IMaskBattleInjector MaskInjector { get; internal set; }

    // ---- Current Engagement ----
    public FightSide CurrentAttackerSide { get; internal set; } = FightSide.None;
    public CombatantRuntime CurrentAttacker { get; internal set; }
    public CombatantRuntime CurrentDefender { get; internal set; }

    // ---- Counters (for material gates: 行动/攻击次数) ----
    /// <summary>本场战斗已发生的“行动次数”（每发生一次攻击 +1）。</summary>
    public int BattleActionCount { get; internal set; }

    /// <summary>本场战斗玩家已攻击次数。</summary>
    public int PlayerAttackCount { get; internal set; }

    /// <summary>本场战斗敌人已攻击次数。</summary>
    public int EnemyAttackCount { get; internal set; }

    /// <summary>本次攻击的“行动序号”（从 1 开始；每发生一次攻击事件 +1）。</summary>
    public int CurrentActionNumber { get; internal set; }

    /// <summary>本次攻击方的“第几次攻击”（从 1 开始）。</summary>
    public int CurrentAttackerAttackNumber { get; internal set; }

    // ---- Processors ----
    public AttackInfoProcessorChain PlayerAttackProcessor { get; } = new();
    public AttackInfoProcessorChain EnemyAttackProcessor { get; } = new();

    // ---- Fight Components (optional) ----
    private readonly List<IFightComponent> _fightComponents = new();
    public IReadOnlyList<IFightComponent> FightComponents => _fightComponents;

    public void AddFightComponent(IFightComponent component)
    {
        if (component == null) return;
        _fightComponents.Add(component);
        component.Inject(this);
    }

    // ---- Callbacks ----
    public event Action<FightContext> OnBattleEnter;
    public event Action<FightContext> OnBattleStart;
    public event Action<FightContext> OnBattleEnd;
    public event Action<FightContext> OnVictory;
    public event Action<FightContext> OnDefeat;

    /// <summary>攻击前/后通知（不用于修改；修改请走处理链/Calculator）。</summary>
    public event Action<FightContext, AttackInfo> OnBeforePlayerAttack;
    public event Action<FightContext, AttackInfo> OnAfterPlayerAttack;
    public event Action<FightContext, AttackInfo> OnBeforeEnemyAttack;
    public event Action<FightContext, AttackInfo> OnAfterEnemyAttack;

    /// <summary>
    /// 伤害结算通知：已对 Defender 扣血之后触发（用于 UI 飘字等展示）。
    /// attackerSide/defenderSide：本次攻击方/受击方。
    /// </summary>
    public event Action<FightContext, FightSide, FightSide, AttackInfo, float> OnDamageApplied;

    // ---- Raise helpers ----
    internal void RaiseBattleEnter() => OnBattleEnter?.Invoke(this);
    internal void RaiseBattleStart() => OnBattleStart?.Invoke(this);
    internal void RaiseBattleEnd() => OnBattleEnd?.Invoke(this);
    internal void RaiseVictory() => OnVictory?.Invoke(this);
    internal void RaiseDefeat() => OnDefeat?.Invoke(this);

    internal void RaiseBeforeAttack(FightSide side, AttackInfo info)
    {
        if (side == FightSide.Player) OnBeforePlayerAttack?.Invoke(this, info);
        else if (side == FightSide.Enemy) OnBeforeEnemyAttack?.Invoke(this, info);
    }

    internal void RaiseAfterAttack(FightSide side, AttackInfo info)
    {
        if (side == FightSide.Player) OnAfterPlayerAttack?.Invoke(this, info);
        else if (side == FightSide.Enemy) OnAfterEnemyAttack?.Invoke(this, info);
    }

    internal void RaiseDamageApplied(FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage)
    {
        OnDamageApplied?.Invoke(this, attackerSide, defenderSide, info, damage);
    }
}
