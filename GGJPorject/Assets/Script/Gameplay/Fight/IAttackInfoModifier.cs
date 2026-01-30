public interface IAttackInfoModifier
{
    /// <summary>
    /// 修改本次攻击信息（材料效果/战斗组件等可实现此接口）。
    /// 注意：修改顺序以“材料链表顺序/注册顺序”为准。
    /// </summary>
    void Modify(ref AttackInfo info, FightContext context);
}


