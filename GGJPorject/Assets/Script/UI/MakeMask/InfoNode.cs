using TMPro;
using UnityEngine;

public class InfoNode : MonoBehaviour
{
    [Header("TMP")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI ttlText;

    public void Show(string name, string desc, int remainingTurns)
    {
        if (nameText != null) nameText.text = name ?? string.Empty;
        if (descText != null) descText.text = desc ?? string.Empty;
        if (ttlText != null) ttlText.text = $"保质期：{remainingTurns}";
    }

    public void Clear()
    {
        if (nameText != null) nameText.text = string.Empty;
        if (descText != null) descText.text = string.Empty;
        if (ttlText != null) ttlText.text = string.Empty;
    }
}
