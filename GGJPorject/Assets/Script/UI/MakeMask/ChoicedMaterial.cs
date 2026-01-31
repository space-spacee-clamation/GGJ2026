using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 面具区域随机生成的“已选材料”UI。
/// 点击后收回到材料库（由 MakeMuskUI 处理）。
/// </summary>
public class ChoicedMaterial : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image image;

    public MaterialObj Material { get; private set; }
    private MakeMuskUI _owner;

    public void Initialize(MakeMuskUI owner, MaterialObj material, Material qualityMat, Material selectedMat)
    {
        _owner = owner;
        Material = material;

        if (image == null) image = GetComponent<Image>();

        var sprite = material != null ? material.BaseSprite : null;
        if (image != null) image.sprite = sprite;


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


    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.OnClickChoiced(this);
    }
}



