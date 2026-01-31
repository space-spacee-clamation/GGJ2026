using System.Text;
using UnityEngine;

/// <summary>
/// Gate：第 X 次攻击允许执行（否则跳出）。
/// 取反：第 X 次攻击不执行（其它攻击执行）。
/// </summary>
public class Gate_AtXAttack : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Min(1)] public int AttackIndex = 1;
    [Tooltip("取反：第X次攻击不执行。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;
        if (AttackIndex <= 0) return false;
        if (context.AttackerAttackNumber <= 0) return false;

        bool allow = context.AttackerAttackNumber == AttackIndex;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.Append(Invert ? $"第{AttackIndex}次攻击不执行后续效果" : $"第{AttackIndex}次攻击执行后续效果");
    }
}


