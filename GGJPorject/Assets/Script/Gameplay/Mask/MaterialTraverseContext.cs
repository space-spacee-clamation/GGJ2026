/// <summary>
/// 仅保留阶段枚举（旧的 TraverseContext 结构体已移除，统一使用 MaterialVommandeTreeContext）。
/// </summary>
public enum MaterialTraversePhase
{
    Bind = 0,
    BattleStart = 1,
    // Legacy（旧配置仍可能使用，运行时会映射到 Player/Enemy 具体事件）
    AttackModify = 2,
    DamageApplied = 3,
    BattleEnd = 4,
    PersistentGrowth = 5,
    Description = 6,

    // 新阶段：明确区分玩家/敌人的“攻击前/攻击后”
    PlayerAttackBefore = 7,
    PlayerAttackAfter = 8,
    EnemyAttackBefore = 9,
    EnemyAttackAfter = 10,
}


