using System.Text;
using UnityEngine;

/// <summary>
/// 示例：即时生效材料效果 - 恢复面具法力值。
/// </summary>
public sealed class RestoreManaImmediateEffect : MonoBehaviour, IMaterialBindEffect, IMaterialDescriptionProvider
{
    [Min(1)] public int RestoreAmount = 1;
    [TextArea] [SerializeField] private string description = "绑定时：恢复法力值。";

    public void OnBind(in BindContext context)
    {
        context.Mask?.AddMana(RestoreAmount);
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine(description);
        }
    }
}


