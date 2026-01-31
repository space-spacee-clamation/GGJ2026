public interface IMaterialBindEffect
{
    /// <summary>
    /// 绑定到面具阶段执行（即时生效）。
    /// </summary>
    void OnBind(in BindContext context);
}



