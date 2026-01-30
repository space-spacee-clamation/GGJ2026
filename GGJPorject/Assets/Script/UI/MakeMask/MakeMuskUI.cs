using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MakeMuskUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InfoNode infoNode;

    [Header("Mask View")]
    [SerializeField] private Image maskImage;
    [SerializeField] private Sprite baseMaskSprite;
    [SerializeField] private Sprite composedMaskSprite;

    [Header("Inventory List")]
    [SerializeField] private Transform inventoryContentRoot; // ScrollView Content
    [SerializeField] private MaterialButton materialButtonPrefab;

    [Header("Chosen Area")]
    [SerializeField] private RectTransform chosenSpawnArea;
    [SerializeField] private ChoicedMaterial choicedMaterialPrefab;

    [Header("Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button composeButton;

    [Header("Outline Shader Material (UI/AlphaOutline)")]
    [SerializeField] private Material outlineMaterial;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private readonly Dictionary<MaterialObj, MaterialButton> _buttons = new();
    private readonly Dictionary<MaterialObj, ChoicedMaterial> _choiced = new();

    private bool _composedOnce;

    private void OnEnable()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnClickNext);
        if (composeButton != null) composeButton.onClick.AddListener(OnClickCompose);
        if (enableLogs)
        {
            if (composeButton == null) Debug.LogWarning("[MakeMuskUI] composeButton 未绑定。", this);
            if (nextButton == null) Debug.LogWarning("[MakeMuskUI] nextButton 未绑定。", this);
            if (chosenSpawnArea == null) Debug.LogWarning("[MakeMuskUI] chosenSpawnArea 未绑定：仍可选择并合成，但不会生成 ChoicedMaterial UI。", this);
            if (choicedMaterialPrefab == null) Debug.LogWarning("[MakeMuskUI] choicedMaterialPrefab 未绑定：仍可选择并合成，但不会生成 ChoicedMaterial UI。", this);
            if (UnityEngine.EventSystems.EventSystem.current == null) Debug.LogWarning("[MakeMuskUI] 场景中没有 EventSystem，UI 点击/悬停可能无效。", this);
        }

        // UI 兜底：如果 UI 被直接打开但还没创建 CurrentMask，自动补一个
        if (GameManager.I != null && GameManager.I.GetCurrentMask() == null)
        {
            GameManager.I.EnsureCurrentMaskForMakeUI();
        }
        RefreshInventoryUI();
        UpdateNextInteractable();
        UpdateMaskSprite();
    }

    private void OnDisable()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnClickNext);
        if (composeButton != null) composeButton.onClick.RemoveListener(OnClickCompose);
    }

    public void RefreshInventoryUI()
    {
        if (inventoryContentRoot == null || materialButtonPrefab == null) return;

        // 清空旧按钮
        foreach (var kv in _buttons)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _buttons.Clear();
        // 清空旧“已选”UI
        foreach (var kv in _choiced)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _choiced.Clear();

        // 按“加入库存顺序”展示：MaterialInventory.Items 的顺序就是加入顺序
        var inv = GameManager.I != null ? GameManager.I.GetMaterialInventoryItems() : null;
        if (inv == null) return;

        for (int i = 0; i < inv.Count; i++)
        {
            var mat = inv[i];
            if (mat == null) continue;

            var btn = Instantiate(materialButtonPrefab, inventoryContentRoot, false);
            btn.Initialize(this, mat, outlineMaterial);
            _buttons[mat] = btn;
        }

        if (infoNode != null) infoNode.Clear();
    }

    public void ShowMaterialInfo(MaterialObj mat)
    {
        if (infoNode == null) return;
        if (mat == null)
        {
            infoNode.Clear();
            return;
        }
        string name = !string.IsNullOrWhiteSpace(mat.DisplayName) ? mat.DisplayName : mat.name;
        string desc = mat.BuildDescription();
        int ttl = mat.RemainingShelfLifeTurns;
        infoNode.Show(name, desc, ttl);
    }

    public void OnClickMaterialButton(MaterialButton btn)
    {
        if (btn == null || btn.Material == null) return;
        var mat = btn.Material;

        // 已选则忽略（V0：一个材料只能被选中一次）
        if (_choiced.ContainsKey(mat)) return;

        // 生成 ChoicedMaterial（UI），但不移动材料实例（材料仍在库存中）
        ChoicedMaterial c = null;
        if (choicedMaterialPrefab != null && chosenSpawnArea != null)
        {
            c = Instantiate(choicedMaterialPrefab, chosenSpawnArea, false);
            c.Initialize(this, mat, outlineMaterial, outlineMaterial);

            // 随机位置（在 given area 内）
            var rt = c.transform as RectTransform;
            if (rt != null)
            {
                var size = chosenSpawnArea.rect.size;
                var px = Random.Range(-size.x * 0.5f, size.x * 0.5f);
                var py = Random.Range(-size.y * 0.5f, size.y * 0.5f);
                rt.anchoredPosition = new Vector2(px, py);
            }

        }

        // 即使未配置 choicedMaterialPrefab / chosenSpawnArea，也要记录“已选”，否则 Compose 会认为没选中
        _choiced[mat] = c;

        btn.SetSelected(true);
        ShowMaterialInfo(mat);
        UpdateNextInteractable();
    }

    public void OnClickChoiced(ChoicedMaterial c)
    {
        if (c == null || c.Material == null) return;
        var mat = c.Material;

        if (_choiced.Remove(mat))
        {
            Destroy(c.gameObject);
        }

        if (_buttons.TryGetValue(mat, out var btn) && btn != null)
        {
            btn.SetSelected(false);
        }

        UpdateNextInteractable();
    }

    private void OnClickCompose()
    {
        if (GameManager.I == null) return;
        var mask = GameManager.I.GetCurrentMask();
        if (mask == null)
        {
            // 再兜底一次：可能是中途被销毁/未创建
            mask = GameManager.I.EnsureCurrentMaskForMakeUI();
            if (mask == null)
            {
                if (enableLogs) Debug.LogWarning("[MakeMuskUI] Compose 失败：当前 Mask 为空（GameManager.GetCurrentMask()）。", this);
                return;
            }
        }

        if (_choiced.Count == 0)
        {
            if (enableLogs) Debug.Log("[MakeMuskUI] Compose：没有已选材料。", this);
            return;
        }

        // 绑定所有已选材料（顺序：按玩家选择顺序。这里用当前字典遍历顺序可能不稳定，V0 先简单实现：按按钮 sibling 顺序绑定）
        // Jam 简化：直接遍历 _choiced 的 key
        var list = new List<MaterialObj>(_choiced.Keys);

        for (int i = 0; i < list.Count; i++)
        {
            var mat = list[i];
            if (mat == null) continue;

            var result = mask.BindMaterial(mat);
            if (result.Success)
            {
                if (enableLogs) Debug.Log($"[MakeMuskUI] Compose 成功：Bind {mat.DisplayName} cost={mat.ManaCost} maskMana={mask.CurrentMana}/{mask.BaseMana}", this);
                // 从库存移除（材料成为面具一部分，不再参与库存结算）
                GameManager.I.RemoveMaterialFromInventory(mat);

                // 删除按钮与已选 UI
                if (_buttons.TryGetValue(mat, out var btn) && btn != null)
                {
                    Destroy(btn.gameObject);
                    _buttons.Remove(mat);
                }
                if (_choiced.TryGetValue(mat, out var cm) && cm != null)
                {
                    Destroy(cm.gameObject);
                }
                _choiced.Remove(mat);

                _composedOnce = true;
            }
            else
            {
                if (enableLogs) Debug.LogWarning($"[MakeMuskUI] Compose 失败：Bind {mat.DisplayName} reason={result.FailReason}", this);
                // 绑定失败：取消选择，材料仍在库存中
                if (_choiced.TryGetValue(mat, out var cm) && cm != null) Destroy(cm.gameObject);
                _choiced.Remove(mat);
                if (_buttons.TryGetValue(mat, out var btn) && btn != null) btn.SetSelected(false);
            }
        }

        UpdateMaskSprite();
        UpdateNextInteractable();
    }

    private void OnClickNext()
    {
        if (_choiced.Count > 0) return; // 安全：有未处理的已选材料禁止进入战斗
        GameManager.I?.NotifyMakeMaskFinished();
        gameObject.SetActive(false);
    }

    private void UpdateNextInteractable()
    {
        if (nextButton != null) nextButton.interactable = _choiced.Count == 0;
    }

    private void UpdateMaskSprite()
    {
        if (maskImage == null) return;
        maskImage.sprite = _composedOnce && composedMaskSprite != null ? composedMaskSprite : baseMaskSprite;
    }
}
