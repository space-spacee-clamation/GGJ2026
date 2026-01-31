using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoNode : MonoBehaviour
{
    [Header("Sprite Display")]
    [Tooltip("显示材料 的 Image（优先显示）。")]
    [SerializeField] private Image spriteImage;

    [Header("TMP（可选）")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI ttlText;

    public void Show(string name, string desc, int remainingTurns)
    {
        Show(name, desc, remainingTurns, null);
    }

    public void Show(string name, string desc, int remainingTurns, Sprite sprite)
    {
        // 优先显示 Sprite
        if (spriteImage != null)
        {
            spriteImage.sprite = sprite;
            spriteImage.gameObject.SetActive(sprite != null);
        }

        // 文本信息（可选）
        if (nameText != null) nameText.text = name ?? string.Empty;
        if (descText != null) descText.text = desc ?? string.Empty;
        if (ttlText != null) ttlText.text = $"保质期：{remainingTurns}";
    }

    public void Clear()
    {
        if (spriteImage != null)
        {
            spriteImage.sprite = null;
            spriteImage.gameObject.SetActive(false);
        }
        if (nameText != null) nameText.text = string.Empty;
        if (descText != null) descText.text = string.Empty;
        if (ttlText != null) ttlText.text = string.Empty;
    }
}
