using System.Text;
using UnityEngine;

/// <summary>
/// Gate：战斗开始时执行后续效果；其它阶段直接跳出。
/// 用于把“战斗开始时”作为可组合的词条前缀，而不是写死在效果组件里。
/// </summary>
public class Gate_BattleStart : MonoBehaviour, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Tooltip("取反：战斗开始时不执行后续效果（其它阶段执行）。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialTraverseContext context)
    {
        // 描述阶段不跳出，避免截断文案
        if (context.Phase == MaterialTraversePhase.Description) return false;

        bool allow = context.Phase == MaterialTraversePhase.BattleStart;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert ? "战斗开始时不执行后续效果" : "战斗开始时：");
    }
}


