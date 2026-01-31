/// <summary>
/// 逻辑树节点标记接口：
/// - 只有实现该接口的组件，才允许被 MaterialObj.logicTreeRoots 引用并参与运行时注入/执行。
/// - “效果器”不再作为独立注入接口对象存在；它必须以“逻辑节点”的形式出现（即实现本接口，并在内部根据阶段执行）。
/// </summary>
public interface IMaterialLogicNode
{
}

/// <summary>
/// 材质节点/效果器的中文元数据（用于策划友好的显示与中文关键词检索）。
/// - Name：中文名（用于显示）
/// - Keywords：关键词（用于检索，建议用空格分隔）
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MaterialCnMetaAttribute : System.Attribute
{
    public string Name { get; }
    public string Keywords { get; }

    public MaterialCnMetaAttribute(string name, string keywords = "")
    {
        Name = name ?? string.Empty;
        Keywords = keywords ?? string.Empty;
    }
}

/// <summary>
/// 纯“效果器”：只负责根据统一上下文执行效果。
/// - 不实现 IMaterialBindEffect / IMaterialBattleStartEffect 等注入接口
/// - 注入/触发由“逻辑节点（IMaterialLogicNode）”负责
/// </summary>
public interface IMaterialEffect
{
    void Execute(in MaterialVommandeTreeContext context);
}

/// <summary>
/// AttackModify 阶段的纯效果器：允许修改 AttackInfo（ref）。
/// </summary>
public interface IMaterialAttackInfoEffect
{
    void Modify(ref AttackInfo info, in MaterialVommandeTreeContext context);
}


