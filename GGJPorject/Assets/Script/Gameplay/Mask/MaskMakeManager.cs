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
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
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
            Debug.LogError("[MaskMakeManager] baseMaskPrefab 未配置，无法制造面具。", this);
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


