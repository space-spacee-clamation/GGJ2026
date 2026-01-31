using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 材料库存中的一个材料实例对应的按钮 UI。
/// </summary>
public class MaterialButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image image;

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

    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        gameObject.SetActive(!selected);
        if(selected)
        {
           _owner?.CloseInfo();
        }
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
        _owner?.CloseInfo();
        // no-op
    }
}



