/// <summary>
/// “战斗组件”接口：在开战时注入 FightContext 以订阅回调/注册处理器/做战斗级逻辑。
/// 用于承接文档中“单独抽出来做一个类叫做战斗组件”的要求。
/// </summary>
public interface IFightComponent
{
    void Inject(FightContext context);
}


