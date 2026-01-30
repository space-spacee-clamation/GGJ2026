using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 面具区域随机生成的“已选材料”UI。
/// 点击后收回到材料库（由 MakeMuskUI 处理）。
/// </summary>
public class ChoicedMaterial : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image image;
    [SerializeField] private Image qualityOutline;
    [SerializeField] private Image selectedOutline;

    public MaterialObj Material { get; private set; }
    private MakeMuskUI _owner;

    public void Initialize(MakeMuskUI owner, MaterialObj material, Material qualityMat, Material selectedMat)
    {
        _owner = owner;
        Material = material;

        if (image == null) image = GetComponent<Image>();

        var sprite = material != null ? material.BaseSprite : null;
        if (image != null) image.sprite = sprite;

        if (qualityOutline != null)
        {
            qualityOutline.sprite = sprite;
            if (qualityMat != null) qualityOutline.material = new Material(qualityMat);
            ApplyQualityOutlineColor(qualityOutline, material);
        }

        if (selectedOutline != null)
        {
            selectedOutline.sprite = sprite;
            if (selectedMat != null) selectedOutline.material = new Material(selectedMat);
            SetSelectedOutline(false);
        }

        // 尺寸对齐 sprite
        if (sprite != null)
        {
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sprite.rect.width);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sprite.rect.height);
            }
        }
    }

    public void SetSelectedOutline(bool on)
    {
        if (selectedOutline == null) return;
        selectedOutline.enabled = on;
        if (selectedOutline.material != null)
        {
            selectedOutline.material.SetColor("_OutlineColor", GameSetting.SelectedOutline_Red);
            selectedOutline.material.SetFloat("_OutlineWidth", on ? 3f : 0f);
        }
    }

    private static void ApplyQualityOutlineColor(Image img, MaterialObj mat)
    {
        if (img == null || img.material == null) return;
        var c = GameSetting.QualityOutline_Common;
        if (mat != null)
        {
            c = mat.Quality switch
            {
                MaterialQuality.Common => GameSetting.QualityOutline_Common,
                MaterialQuality.Uncommon => GameSetting.QualityOutline_Uncommon,
                MaterialQuality.Rare => GameSetting.QualityOutline_Rare,
                MaterialQuality.Epic => GameSetting.QualityOutline_Epic,
                MaterialQuality.Legendary => GameSetting.QualityOutline_Legendary,
                _ => GameSetting.QualityOutline_Common
            };
        }
        img.material.SetColor("_OutlineColor", c);
        img.material.SetFloat("_OutlineWidth", 1.5f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.OnClickChoiced(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetSelectedOutline(true);
        _owner?.ShowMaterialInfo(Material);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetSelectedOutline(false);
    }
}


