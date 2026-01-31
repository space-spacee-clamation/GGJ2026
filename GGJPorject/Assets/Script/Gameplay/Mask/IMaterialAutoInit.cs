public interface IMaterialAutoInit
{
    /// <summary>
    /// MaterialObj.Awake() 中自动调用，用于给子组件一个拿到 owner 的入口（可选）。
    /// </summary>
    void Initialize(MaterialObj owner);
}



