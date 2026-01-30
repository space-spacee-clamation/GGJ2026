using UnityEngine;

public class MaskObj : MonoBehaviour, IMaskBattleInjector
{
    [System.Serializable]
    public class StaticConfig
    {
        [Min(0)] public int BaseMana;
        public string DisplayName;
        [TextArea] public string Description;
    }

    [Header("Config")]
    [SerializeField] private StaticConfig config;

    [Header("Runtime")]
    [SerializeField] private Transform materialRoot;

    public int BaseMana { get; private set; }
    public int CurrentMana { get; private set; }

    private readonly System.Collections.Generic.List<MaterialObj> _materials = new();
    public System.Collections.Generic.IReadOnlyList<MaterialObj> Materials => _materials;

    public event System.Action<MaterialObj> OnMaterialBound;

    private void Awake()
    {
        if (materialRoot == null) materialRoot = transform;
        RebuildFromConfig(config);
    }

    /// <summary>
    /// 使用配置重建面具实例（战后“旧面具不保留”的 V0 流程可直接调用该方法重建）。
    /// </summary>
    public void RebuildFromConfig(StaticConfig cfg)
    {
        config = cfg;
        BaseMana = Mathf.Max(0, config.BaseMana);
        CurrentMana = BaseMana;

        // 清理旧材料实例
        for (int i = materialRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(materialRoot.GetChild(i).gameObject);
        }
        _materials.Clear();
    }

    public void AddMana(int delta)
    {
        if (delta == 0) return;
        CurrentMana = Mathf.Max(0, CurrentMana + delta);
    }

    /// <summary>
    /// 绑定一个材料：不会实例化，不会创建新物体，只将其纳入链表并触发 OnBind。
    /// </summary>
    public BindResult BindMaterial(MaterialObj material)
    {
        if (material == null)
        {
            return BindResult.Fail(BindFailReason.InvalidMaterialPrefab, "材料为空。");
        }

        int cost = Mathf.Max(0, material.ManaCost);

        if (CurrentMana < cost)
        {
            return BindResult.Fail(BindFailReason.NotEnoughMana, $"法力值不足：需要 {cost}，当前 {CurrentMana}。");
        }

        CurrentMana -= cost;

        // 加入链表（顺序很重要）
        _materials.Add(material);

        // 绑定到面具（仅做 re-parent，不创建新物体）
        var go = material.gameObject;
        if (materialRoot != null && go != null && go.transform.parent != materialRoot)
        {
            go.transform.SetParent(materialRoot, false);
        }

        // 即时生效：由 MaterialObj 负责 foreach 所有组件（组件自己决定做不做）
        var ctx = new BindContext(this, _materials, OnMaterialBound);
        material.RunBindEffects(in ctx);

        OnMaterialBound?.Invoke(material);
        return BindResult.Ok(material);
    }

    public void InjectBattleContext(FightContext context)
    {
        // 按“材料链表顺序”注入
        if (context == null) return;

        for (int i = 0; i < _materials.Count; i++)
        {
            var mat = _materials[i];
            if (mat == null) continue;
            mat.InjectBattle(context);
        }
    }
}
