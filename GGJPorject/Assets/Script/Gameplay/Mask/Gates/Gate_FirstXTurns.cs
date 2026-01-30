using System.Text;
using UnityEngine;

/// <summary>
/// Gate：前 X 回合允许执行（否则跳出）。
/// 取反：前 X 回合不执行（从第 X+1 回合开始执行）。
/// </summary>
public class Gate_FirstXTurns : MonoBehaviour, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)] public int FirstX = 2;
    [Tooltip("取反：前X回合不执行。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialTraverseContext context)
    {
        if (FirstX <= 0) return false;
        if (context.ActionNumber <= 0) return false;

        bool allow = context.ActionNumber <= FirstX;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert ? $"前{FirstX}回合不执行后续效果" : $"前{FirstX}回合执行后续效果");
    }
}


