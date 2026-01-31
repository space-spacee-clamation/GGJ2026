using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class FightManager : MonoBehaviour
{
    public static FightManager I { get; private set; }

    [Header("Injected (Interface via Odin)")]
    [OdinSerialize] private IAttackInfoModifier finalDamageCalculator = new FinalDamageCalculator();

    [Header("Optional Injection")]
    [Tooltip("如果场景中有面具对象（或面具实例持有者），可在此注入战斗上下文。")]
    [OdinSerialize] private IMaskBattleInjector maskBattleInjector;

    [Tooltip("可选：战斗组件（会在开战时注入 context，订阅回调/注册处理器）。")]
    [OdinSerialize] private List<IFightComponent> fightComponents = new();

    [Header("Loop")]
    [SerializeField] private bool autoStartOnPlay = false;
    [SerializeField] private bool enableLogs = false;
    [SerializeField] private bool forceLogsInJam = true;

    public FightContext Context { get; private set; }

    private bool _isFighting;
    private float _nextPlayerAttackTime;
    private float _nextEnemyAttackTime;

    /// <summary>
    /// 必须由 GameManager.Awake() 调用，符合“所有管理类只能在 GameManager 中实例化与初始化”的强约束。
    /// </summary>
    public void Initialize()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
    }

    public void SetMaskBattleInjector(IMaskBattleInjector injector)
    {
        maskBattleInjector = injector;
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartFight();
        }
    }

    [Button(ButtonSizes.Medium)]
    public void StartFight()
    {
        if (MonsterSpawnSystem.I == null)
        {
            Debug.LogError("[FightManager] MonsterSpawnSystem 未初始化，无法生成敌人。", this);
            return;
        }

        if (finalDamageCalculator == null)
        {
            Debug.LogError("[FightManager] finalDamageCalculator 未配置（IAttackInfoModifier，用于最终结算且必须最后执行）。", this);
            return;
        }

        if (Player.I == null)
        {
            Debug.LogError("[FightManager] Player 单例未初始化，无法开始战斗。", this);
            return;
        }

        var enemyCfg = MonsterSpawnSystem.I.Spawn(_battleRoundIndex, Context);
        if (enemyCfg == null)
        {
            Debug.LogError("[FightManager] MonsterSpawnSystem 未生成 Enemy 配置。", this);
            return;
        }

        Context = new FightContext
        {
            Player = CombatantRuntime.FromStats("Player", Player.I.BuildBattleStats(),GameManager.I.PendingGrowthDelta),
            Enemy = new CombatantRuntime("Enemy", enemyCfg),
            ArenaSpeedThreshold = Mathf.Max(1, GameSetting.DefaultArenaSpeedThreshold),
        };
        var logs = (enableLogs || forceLogsInJam);
        Context.DebugVerbose = logs;
        Context.DebugLogger = logs ? (System.Action<string>)(msg => Debug.Log(msg)) : null;

        // 注入“战斗组件”
        if (fightComponents != null)
        {
            for (int i = 0; i < fightComponents.Count; i++)
            {
                Context.AddFightComponent(fightComponents[i]);
            }
        }

        // 注入“面具/材料”（占位接口）
        if (maskBattleInjector != null)
        {
            Context.MaskInjector = maskBattleInjector;
            maskBattleInjector.InjectBattleContext(Context);
            Context.MaskCount = (maskBattleInjector as IMaskBattleInjectorWithCount)?.MaskCount ?? 0;
        }

        // 最终结算器：永远最后执行（不允许被材料动态 Add 插到后面）
        Context.PlayerAttackProcessor.SetFinalizer(finalDamageCalculator);
        Context.EnemyAttackProcessor.SetFinalizer(finalDamageCalculator);

        // 速度条初始化
        Context.PlayerSpeedValue = 0f;
        Context.EnemySpeedValue = 0f;
        _nextPlayerAttackTime = Time.time;
        _nextEnemyAttackTime = Time.time;
        _isFighting = true;

        Context.RaiseBattleEnter();
        Context.RaiseBattleStart();

        if (logs)
        {
            DumpBattleHeader();
        }

        _battleRoundIndex++;
    }

    private int _battleRoundIndex = 0;

    public void StopFight()
    {
        _isFighting = false;
    }

    private void Update()
    {
        if (!_isFighting || Context == null) return;
        if (Context.Player == null || Context.Enemy == null) return;

        if (Context.Player.IsDead || Context.Enemy.IsDead)
        {
            EndFight();
            return;
        }

        // 速度累积
        Context.PlayerSpeedValue += Context.Player.SpeedRate * Time.deltaTime;
        Context.EnemySpeedValue += Context.Enemy.SpeedRate * Time.deltaTime;

        // 尝试触发攻击（允许溢出多次，但受攻击动画间隔限制）
        TryExecuteAttacks(FightSide.Player);
        TryExecuteAttacks(FightSide.Enemy);

        if (Context.Player.IsDead || Context.Enemy.IsDead)
        {
            EndFight();
        }
    }

    private void TryExecuteAttacks(FightSide side)
    {
        int threshold = Mathf.Max(1, Context.ArenaSpeedThreshold);

        if (side == FightSide.Player)
        {
            if (Time.time < _nextPlayerAttackTime) return;
            if (Context.PlayerSpeedValue < threshold) return;

            ExecuteAttack(FightSide.Player);
            Context.PlayerSpeedValue -= threshold;
            _nextPlayerAttackTime = Time.time + GameSetting.AttackAnimIntervalSeconds;
        }
        else
        {
            if (Time.time < _nextEnemyAttackTime) return;
            if (Context.EnemySpeedValue < threshold) return;

            ExecuteAttack(FightSide.Enemy);
            Context.EnemySpeedValue -= threshold;
            _nextEnemyAttackTime = Time.time + GameSetting.AttackAnimIntervalSeconds;
        }
    }

    private void ExecuteAttack(FightSide side)
    {
        var attacker = side == FightSide.Player ? Context.Player : Context.Enemy;
        var defender = side == FightSide.Player ? Context.Enemy : Context.Player;
        var defenderSide = side == FightSide.Player ? FightSide.Enemy : FightSide.Player;

        // 设置本次“交战上下文”，供 Calculator/材料读取
        Context.CurrentAttackerSide = side;
        Context.CurrentAttacker = attacker;
        Context.CurrentDefender = defender;

        // 计数器：供“每X回合/第X攻击/前X回合/前X攻击”类词条使用
        Context.CurrentActionNumber = Context.BattleActionCount + 1;
        if (side == FightSide.Player) Context.CurrentAttackerAttackNumber = Context.PlayerAttackCount + 1;
        else if (side == FightSide.Enemy) Context.CurrentAttackerAttackNumber = Context.EnemyAttackCount + 1;
        else Context.CurrentAttackerAttackNumber = 0;

        // 创建 AttackInfo（每次攻击创建）
        var info = new AttackInfo
        {
            BaseValue = attacker.Attack,
            CritChance = attacker.CritChance,
            CritMultiplier = attacker.CritMultiplier,
            RawAttack = attacker.Attack,
            PenetrationFixed = attacker.PenetrationFixed,
            PenetrationPercent = attacker.PenetrationPercent,
            IsCrit = false,
            FinalDamage = 0f,
        };
        var beforeProc = info;

        // 处理链（材料链表顺序）
        if (side == FightSide.Player) Context.PlayerAttackProcessor.Process(ref info, Context);
        else Context.EnemyAttackProcessor.Process(ref info, Context);
        var afterProc = info;

        // 通知：攻击前（不用于修改）
        Context.RaiseBeforeAttack(side, info);

        // 通知：攻击后
        Context.RaiseAfterAttack(side, info);

        // 应用伤害（V0：如果 Calculator 没写 FinalDamage，则回退用 RawAttack）
        var damage = info.FinalDamage > 0f ? info.FinalDamage : info.RawAttack;
        defender.Damage(damage);

        // 通知：伤害已结算（给 UI 飘字等展示用）
        Context.RaiseDamageApplied(side, defenderSide, info, damage);

        // 结算后累加次数
        Context.BattleActionCount += 1;
        if (side == FightSide.Player) Context.PlayerAttackCount += 1;
        else if (side == FightSide.Enemy) Context.EnemyAttackCount += 1;

        if (Context != null && Context.DebugVerbose && Context.DebugLogger != null)
        {
            // afterProc 即“最终结算后”的 info（因为最终结算器在处理链末尾）
            DumpAttackVerbose(side, attacker, defender, beforeProc, afterProc, afterProc, damage);
        }
    }

    private void EndFight()
    {
        if (!_isFighting) return;
        _isFighting = false;

        Context.RaiseBattleEnd();
        if (!Context.Player.IsDead && Context.Enemy.IsDead) Context.RaiseVictory();
        else Context.RaiseDefeat();

        if (Context != null && Context.DebugVerbose && Context.DebugLogger != null)
        {
            var result = (!Context.Player.IsDead && Context.Enemy.IsDead) ? "Victory" : "Defeat";
            Context.DebugLogger($"[Fight] BattleEnd Result={result} PlayerHP={FmtHP(Context.Player)} EnemyHP={FmtHP(Context.Enemy)} Actions={Context.BattleActionCount} PAtk={Context.PlayerAttackCount} EAtk={Context.EnemyAttackCount}");
        }
    }

    private void DumpBattleHeader()
    {
        var c = Context;
        if (c == null || c.DebugLogger == null) return;
        c.DebugLogger($"[Fight] BattleStart ArenaSpeed={c.ArenaSpeedThreshold} " +
                      $"P({FmtCore(c.Player)} SpeedValue={c.PlayerSpeedValue:0.0}) " +
                      $"E({FmtCore(c.Enemy)} SpeedValue={c.EnemySpeedValue:0.0}) " +
                      $"PlayerMods={c.PlayerAttackProcessor.Modifiers.Count} EnemyMods={c.EnemyAttackProcessor.Modifiers.Count} FightComponents={c.FightComponents.Count}");
    }

    private void DumpAttackVerbose(FightSide side, CombatantRuntime attacker, CombatantRuntime defender,
        AttackInfo beforeProc, AttackInfo afterProc, AttackInfo afterCalc, float damage)
    {
        var c = Context;
        if (c == null || c.DebugLogger == null) return;
        string who = side == FightSide.Player ? "Player" : "Enemy";
        c.DebugLogger($"[Fight] ---- Attack {c.CurrentActionNumber} ({who} #{c.CurrentAttackerAttackNumber}) ----");
        c.DebugLogger($"[Fight] SpeedValue P={c.PlayerSpeedValue:0.0}/{c.ArenaSpeedThreshold} E={c.EnemySpeedValue:0.0}/{c.ArenaSpeedThreshold}");
        c.DebugLogger($"[Fight] Attacker {attacker.Name} {FmtCore(attacker)}");
        c.DebugLogger($"[Fight] Defender {defender.Name} {FmtCore(defender)}");
        c.DebugLogger($"[Fight] AttackInfo BeforeProc {FmtInfo(beforeProc)}");
        c.DebugLogger($"[Fight] AttackInfo AfterProc  {FmtInfo(afterProc)}");
        c.DebugLogger($"[Fight] AttackInfo AfterCalc {FmtInfo(afterCalc)}");
        c.DebugLogger($"[Fight] Result damage={damage:0.##} DefenderHP={FmtHP(defender)}");
    }

    private static string FmtHP(CombatantRuntime c)
    {
        if (c == null) return "null";
        return $"{c.CurrentHP:0.##}/{c.MaxHP:0.##}";
    }

    private static string FmtCore(CombatantRuntime c)
    {
        if (c == null) return "null";
        return $"HP={FmtHP(c)} ATK={c.Attack:0.##} DEF={c.Defense:0.##} Crit={c.CritChance:0.##} CritMul={c.CritMultiplier:0.##} SpeedRate={c.SpeedRate}";
    }

    private static string FmtInfo(AttackInfo i)
    {
        return $"Base={i.BaseValue:0.##} Raw={i.RawAttack:0.##} CritChance={i.CritChance:0.##} CritMul={i.CritMultiplier:0.##} IsCrit={i.IsCrit} Final={i.FinalDamage:0.##}";
    }
}
