using System;

[Serializable]
public enum MaterialLogicNodeRole
{
    /// <summary>自动：根据 Component 是否实现 IMaterialTraversalGate 推断为 Condition，否则为 Action。</summary>
    Auto = 0,
    Condition = 1,
    Action = 2,
}


