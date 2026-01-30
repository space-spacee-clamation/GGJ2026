using UnityEngine;

/// <summary>
/// 示例：战斗生效材料效果 - 增加本次攻击的 RawAttack。
/// </summary>
public sealed class AddRawAttackModifier : MonoBehaviour, IAttackInfoModifier
{
    [SerializeField] private FightSide appliesTo = FightSide.Player;
    [SerializeField] private float addValue = 1f;

    public void Modify(ref AttackInfo info, FightContext context)
    {
        if (context == null) return;
        if (appliesTo != FightSide.None && context.CurrentAttackerSide != appliesTo) return;
        info.RawAttack += addValue;
    }
}


