using System.Collections.Generic;

/// <summary>
/// 攻击处理器链（玩家/敌人各一套）。材料效果按“链表顺序”注册到链中执行。
/// </summary>
public sealed class AttackInfoProcessorChain
{
    private readonly List<IAttackInfoModifier> _modifiers = new();

    public IReadOnlyList<IAttackInfoModifier> Modifiers => _modifiers;

    public void Clear() => _modifiers.Clear();

    public void Add(IAttackInfoModifier modifier)
    {
        if (modifier == null) return;
        _modifiers.Add(modifier);
    }

    public void Process(ref AttackInfo info, FightContext context)
    {
        if (context != null && context.DebugVerbose && context.DebugLogger != null)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m == null) continue;
                var before = info;
                m.Modify(ref info, context);
                if (!ApproxEqual(before, info))
                {
                    context.DebugLogger($"[Proc] {m.GetType().Name} {FmtDiff(before, info)}");
                }
                else
                {
                    context.DebugLogger($"[Proc] {m.GetType().Name} (no change)");
                }
            }
            return;
        }

        for (int i = 0; i < _modifiers.Count; i++)
        {
            _modifiers[i].Modify(ref info, context);
        }
    }

    private static bool ApproxEqual(AttackInfo a, AttackInfo b)
    {
        const float eps = 0.0001f;
        if (System.Math.Abs(a.BaseValue - b.BaseValue) > eps) return false;
        if (System.Math.Abs(a.RawAttack - b.RawAttack) > eps) return false;
        if (System.Math.Abs(a.CritChance - b.CritChance) > eps) return false;
        if (System.Math.Abs(a.CritMultiplier - b.CritMultiplier) > eps) return false;
        if (a.IsCrit != b.IsCrit) return false;
        if (System.Math.Abs(a.FinalDamage - b.FinalDamage) > eps) return false;
        return true;
    }

    private static string FmtDiff(AttackInfo before, AttackInfo after)
    {
        return $"Base {before.BaseValue:0.##}->{after.BaseValue:0.##}, Raw {before.RawAttack:0.##}->{after.RawAttack:0.##}, " +
               $"CritChance {before.CritChance:0.##}->{after.CritChance:0.##}, CritMul {before.CritMultiplier:0.##}->{after.CritMultiplier:0.##}, " +
               $"IsCrit {before.IsCrit}->{after.IsCrit}, Final {before.FinalDamage:0.##}->{after.FinalDamage:0.##}";
    }
}


