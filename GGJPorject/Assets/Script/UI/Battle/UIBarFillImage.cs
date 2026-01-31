using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 给 UI Image 套用 `UI/BarFill` shader，并以材质参数控制 0~1 完整度。
/// 注意：每个 bar 会实例化一份 Material，避免多个 Image 互相串值。
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class UIBarFillImage : MonoBehaviour
{
    private static readonly int Fill01Id = Shader.PropertyToID("_Fill01");
    private static readonly int ReverseId = Shader.PropertyToID("_Reverse");

    [SerializeField] private Image image;
    [Range(0f, 1f)][SerializeField] private float fill01 = 1f;
    [SerializeField] private bool reverse = false;

    private Material _runtimeMat;

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        EnsureMaterial();
        Apply();
    }

    private void OnDestroy()
    {
        if (_runtimeMat != null)
        {
            Destroy(_runtimeMat);
            _runtimeMat = null;
        }
    }

    private void EnsureMaterial()
    {
        if (image == null) return;
        if (_runtimeMat != null) return;

        // 优先用当前 Image 上配置的材质（例如你在 UI 里手动拖了材质）
        var src = image.material;
        if (src == null || src.shader == null || src.shader.name != "UI/BarFill")
        {
            var shader = Shader.Find("UI/BarFill");
            if (shader != null) src = new Material(shader);
        }

        if (src == null) return;

        _runtimeMat = new Material(src);
        image.material = _runtimeMat;
    }

    private void Apply()
    {
        if (_runtimeMat == null) return;
        _runtimeMat.SetFloat(Fill01Id, fill01);
        _runtimeMat.SetFloat(ReverseId, reverse ? 1f : 0f);
    }

    public void SetFill01(float v)
    {
        fill01 = Mathf.Clamp01(v);
        Apply();
    }

    public void SetReverse(bool v)
    {
        reverse = v;
        Apply();
    }
}


