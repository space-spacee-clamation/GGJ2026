using System.Text;
using UnityEngine;

/// <summary>
/// 示例：即时生效材料效果 - 恢复面具法力值。
/// </summary>
[MaterialCnMeta("恢复法力", "回蓝 法力 恢复 绑定")]
public sealed class RestoreManaImmediateEffect : MonoBehaviour, IMaterialEffect, IMaterialDescriptionProvider
{
    [Min(1)] public int RestoreAmount = 1;

    public void Execute(in MaterialVommandeTreeContext context)
    {
        context.Mask?.AddMana(RestoreAmount);
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        sb.Append($"绑定时：恢复法力值 +{RestoreAmount}");
    }
}


