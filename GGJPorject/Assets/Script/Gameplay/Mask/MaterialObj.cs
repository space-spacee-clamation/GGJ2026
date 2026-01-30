using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 材料对象（挂在“材料 Prefab”根节点上）。
/// 在 Awake 中自动 GetComponents 并缓存，后续由 MaskObj 直接调用 MaterialObj 完成：
/// - 绑定阶段即时效果
/// - 进入战斗前注入 FightContext（回调/处理链）
/// </summary>
public class MaterialObj : MonoBehaviour
{
    [Header("Base Data")]
    [Min(0)] public int Id = 0;
    [Tooltip("材质名（用于 UI 与保存命名）。")]
    public string DisplayName;

    [Min(0)] public int ManaCost = 0;

    public MaterialQuality Quality = MaterialQuality.Common;

    [Tooltip("用于 UI 显示的基础图像。")]
    public Sprite BaseSprite;

    [Header("Inventory")]
    [Tooltip("保质期（回合）：从获取开始算，经过多少个“制造面具回合结束”后会在材料库中销毁。")]
    [Min(1)] public int ShelfLifeTurns = 1;

    [SerializeField, Min(0)] private int remainingShelfLifeTurns = 0;
    public int RemainingShelfLifeTurns => remainingShelfLifeTurns;

    /// <summary>
    /// 入库时调用：把 RemainingShelfLifeTurns 重置为 ShelfLifeTurns。
    /// </summary>
    public void ResetInventoryShelfLife()
    {
        remainingShelfLifeTurns = Mathf.Max(1, ShelfLifeTurns);
    }

    /// <summary>
    /// 库存回合结算：Remaining--，返回是否过期（<=0）。
    /// 注意：仅库存中的材料会被调用；已绑定到面具的材料不参与库存结算。
    /// </summary>
    public bool TickInventory()
    {
        remainingShelfLifeTurns = Mathf.Max(0, remainingShelfLifeTurns - 1);
        return remainingShelfLifeTurns <= 0;
    }

    /// <summary>
    /// 按组件顺序生成描述：遍历本 GameObject 上的 MonoBehaviour，依次调用 IMaterialDescriptionProvider。
    /// </summary>
    public string BuildDescription()
    {
        var sb = new StringBuilder(128);
        BuildDescription(sb);
        return sb.ToString();
    }

    public void BuildDescription(StringBuilder sb)
    {
        if (sb == null) return;
        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMaterialDescriptionProvider p)
            {
                p.AppendDescription(sb);
            }
        }
    }

    private readonly List<IMaterialBindEffect> _bindEffects = new();
    private readonly List<IFightComponent> _fightComponents = new();
    private readonly List<IAttackInfoModifier> _attackModifiers = new();

    public IReadOnlyList<IMaterialBindEffect> BindEffects => _bindEffects;
    public IReadOnlyList<IFightComponent> FightComponents => _fightComponents;
    public IReadOnlyList<IAttackInfoModifier> AttackModifiers => _attackModifiers;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        _bindEffects.Clear();
        _fightComponents.Clear();
        _attackModifiers.Clear();

        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;

            if (b is IMaterialAutoInit init) init.Initialize(this);
            if (b is IMaterialBindEffect bind) _bindEffects.Add(bind);
            if (b is IFightComponent fight) _fightComponents.Add(fight);
            if (b is IAttackInfoModifier mod) _attackModifiers.Add(mod);
        }
    }

    public void RunBindEffects(in BindContext context)
    {
        for (int i = 0; i < _bindEffects.Count; i++)
        {
            _bindEffects[i]?.OnBind(in context);
        }
    }

    public void InjectBattle(FightContext context)
    {
        if (context == null) return;

        // 订阅回调等（战斗组件）
        for (int i = 0; i < _fightComponents.Count; i++)
        {
            context.AddFightComponent(_fightComponents[i]);
        }

        // 攻击修改器（默认注入两条链，组件内部自行判断是否生效）
        for (int i = 0; i < _attackModifiers.Count; i++)
        {
            var mod = _attackModifiers[i];
            if (mod == null) continue;
            context.PlayerAttackProcessor.Add(mod);
            context.EnemyAttackProcessor.Add(mod);
        }
    }
}


