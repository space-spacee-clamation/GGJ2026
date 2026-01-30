using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 材质运行时执行器：按 MaterialObj 的 orderedComponents 顺序执行，并支持 Gate 跳出。
/// - 作为 IFightComponent：订阅战斗开始回调
/// - 作为 IAttackInfoModifier：在攻击处理链中按顺序执行所有 IAttackInfoModifier 组件
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
    }

    private void OnBattleStart(FightContext context)
    {
        if (_material == null) return;
        var comps = _material.OrderedComponents;

        // 如果未配置 orderedComponents，MaterialObj 会在运行时 fallback GetComponents（但 OrderedComponents 可能为空）
        IReadOnlyList<MonoBehaviour> runtimeList = comps as IReadOnlyList<MonoBehaviour>;
        if (runtimeList == null || runtimeList.Count == 0)
        {
            // 触发一次描述生成会走 fallback 清理，复用其逻辑不划算；这里直接走 material 的内部 fallback
            // 通过 Bind 描述的 GetOrderedComponentsRuntime 是 private，因此保持简单：用 GetComponents 顺序兜底
            var bs = _material.GetComponents<MonoBehaviour>();
            var tmp = new List<MonoBehaviour>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                if (bs[i] is MaterialObj) continue;
                tmp.Add(bs[i]);
            }
            runtimeList = tmp;
        }

        var tctx = new MaterialTraverseContext(MaterialTraversePhase.BattleStart, context, FightSide.None, 0, 0);
        for (int i = 0; i < runtimeList.Count; i++)
        {
            var c = runtimeList[i];
            if (c == null) continue;
            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx)) break;
            if (c is IMaterialBattleStartEffect start) start.OnBattleStart(context);
        }
    }

    private void OnBattleEnd(FightContext context)
    {
        if (_material == null) return;

        IReadOnlyList<MonoBehaviour> runtimeList = _material.OrderedComponents;
        if (runtimeList == null || runtimeList.Count == 0)
        {
            var bs = _material.GetComponents<MonoBehaviour>();
            var tmp = new List<MonoBehaviour>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                if (bs[i] is MaterialObj) continue;
                tmp.Add(bs[i]);
            }
            runtimeList = tmp;
        }

        var tctx = new MaterialTraverseContext(MaterialTraversePhase.BattleEnd, context, FightSide.None, context.BattleActionCount, 0);
        for (int i = 0; i < runtimeList.Count; i++)
        {
            var c = runtimeList[i];
            if (c == null) continue;
            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx)) break;
            if (c is IMaterialBattleEndEffect end) end.OnBattleEnd(context);
        }
    }

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (_material == null || context == null) return;

        IReadOnlyList<MonoBehaviour> runtimeList = _material.OrderedComponents;
        if (runtimeList == null || runtimeList.Count == 0)
        {
            var bs = _material.GetComponents<MonoBehaviour>();
            var tmp = new List<MonoBehaviour>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                if (bs[i] is MaterialObj) continue;
                tmp.Add(bs[i]);
            }
            runtimeList = tmp;
        }

        var tctx = new MaterialTraverseContext(
            MaterialTraversePhase.AttackModify,
            context,
            context.CurrentAttackerSide,
            context.CurrentActionNumber,
            context.CurrentAttackerAttackNumber
        );

        for (int i = 0; i < runtimeList.Count; i++)
        {
            var c = runtimeList[i];
            if (c == null) continue;
            if (c is IMaterialTraversalGate g && g.ShouldBreak(in tctx)) break;
            if (c is IAttackInfoModifier mod) mod.Modify(ref info, context);
        }
    }
}


