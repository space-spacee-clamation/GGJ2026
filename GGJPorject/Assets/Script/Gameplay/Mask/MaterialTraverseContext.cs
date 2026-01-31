/// <summary>
/// 仅保留阶段枚举（旧的 TraverseContext 结构体已移除，统一使用 MaterialVommandeTreeContext）。
/// </summary>
public enum MaterialTraversePhase
{
    Bind = 0,
    BattleStart = 1,
    AttackModify = 2,
    DamageApplied = 3,
    BattleEnd = 4,
    PersistentGrowth = 5,
    Description = 6,
}


