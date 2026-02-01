using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 材质运行时执行器：遍历 MaterialObj.logicTreeRoots，并触发各阶段逻辑节点。
/// - 作为 IFightComponent：订阅战斗回调（BattleStart/BattleEnd/DamageApplied）
/// - 作为 IAttackInfoModifier：在攻击处理链中触发 “玩家/敌人攻击前” 阶段
/// </summary>
public sealed class MaterialRuntimeRunner : IFightComponent, IAttackInfoModifier
{
    private readonly MaterialObj _material;

    public MaterialRuntimeRunner(MaterialObj material)
    {
        _material = material;
    }

    public void Inject(FightContext context)
    {
        if (context == null) return;
        context.OnBattleStart += OnBattleStart;
        context.OnBattleEnd += OnBattleEnd;
        context.OnDamageApplied += OnDamageApplied;
    }

    private void OnBattleStart(FightContext context)
    {
        if (_material == null) return;
        if (_material.LogicTreeRoots == null || _material.LogicTreeRoots.Count == 0)
        {
            if (context != null && context.DebugVerbose)
            {
                context.DebugLogger?.Invoke($"[MaterialRuntimeRunner] {_material.name} 未配置 logicTreeRoots，跳过 BattleStart。");
            }
            return;
        }

        var treeCtx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.BattleStart,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: context,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TraverseTree_BattleStart(_material.LogicTreeRoots, in treeCtx);
    }

    private void TraverseTree_BattleStart(IReadOnlyList<MaterialLogicNode> nodes, in MaterialVommandeTreeContext tctx)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx))
            {
                LogGateBreak(tctx.Fight, "BattleStart(Tree)", g, tctx);
                continue; // break 仅影响分支：跳过 Children
            }

            if (c is IMaterialBattleStartEffect start)
            {
                LogEffect(tctx.Fight, "BattleStart(Tree)", c);
                start.OnBattleStart(tctx.Fight);
            }

            // 只有“逻辑节点（gate/空节点）”允许有子节点；效果节点不遍历 children
            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTree_BattleStart(n.Children, in tctx);
            }
        }
    }

    private void OnBattleEnd(FightContext context)
    {
        if (_material == null) return;
        if (_material.LogicTreeRoots == null || _material.LogicTreeRoots.Count == 0)
        {
            if (context != null && context.DebugVerbose)
            {
                context.DebugLogger?.Invoke($"[MaterialRuntimeRunner] {_material.name} 未配置 logicTreeRoots，跳过 BattleEnd。");
            }
            return;
        }

        var treeCtx = new MaterialVommandeTreeContext(
            MaterialTraversePhase.BattleEnd,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: FightSide.None,
            defenderSide: FightSide.None,
            actionNumber: context != null ? context.BattleActionCount : 0,
            attackerAttackNumber: 0,
            attackInfo: default,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TraverseTree_BattleEnd(_material.LogicTreeRoots, in treeCtx);
    }

    private void TraverseTree_BattleEnd(IReadOnlyList<MaterialLogicNode> nodes, in MaterialVommandeTreeContext tctx)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx))
            {
                LogGateBreak(tctx.Fight, "BattleEnd(Tree)", g, tctx);
                continue;
            }

            if (c is IMaterialBattleEndEffect end)
            {
                LogEffect(tctx.Fight, "BattleEnd(Tree)", c);
                end.OnBattleEnd(tctx.Fight);
            }

            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTree_BattleEnd(n.Children, in tctx);
            }
        }
    }

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (_material == null || context == null) return;
        if (_material.LogicTreeRoots == null || _material.LogicTreeRoots.Count == 0)
        {
            if (context != null && context.DebugVerbose)
            {
                context.DebugLogger?.Invoke($"[MaterialRuntimeRunner] {_material.name} 未配置 logicTreeRoots，跳过 AttackModify。");
            }
            return;
        }

        var phase = context.CurrentAttackerSide == FightSide.Enemy
            ? MaterialTraversePhase.EnemyAttackBefore
            : MaterialTraversePhase.PlayerAttackBefore;

        var treeCtx = new MaterialVommandeTreeContext(
            phase,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: context.CurrentAttackerSide,
            actionNumber: context.CurrentActionNumber,
            attackerAttackNumber: context.CurrentAttackerAttackNumber,
            defenderSide: context.CurrentAttackerSide == FightSide.Player ? FightSide.Enemy : FightSide.Player,
            attackInfo: info,
            damage: 0f,
            player: null,
            growthDelta: null
        );
        TraverseTree_AttackModify(_material.LogicTreeRoots, ref info, in treeCtx);
    }

    private void TraverseTree_AttackModify(IReadOnlyList<MaterialLogicNode> nodes, ref AttackInfo info, in MaterialVommandeTreeContext tctx)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx))
            {
                LogGateBreak(tctx.Fight, "AttackModify(Tree)", g, tctx);
                continue;
            }

            if (n.ActionSide == MaterialActionSideFilter.PlayerOnly && tctx.Side != FightSide.Player) continue;
            if (n.ActionSide == MaterialActionSideFilter.EnemyOnly && tctx.Side != FightSide.Enemy) continue;

            if (c is IAttackInfoModifier mod2)
            {
                LogEffect(tctx.Fight, "AttackModify(Tree)", c);
                mod2.Modify(ref info, tctx.Fight);
            }

            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTree_AttackModify(n.Children, ref info, in tctx);
            }
        }
    }

    private void OnDamageApplied(FightContext context, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage)
    {
        if (_material == null || context == null) return;
        if (_material.LogicTreeRoots == null || _material.LogicTreeRoots.Count == 0) return;

        var phase = attackerSide == FightSide.Enemy
            ? MaterialTraversePhase.EnemyAttackAfter
            : MaterialTraversePhase.PlayerAttackAfter;

        var tctx = new MaterialVommandeTreeContext(
            phase,
            mask: null,
            maskMaterials: null,
            onMaterialBound: null,
            fight: null,
            side: attackerSide,
            defenderSide: defenderSide,
            actionNumber: context.CurrentActionNumber,
            attackerAttackNumber: context.CurrentAttackerAttackNumber,
            attackInfo: info,
            damage: damage,
            player: null,
            growthDelta: null
        );
        TraverseTree_DamageApplied(_material.LogicTreeRoots, attackerSide, defenderSide, info, damage, in tctx);
    }

    private void TraverseTree_DamageApplied(IReadOnlyList<MaterialLogicNode> nodes, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage, in MaterialVommandeTreeContext tctx)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx))
            {
                LogGateBreak(tctx.Fight, "DamageApplied(Tree)", g, tctx);
                continue;
            }

            // 行动侧筛选
            if (n.ActionSide == MaterialActionSideFilter.PlayerOnly && attackerSide != FightSide.Player) continue;
            if (n.ActionSide == MaterialActionSideFilter.EnemyOnly && attackerSide != FightSide.Enemy) continue;

            if (c is IMaterialDamageAppliedEffect e)
            {
                LogEffect(tctx.Fight, "DamageApplied(Tree)", c);
                e.OnDamageApplied(tctx.Fight, attackerSide, defenderSide, info, damage);
            }
            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTree_DamageApplied(n.Children, attackerSide, defenderSide, info, damage, in tctx);
            }
        }
    }

    private void LogGateBreak(FightContext context, string phase, IMaterialTraversalGate gate, in MaterialVommandeTreeContext tctx)
    {
        if (context == null || !context.DebugVerbose || context.DebugLogger == null) return;
        var name = _material != null ? (!string.IsNullOrWhiteSpace(_material.DisplayName) ? _material.DisplayName : _material.name) : "nullMaterial";
        context.DebugLogger($"[MatGate] {name} phase={phase}({ToCnPhase(tctx.Phase, tctx.Side)}) gate={gate.GetType().Name} BREAK action={tctx.ActionNumber} atk#{tctx.AttackerAttackNumber} side={tctx.Side}");
    }

    private static string ToCnPhase(MaterialTraversePhase phase, FightSide side)
    {
        switch (phase)
        {
            case MaterialTraversePhase.Bind:
                return "绑定";
            case MaterialTraversePhase.BattleStart:
                return "战斗开始";
            case MaterialTraversePhase.AttackModify:
                return "攻击前（Legacy）";
            case MaterialTraversePhase.DamageApplied:
                return "攻击后（Legacy）";
            case MaterialTraversePhase.PlayerAttackBefore:
                return "玩家攻击前";
            case MaterialTraversePhase.PlayerAttackAfter:
                return "玩家攻击后";
            case MaterialTraversePhase.EnemyAttackBefore:
                return "敌人攻击前";
            case MaterialTraversePhase.EnemyAttackAfter:
                return "敌人攻击后";
            case MaterialTraversePhase.BattleEnd:
                return "战斗结束";
            case MaterialTraversePhase.PersistentGrowth:
                return "持久成长结算";
            case MaterialTraversePhase.Description:
                return "描述";
            default:
                return phase.ToString();
        }
    }

    private void LogEffect(FightContext context, string phase, MonoBehaviour comp)
    {
        if (context == null || !context.DebugVerbose || context.DebugLogger == null) return;
        if (comp == null) return;

        var matName = _material != null ? (!string.IsNullOrWhiteSpace(_material.DisplayName) ? _material.DisplayName : _material.name) : "nullMaterial";
        var desc = BuildComponentDesc(comp);
        if (string.IsNullOrWhiteSpace(desc))
        {
            context.DebugLogger($"[MatFx] {matName} phase={phase} comp={comp.GetType().Name}");
        }
        else
        {
            context.DebugLogger($"[MatFx] {matName} phase={phase} comp={comp.GetType().Name} desc={desc}");
        }
    }

    private static string BuildComponentDesc(MonoBehaviour comp)
    {
        try
        {
            if (comp is IMaterialDescriptionProvider p)
            {
                var sb = new StringBuilder(128);
                p.AppendDescription(sb);
                return sb.ToString().Trim().Replace("\n", " | ").Replace("\r", "");
            }
        }
        catch { /* ignore */ }
        return string.Empty;
    }
}


