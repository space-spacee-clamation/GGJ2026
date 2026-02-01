using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 面具制造管理器：每次制造都从“底板面具 Prefab”生成一个新的 MaskObj，进入材料附加阶段。
/// 约束：必须由 GameManager.Awake() 进行创建与 Initialize（遵循项目初始化规范）。
/// </summary>
public class MaskMakeManager : MonoBehaviour
{
    public static MaskMakeManager I { get; private set; }

    [Header("Base Mask Prefab")]
    [Tooltip("底板面具预制体：每次制造面具时都会先实例化这个对象，然后进行材料附加。")]
    [SerializeField] private MaskObj baseMaskPrefab;

    [Header("Runtime")]
    [SerializeField] private MaskObj currentMask;

    public MaskObj CurrentMask => currentMask;

    public void Initialize()
    {
        I = this;
    }

    /// <summary>
    /// Jam 测试用：如果未配置 baseMaskPrefab，则在运行时创建一个临时底板面具并作为“模板”使用。
    /// </summary>
    public void EnsureBaseMaskPrefabForTest(int baseMana = 10)
    {
        if (baseMaskPrefab != null) return;

        var go = new GameObject("TempBaseMaskPrefab");
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.HideInHierarchy;

        var mask = go.AddComponent<MaskObj>();
        // MaskObj 自身已有 config 容错；这里确保 baseMana
        mask.RebuildFromConfig(new MaskObj.StaticConfig
        {
            BaseMana = Mathf.Max(0, baseMana),
            DisplayName = "TempMask",
            Description = "Jam 自动生成的底板面具（仅用于测试跑流程）。"
        });

        baseMaskPrefab = mask;
    }

    [Button(ButtonSizes.Medium)]
    public MaskObj MakeNextMask()
    {
        // 如果当前面具还没被上层“入库/接管”，则制造新面具前先销毁旧的（避免泄漏）。
        if (currentMask != null)
        {
            Destroy(currentMask.gameObject);
            currentMask = null;
        }

        if (baseMaskPrefab == null)
        {
            // Jam 容错：尝试自动生成一个临时底板面具，保证流程可跑
            EnsureBaseMaskPrefabForTest(10);
        }
        if (baseMaskPrefab == null)
        {
            Debug.LogError("[MaskMakeManager] baseMaskPrefab 未配置且自动生成失败，无法制造面具。", this);
            return null;
        }

        currentMask = Instantiate(baseMaskPrefab, transform, false);
        currentMask.name = $"MaskObj_RunMask";
        return currentMask;
    }

    /// <summary>
    /// 战斗结束后，上层将当前面具“入库接管”，并清空 currentMask，避免下一次 MakeNextMask 销毁入库面具。
    /// </summary>
    public MaskObj DetachCurrentMaskForLibrary()
    {
        var m = currentMask;
        currentMask = null;
        return m;
    }
}


