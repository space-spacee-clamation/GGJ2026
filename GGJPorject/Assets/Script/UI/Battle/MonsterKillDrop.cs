using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物死亡掉落物表现类：从怪物位置移动到附近随机点，使用贝塞尔曲线，带有旋转动画。
/// </summary>
public sealed class MonsterKillDrop : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image materialImage;
    
    [Header("Drop Animation")]
    [Tooltip("掉落动画持续时间（秒）。")]
    [SerializeField] private float dropDuration = 1.5f;
    [Tooltip("掉落随机范围（从怪物位置向四周随机偏移的最大距离）。")]
    [SerializeField] private Vector2 randomRange = new Vector2(200f, 150f);
    [Tooltip("贝塞尔曲线控制点高度（相对于起始和终点连线的垂直偏移）。")]
    [SerializeField] private float bezierHeight = 100f;
    [Tooltip("Z 轴旋转幅度（随机范围）。")]
    [SerializeField] private float zRotationRange = 15f;
    [Tooltip("Y 轴持续旋转速度（度/秒）。")]
    [SerializeField] private float yRotationSpeed = 180f;

    private RectTransform _rectTransform;
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private Tween _moveTween;
    private Tween _yRotationTween;
    private System.Action<MonsterKillDrop> _onComplete;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            _rectTransform = gameObject.AddComponent<RectTransform>();
        }
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _yRotationTween?.Kill();
    }

    /// <summary>
    /// 初始化掉落物：设置材料 sprite 并开始掉落动画。
    /// </summary>
    /// <param name="materialSprite">材料的 sprite</param>
    /// <param name="startPos">起始位置（怪物位置）</param>
    /// <param name="parent">父节点（通常是 Canvas 或 UI 根节点）</param>
    /// <param name="onComplete">动画完成回调</param>
    public void Initialize(Sprite materialSprite, Vector3 startPos, Transform parent, System.Action<MonsterKillDrop> onComplete = null)
    {
        if (materialImage != null && materialSprite != null)
        {
            materialImage.sprite = materialSprite;
        }

        if (parent != null)
        {
            transform.SetParent(parent, false);
        }

        _startPosition = startPos;
        _onComplete = onComplete;

        // 计算随机目标位置
        CalculateRandomTarget();

        // 设置初始位置和旋转
        if (_rectTransform != null)
        {
            _rectTransform.position = _startPosition;
        }
        
        // 随机 Z 轴旋转
        float randomZRotation = Random.Range(-zRotationRange, zRotationRange);
        transform.rotation = Quaternion.Euler(0f, 0f, randomZRotation);

        // 开始动画
        StartDropAnimation();
    }

    private void CalculateRandomTarget()
    {
        // 在起始位置附近随机一个目标点
        float randomX = Random.Range(-randomRange.x, randomRange.x);
        float randomY = Random.Range(-randomRange.y, randomRange.y);
        _targetPosition = _startPosition + new Vector3(randomX, randomY, 0f);
    }

    private void StartDropAnimation()
    {
        if (_rectTransform == null) return;

        // 贝塞尔曲线移动
        Vector3 p0 = _startPosition;
        Vector3 p2 = _targetPosition;
        Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * bezierHeight; // 控制点在中间上方

        // 使用 DOTween 的路径动画实现贝塞尔曲线
        _moveTween = DOTween.To(
            () => 0f,
            t =>
            {
                // 二次贝塞尔曲线：B(t) = (1-t)²P₀ + 2(1-t)tP₁ + t²P₂
                float u = 1f - t;
                Vector3 pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                _rectTransform.position = pos;
            },
            1f,
            dropDuration
        ).SetEase(Ease.OutQuad).OnComplete(OnDropComplete);

        // Y 轴持续旋转
        float currentY = transform.eulerAngles.y;
        _yRotationTween = DOTween.To(
            () => currentY,
            y =>
            {
                Vector3 euler = transform.eulerAngles;
                euler.y = y;
                transform.eulerAngles = euler;
            },
            currentY + yRotationSpeed * dropDuration,
            dropDuration
        ).SetEase(Ease.Linear);
    }

    private void OnDropComplete()
    {
        _onComplete?.Invoke(this);
    }

    /// <summary>
    /// 手动完成动画（用于提前结束）。
    /// </summary>
    public void Complete()
    {
        _moveTween?.Kill();
        _yRotationTween?.Kill();
        OnDropComplete();
    }
}

