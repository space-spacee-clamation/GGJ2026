using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 材料库存中的一个材料实例对应的按钮 UI。
/// </summary>
public class MaterialButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image image;
    [SerializeField] private Image qualityOutline;
    [SerializeField] private Image selectedOutline;

    public MaterialObj Material { get; private set; }
    public bool IsSelected { get; private set; }

    private MakeMuskUI _owner;

    public void Initialize(MakeMuskUI owner, MaterialObj material, Material outlineMat)
    {
        _owner = owner;
        Material = material;
        IsSelected = false;

        if (image == null) image = GetComponent<Image>();

        var sprite = material != null ? material.BaseSprite : null;
        if (image != null) image.sprite = sprite;

        if (qualityOutline != null)
        {
            qualityOutline.sprite = sprite;
            if (outlineMat != null) qualityOutline.material = new Material(outlineMat);
            ApplyQualityOutlineColor(qualityOutline, material);
        }

        if (selectedOutline != null)
        {
            selectedOutline.sprite = sprite;
            if (outlineMat != null) selectedOutline.material = new Material(outlineMat);
            SetSelected(false);
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectedOutline == null) return;

        selectedOutline.enabled = selected;
        if (selectedOutline.material != null)
        {
            selectedOutline.material.SetColor("_OutlineColor", GameSetting.SelectedOutline_Green);
            selectedOutline.material.SetFloat("_OutlineWidth", selected ? 3f : 0f);
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
        _owner?.OnClickMaterialButton(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowMaterialInfo(Material);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // no-op
    }
}


