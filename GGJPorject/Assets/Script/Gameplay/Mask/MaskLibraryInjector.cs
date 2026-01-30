using System.Collections.Generic;

/// <summary>
/// 将“面具库（多面具）+ 当前面具”统一作为一个注入器交给 FightManager。
/// </summary>
public sealed class MaskLibraryInjector : IMaskBattleInjector
{
    private readonly IReadOnlyList<IMaskBattleInjector> _injectors;

    public MaskLibraryInjector(IReadOnlyList<IMaskBattleInjector> injectors)
    {
        _injectors = injectors;
    }

    public void InjectBattleContext(FightContext context)
    {
        if (context == null || _injectors == null) return;
        for (int i = 0; i < _injectors.Count; i++)
        {
            _injectors[i]?.InjectBattleContext(context);
        }
    }
}


