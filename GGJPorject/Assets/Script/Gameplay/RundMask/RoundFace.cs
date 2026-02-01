using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundFace : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("中心点（围绕谁转），如果不填则默认围绕本脚本挂载的物体中心")]
    public RectTransform centerAnchor; 
    
    [Tooltip("环绕半径")]
    public float radius = 150f;
    
    [Tooltip("旋转速度 (度/秒)")]
    public float rotationSpeed = 30f;
    
    [Tooltip("用来生成的面具图标预制体 (必须包含 Image 组件)")]
    public GameObject maskIconPrefab;

    [Header("显示控制")]
    [Tooltip("是否隐藏当前佩戴的面具（只让库存里的转圈）")]
    public bool excludeCurrentMask = true;

    // 运行时数据
    private List<RectTransform> _spawnedMasks = new List<RectTransform>();
    private float _currentAngle = 0f;

    private void Start()
    {
        // 如果没指定中心，就以自身为中心
        if (centerAnchor == null) centerAnchor = GetComponent<RectTransform>();
        
        // 初始化生成
        RefreshMasks();
    }

    private void Update()
    {
        // 持续旋转逻辑
        RotateMasks();
        
        // (可选) 可以在 Update 里检测数量变化，或者由外部在获得面具时调用 RefreshMasks()
    }

    // 外部调用入口：当获得新面具时调用此方法刷新显示
    public void RefreshMasks()
    {
        if (GameManager.I == null) return;

        // 1. 清理旧的图标
        ClearOldIcons();

        // 2. 获取数据
        var library = GameManager.I.GetMaskLibrary();
        var currentMask = GameManager.I.GetCurrentMask();

        if (library == null) return;

        // 3. 遍历并生成
        foreach (var mask in library)
        {
            if (mask == null) continue;

            // 如果设置了排除当前佩戴的面具，且该面具正是当前面具，则跳过
            if (excludeCurrentMask && mask == currentMask) continue;

            // 实例化
            GameObject obj = Instantiate(maskIconPrefab, transform); // 生成为本物体的子物体

            // 记录 RectTransform 以便 Update 中移动
            _spawnedMasks.Add(obj.GetComponent<RectTransform>());
        }

        // 4. 立即排列一次位置
        UpdatePositions();
    }

    private void RotateMasks()
    {
        if (_spawnedMasks.Count == 0) return;

        // 随时间增加角度
        _currentAngle += rotationSpeed * Time.deltaTime;
        if (_currentAngle >= 360f) _currentAngle -= 360f;

        UpdatePositions();
    }

    private void UpdatePositions()
    {
        int count = _spawnedMasks.Count;
        if (count == 0) return;

        // 计算每个面具之间的间隔角度
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            var itemRect = _spawnedMasks[i];
            
            // 计算当前这个面具的角度：基础角度 + 偏移角度
            float finalAngleDeg = _currentAngle + (angleStep * i);
            float finalAngleRad = finalAngleDeg * Mathf.Deg2Rad; // 转为弧度

            // 计算坐标 (x = r * cos, y = r * sin)
            float x = Mathf.Cos(finalAngleRad) * radius;
            float y = Mathf.Sin(finalAngleRad) * radius;

            // 如果 centerAnchor 不是本物体，可能需要加上 centerAnchor 的位置偏移
            // 这里假设 maskIconPrefab 是作为本物体的子物体生成的，且本物体锚点就在中心
            itemRect.anchoredPosition = new Vector2(x, y);
        }
    }

    private void ClearOldIcons()
    {
        foreach (var rect in _spawnedMasks)
        {
            if (rect != null) Destroy(rect.gameObject);
        }
        _spawnedMasks.Clear();
    }
}