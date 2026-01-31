using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 纯 UI 伤害飘字：上飘 + 淡出，暴击时红色描边。
/// </summary>
public sealed class UIDamageText : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Outline outline;
    [SerializeField] private RectTransform rect;

    [Header("Style")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color critTextColor = Color.white;
    [SerializeField] private Color critOutlineColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Vector2 critOutlineDistance = new Vector2(2f, -2f);

    [Header("Anim")]
    [SerializeField] private float floatUp = 60f;
    [SerializeField] private float duration = 0.55f;

    private Tween _tween;

    private void Awake()
    {
        if (rect == null) rect = transform as RectTransform;
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (outline == null) outline = GetComponent<Outline>();
    }

    private void OnDestroy()
    {
        if (_tween != null && _tween.IsActive()) _tween.Kill();
    }

    public void Play(float damage, bool isCrit)
    {
        if (text != null)
        {
            text.text = Mathf.RoundToInt(damage).ToString();
            text.color = isCrit ? critTextColor : normalTextColor;
        }

        if (outline != null)
        {
            outline.enabled = isCrit;
            outline.effectColor = critOutlineColor;
            outline.effectDistance = critOutlineDistance;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        var startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        var endPos = startPos + new Vector2(0f, floatUp);

        _tween?.Kill();
        _tween = DOTween.Sequence()
            .SetUpdate(true) // Jam：即使 TimeScale=0 也能播 UI
            .Join(rect.DOAnchorPos(endPos, duration).SetEase(Ease.OutCubic))
            .Join(canvasGroup.DOFade(0f, duration).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject));
    }
}


