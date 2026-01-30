using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 顺序怪物生成逻辑（Mono）：按顺序从列表中取 Enemy 配置（循环）。
/// 该逻辑自身持有怪物列表（不再由 GameManager 注入 defaultEnemyConfigs）。
/// </summary>
public class SequentialEnemySpawnLogic : MonoBehaviour, IMonsterSpawnLogic
{
    [Header("怪物列表（顺序循环）")]
    [Tooltip("按顺序循环生成的怪物配置列表（可包含 null，会自动跳过）。")]
    [SerializeField] private List<CharacterConfig> enemyConfigs = new();

    [SerializeField, Min(0)] private int nextIndex = 0;

    public CharacterConfig TrySpawn(int roundIndex, FightContext context)
    {
        if (enemyConfigs == null || enemyConfigs.Count == 0) return null;

        int safeGuard = 0;
        while (safeGuard < enemyConfigs.Count && enemyConfigs[nextIndex] == null)
        {
            nextIndex = (nextIndex + 1) % enemyConfigs.Count;
            safeGuard++;
        }

        var cfg = enemyConfigs[nextIndex];
        if (cfg == null) return null;

        nextIndex = (nextIndex + 1) % enemyConfigs.Count;
        return cfg;
    }

    [Button(ButtonSizes.Small)]
    public void ResetIndex(int index = 0)
    {
        nextIndex = Mathf.Max(0, index);
        if (enemyConfigs != null && enemyConfigs.Count > 0)
        {
            nextIndex %= enemyConfigs.Count;
        }
    }
}


