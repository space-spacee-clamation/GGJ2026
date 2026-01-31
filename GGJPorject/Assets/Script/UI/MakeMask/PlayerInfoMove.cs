using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 底部数据区的鼠标悬停动画控制器。
/// 鼠标靠近时移出，鼠标离开时退回。
/// </summary>
public class PlayerInfoMove : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Bottom Data Area")]
    [SerializeField] private RectTransform bottomDataArea; // 底部数据区的 RectTransform
    [SerializeField] private RectTransform showPosition; // 移出到达的位置
    [SerializeField] private RectTransform hidePosition; // 移入到达的位置（隐藏位置）
    [SerializeField] private float animationDuration = 0.3f; // 动画时长

    [Header("Debug")]
    [SerializeField] private bool enableLogs = false;

    private Tween _bottomDataAreaTween; // 底部数据区的动画
    private bool _isBottomDataAreaVisible; // 底部数据区是否可见

    private void OnEnable()
    {
        // 初始化底部数据区位置（隐藏状态）
        InitializeBottomDataArea();
    }

    private void OnDisable()
    {
        // 清理动画
        _bottomDataAreaTween?.Kill();
        _bottomDataAreaTween = null;
    }

    /// <summary>
    /// 初始化底部数据区位置（设置为隐藏位置）
    /// </summary>
    private void InitializeBottomDataArea()
    {
        if (bottomDataArea == null || hidePosition == null)
        {
            if (enableLogs)
            {
                if (bottomDataArea == null) Debug.LogWarning("[PlayerInfoMove] bottomDataArea 未绑定。", this);
                if (hidePosition == null) Debug.LogWarning("[PlayerInfoMove] hidePosition 未绑定。", this);
            }
            return;
        }
        
        // 设置初始位置为隐藏位置
        bottomDataArea.anchoredPosition = hidePosition.anchoredPosition;
        _isBottomDataAreaVisible = false;
    }

    /// <summary>
    /// 显示底部数据区（移出动画）
    /// </summary>
    private void ShowBottomDataArea()
    {
        if (bottomDataArea == null || showPosition == null) return;
        if (_isBottomDataAreaVisible) return; // 已经显示，不需要重复动画

        // 停止当前动画
        _bottomDataAreaTween?.Kill();

        // 移动到显示位置
        _bottomDataAreaTween = bottomDataArea.DOAnchorPos(showPosition.anchoredPosition, animationDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => {
                _isBottomDataAreaVisible = true;
                _bottomDataAreaTween = null;
            });
    }

    /// <summary>
    /// 隐藏底部数据区（移入动画）
    /// </summary>
    private void HideBottomDataArea()
    {
        if (bottomDataArea == null || hidePosition == null) return;
        if (!_isBottomDataAreaVisible) return; // 已经隐藏，不需要重复动画

        // 停止当前动画
        _bottomDataAreaTween?.Kill();

        // 移动到隐藏位置
        _bottomDataAreaTween = bottomDataArea.DOAnchorPos(hidePosition.anchoredPosition, animationDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .OnComplete(() => {
                _isBottomDataAreaVisible = false;
                _bottomDataAreaTween = null;
            });
    }

    /// <summary>
    /// 鼠标进入事件（IPointerEnterHandler）
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowBottomDataArea();
    }

    /// <summary>
    /// 鼠标离开事件（IPointerExitHandler）
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        HideBottomDataArea();
    }
}

