using UnityEngine;

public enum MaterialTraversePhase
{
    Bind = 0,
    BattleStart = 1,
    AttackModify = 2,
    PersistentGrowth = 3,
    Description = 4,
}

/// <summary>
/// 遍历上下文：供“每X回合/第X攻击/前X回合/前X攻击”等 gate 使用。
/// </summary>
public readonly struct MaterialTraverseContext
{
    public readonly MaterialTraversePhase Phase;
    public readonly FightContext Fight;
    public readonly FightSide Side;

    /// <summary>本次“回合序号”（从 1 开始）。</summary>
    public readonly int ActionNumber;

    /// <summary>本次攻击方“第几次攻击”（从 1 开始）。</summary>
    public readonly int AttackerAttackNumber;

    public MaterialTraverseContext(MaterialTraversePhase phase, FightContext fight, FightSide side, int actionNumber, int attackerAttackNumber)
    {
        Phase = phase;
        Fight = fight;
        Side = side;
        ActionNumber = Mathf.Max(0, actionNumber);
        AttackerAttackNumber = Mathf.Max(0, attackerAttackNumber);
    }
}


