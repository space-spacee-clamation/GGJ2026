/// <summary>
/// 行动结算后（已扣血）触发：用于“改变当前生命值”等需要拿到最终伤害的效果。
/// </summary>
public interface IMaterialDamageAppliedEffect
{
    void OnDamageApplied(FightContext context, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage);
}


