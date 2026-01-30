using System.Text;
using UnityEngine;

/// <summary>
/// Gate：每 X 回合允许执行（否则跳出）。
/// 取反：每 X 回合不执行（其它回合执行）。
/// </summary>
public class Gate_EveryXTurns : MonoBehaviour, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)] public int EveryX = 2;
    [Tooltip("取反：每X回合不执行。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialTraverseContext context)
    {
        if (EveryX <= 0) return false;
        if (context.ActionNumber <= 0) return false; // 非攻击阶段通常为 0，不拦截

        bool allow = (context.ActionNumber % EveryX) == 0;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert ? $"每{EveryX}回合不执行后续效果" : $"每{EveryX}回合执行后续效果");
    }
}


