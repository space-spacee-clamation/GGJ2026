using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 玩家面具显示组件：显示当前佩戴面具和面具库中其它面具的环绕效果。
/// </summary>
public sealed class PlayerMask : MonoBehaviour
{
    [Header("中心面具（当前佩戴）")]
    [Tooltip("显示当前佩戴面具的 Image。")]
    [SerializeField] private Image currentMaskImage;

    [Header("环绕配置")]
    [Tooltip("环绕半径（像素）。")]
    [SerializeField] private float orbitRadius = 100f;
    [Tooltip("环绕角速度（度/秒）。")]
    [SerializeField] private float angularSpeed = 45f;
    [Tooltip("环绕面具的 Image 预制体。")]
    [SerializeField] private Image orbitMaskImagePrefab;
    [Tooltip("环绕面具的父节点。")]
    [SerializeField] private Transform orbitRoot;

    private Player _player;
    private readonly List<Image> _orbitImages = new();
    private float _currentAngle;
    private Tween _rotationTween;

    /// <summary>
    /// 初始化：传入 Player 对象。
    /// </summary>
    public void Initialize(Player player)
    {
        _player = player;
        RefreshMaskDisplay();
    }

    private void OnEnable()
    {
        if (_player != null)
        {
            RefreshMaskDisplay();
        }
    }

    private void OnDisable()
    {
        StopRotation();
    }

    private void OnDestroy()
    {
        StopRotation();
        ClearOrbitImages();
    }

    /// <summary>
    /// 刷新面具显示：更新中心面具和环绕面具。
    /// </summary>
    public void RefreshMaskDisplay()
    {
        if (_player == null || GameManager.I == null) return;

        // 更新中心面具（当前佩戴）
        var currentMask = GameManager.I.GetCurrentMask();
        if (currentMaskImage != null && currentMask != null)
        {
            currentMaskImage.sprite = currentMask.DisplaySprite;
        }

        // 更新环绕面具（面具库中其它面具）
        RefreshOrbitMasks();
    }

    private void RefreshOrbitMasks()
    {
        if (GameManager.I == null) return;

        // 获取面具库（排除当前面具）
        var currentMask = GameManager.I.GetCurrentMask();
        var maskLibrary = GameManager.I.GetMaskLibrary();
        if (maskLibrary == null) return;

        // 清理旧的环绕 Image
        ClearOrbitImages();

        // 创建环绕 Image
        if (orbitMaskImagePrefab == null || orbitRoot == null) return;

        int validMaskCount = 0;
        for (int i = 0; i < maskLibrary.Count; i++)
        {
            var mask = maskLibrary[i];
            if (mask == null || mask == currentMask) continue;

            var sprite = mask.DisplaySprite;
            if (sprite == null) continue;

            var img = Instantiate(orbitMaskImagePrefab, orbitRoot, false);
            img.sprite = sprite;
            _orbitImages.Add(img);
            validMaskCount++;
        }

        // 如果有效面具数量 > 0，开始旋转动画
        if (validMaskCount > 0)
        {
            StartRotation();
        }
    }

    private void StartRotation()
    {
        StopRotation();

        if (_orbitImages.Count == 0) return;

        // 计算每个面具的角度间隔
        float angleStep = 360f / _orbitImages.Count;
        _currentAngle = 0f;

        // 使用 DOTween 持续旋转
        _rotationTween = DOTween.To(
            () => _currentAngle,
            angle =>
            {
                _currentAngle = angle;
                UpdateOrbitPositions();
            },
            360f,
            360f / angularSpeed
        )
        .SetLoops(-1, LoopType.Incremental)
        .SetEase(Ease.Linear)
        .SetUpdate(true);
    }

    private void StopRotation()
    {
        _rotationTween?.Kill();
        _rotationTween = null;
    }

    private void UpdateOrbitPositions()
    {
        if (_orbitImages.Count == 0) return;

        float angleStep = 360f / _orbitImages.Count;

        for (int i = 0; i < _orbitImages.Count; i++)
        {
            var img = _orbitImages[i];
            if (img == null || img.transform == null) continue;

            float angle = (_currentAngle + angleStep * i) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(
                Mathf.Cos(angle) * orbitRadius,
                Mathf.Sin(angle) * orbitRadius
            );

            var rt = img.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = offset;
            }
        }
    }

    private void ClearOrbitImages()
    {
        foreach (var img in _orbitImages)
        {
            if (img != null) Destroy(img.gameObject);
        }
        _orbitImages.Clear();
    }
}

