/// <summary>
/// 面具在进入战斗前的注入接口（V0 占位）。
/// 后续材料系统应在此把“材料链表”上的战斗效果注入到 FightContext 的回调与处理器链中。
/// </summary>
public interface IMaskBattleInjector
{
    void InjectBattleContext(FightContext context);
}

/// <summary>
/// 可选扩展：注入器可提供“面具数量”信息（面具库 + 当前面具）。
/// </summary>
public interface IMaskBattleInjectorWithCount : IMaskBattleInjector
{
    int MaskCount { get; }
}

