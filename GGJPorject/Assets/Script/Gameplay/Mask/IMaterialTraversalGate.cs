/// <summary>
/// 遍历跳出接口：当返回 true 时，MaterialObj 在本次遍历中会提前 break（后续组件不再执行）。
/// </summary>
public interface IMaterialTraversalGate
{
    bool ShouldBreak(in MaterialTraverseContext context);
}



