using System.Text;
using UnityEngine;

/// <summary>
/// 战斗中对运行时 CritChance 做加值（可为负数，最终会 clamp 到 0~1）。
/// </summary>
public sealed class AddCritChanceMaterial : MonoBehaviour, IFightComponent, IMaterialDescriptionProvider
{
    [SerializeField] private FightSide appliesTo = FightSide.Player;

    [Tooltip("可填负数；最终会 clamp 到 0~1。")]
    [SerializeField] private float deltaCritChance = 0.1f;

    [TextArea] [SerializeField] private string description = "战斗开始：暴击率加值。";

    private bool _appliedThisBattle;

    public void Inject(FightContext context)
    {
        if (context == null) return;
        context.OnBattleEnter += OnBattleEnter;
        context.OnBattleStart += OnBattleStart;
    }

    private void OnBattleEnter(FightContext context)
    {
        _appliedThisBattle = false;
    }

    private void OnBattleStart(FightContext context)
    {
        if (_appliedThisBattle) return;
        _appliedThisBattle = true;

        ApplyTo(appliesTo, context)?.AddCritChance(deltaCritChance);
    }

    private static CombatantRuntime ApplyTo(FightSide side, FightContext context)
    {
        if (context == null) return null;
        if (side == FightSide.Player) return context.Player;
        if (side == FightSide.Enemy) return context.Enemy;
        return null;
    }

    public void AppendDescription(StringBuilder sb)
    {
        if (sb == null) return;
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine(description);
        }
    }
}


