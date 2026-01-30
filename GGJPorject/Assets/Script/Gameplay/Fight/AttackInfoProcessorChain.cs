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
        for (int i = 0; i < _modifiers.Count; i++)
        {
            _modifiers[i].Modify(ref info, context);
        }
    }
}


