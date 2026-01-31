using System.Text;
using UnityEngine;

/// <summary>
/// Gate：战斗结束时执行后续效果；其它阶段直接跳出。
/// 注意：当前项目的“持久成长收集”也视为战斗结束阶段（会用 BattleEnd 作为遍历 phase）。
/// </summary>
public class Gate_BattleEnd : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Tooltip("取反：战斗结束时不执行后续效果（其它阶段执行）。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        // 描述阶段不跳出，避免截断文案
        if (context.Phase == MaterialTraversePhase.Description) return false;

        bool allow = context.Phase == MaterialTraversePhase.BattleEnd;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(Invert ? "战斗结束时不执行后续效果" : "战斗结束时：");
    }
}



