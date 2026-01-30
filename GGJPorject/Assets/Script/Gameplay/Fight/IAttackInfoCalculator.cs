public interface IAttackInfoCalculator
{
    /// <summary>
    /// 数值计算入口（V0 占位）：允许通过 ref 修改 AttackInfo，并依赖 FightContext 获取当前攻击方/防守方运行时数据。
    /// </summary>
    void Calculate(ref AttackInfo info, FightContext context);
}


