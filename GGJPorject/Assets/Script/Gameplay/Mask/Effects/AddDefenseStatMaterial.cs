using System.Text;
using UnityEngine;

/// <summary>
/// 战斗中对运行时 Defense 做加值（可为负数）。
/// </summary>
public sealed class AddDefenseStatMaterial : MonoBehaviour, IFightComponent, IMaterialDescriptionProvider
{
    [SerializeField] private FightSide appliesTo = FightSide.Player;
    [SerializeField] private float deltaDefense = 1f;
    [TextArea] [SerializeField] private string description = "战斗开始：防御加值。";

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

        ApplyTo(appliesTo, context)?.AddDefense(deltaDefense);
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


