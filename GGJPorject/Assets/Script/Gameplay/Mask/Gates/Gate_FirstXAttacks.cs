using System.Text;
using UnityEngine;

/// <summary>
/// Gate：前 X 次攻击允许执行（否则跳出）。
/// 取反：前 X 次攻击不执行（从第 X+1 次开始执行）。
/// </summary>
public class Gate_FirstXAttacks : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)] public int FirstX = 2;
    [Tooltip("取反：前X次攻击不执行。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;
        if (FirstX <= 0) return false;
        if (context.AttackerAttackNumber <= 0) return false;

        bool allow = context.AttackerAttackNumber <= FirstX;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert ? $"前{FirstX}次攻击不执行后续效果" : $"前{FirstX}次攻击执行后续效果");
    }
}


