using System.Text;
using UnityEngine;

/// <summary>
/// Gate：按概率决定是否执行后续分支（否则跳过该分支）。
/// </summary>
public sealed class Gate_RandomChance : MonoBehaviour, IMaterialLogicNode, IMaterialTraversalGate, IMaterialDescriptionProvider
{
    [Range(0f, 1f)]
    [Tooltip("通过概率：0~1。每次触发都会重新抽一次。")]
    public float Chance01 = 0.5f;

    [Tooltip("取反：在未通过时执行（通过时不执行）。")]
    public bool Invert = false;

    public bool ShouldBreak(in MaterialVommandeTreeContext context)
    {
        if (context.Phase == MaterialTraversePhase.Description) return false;
        var c = Mathf.Clamp01(Chance01);
        bool allow = Random.value <= c;
        if (Invert) allow = !allow;
        return !allow;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var c = Mathf.Clamp01(Chance01);
        sb.Append(Invert ? $"未触发（{c:P0}）时执行后续效果" : $"{c:P0} 概率执行后续效果");
    }
}


