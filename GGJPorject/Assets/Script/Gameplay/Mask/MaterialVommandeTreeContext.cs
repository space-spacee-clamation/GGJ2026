using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一的材料逻辑树上下文（所有阶段共用）。
/// 设计目标：
/// - 不再拆分 BindContext/TraverseContext 等多套上下文
/// - 逻辑节点基于该上下文判断 Gate、并驱动效果器执行
/// </summary>
public readonly struct MaterialVommandeTreeContext
{
    public readonly MaterialTraversePhase Phase;

    // ---- Make/Bind ----
    public readonly MaskObj Mask;
    public readonly IReadOnlyList<MaterialObj> MaskMaterials;
    public readonly Action<MaterialObj> OnMaterialBound;

    // ---- Battle ----
    private readonly FightContext _fightOverride;
    public FightContext Fight => _fightOverride ?? FightManager.I?.Context;
    /// <summary>本次动作发起方（通常是 attackerSide）。</summary>
    public readonly FightSide Side;
    /// <summary>本次动作目标方（通常是 defenderSide；仅在 DamageApplied 等阶段有效）。</summary>
    public readonly FightSide DefenderSide;
    public readonly int ActionNumber;
    public readonly int AttackerAttackNumber;
    public readonly AttackInfo AttackInfo;
    public readonly float Damage;

    // ---- Settlement / Growth ----
    private readonly Player _playerOverride;
    public Player Player => _playerOverride ?? Player.I;
    public readonly PlayerGrowthDelta GrowthDelta => GameManager.I.PendingGrowthDelta;

    public MaterialVommandeTreeContext(
        MaterialTraversePhase phase,
        MaskObj mask,
        IReadOnlyList<MaterialObj> maskMaterials,
        Action<MaterialObj> onMaterialBound,
        FightContext fight,
        FightSide side,
        FightSide defenderSide,
        int actionNumber,
        int attackerAttackNumber,
        AttackInfo attackInfo,
        float damage,
        Player player,
        PlayerGrowthDelta growthDelta)
    {
        Phase = phase;
        Mask = mask;
        MaskMaterials = maskMaterials;
        OnMaterialBound = onMaterialBound;
        _fightOverride = fight;
        Side = side;
        DefenderSide = defenderSide;
        ActionNumber = Mathf.Max(0, actionNumber);
        AttackerAttackNumber = Mathf.Max(0, attackerAttackNumber);
        AttackInfo = attackInfo;
        Damage = damage;
        _playerOverride = player;
    }

    public static MaterialVommandeTreeContext ForDescription()
    {
        return new MaterialVommandeTreeContext(
            MaterialTraversePhase.Description,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
    }
}


