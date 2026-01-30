using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 怪物生成系统：内部维护“生成逻辑链”，按顺序尝试生成，返回第一个非空结果。
/// 初始化必须由 GameManager.Awake() 完成。
/// </summary>
public class MonsterSpawnSystem : MonoBehaviour
{
    public static MonsterSpawnSystem I { get; private set; }

    private readonly List<IMonsterSpawnLogic> _logics = new();

    public void Initialize()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        _logics.Clear();

        // 生成逻辑链来源：挂在本 GameObject 上的 MonoBehaviour（实现 IMonsterSpawnLogic）
        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMonsterSpawnLogic logic)
            {
                _logics.Add(logic);
            }
        }

        // Jam 容错：如果没有任何逻辑，就自动挂一个“顺序取列表”的逻辑（由该组件自己持有怪物列表）
        if (_logics.Count == 0)
        {
            var seq = GetComponent<SequentialEnemySpawnLogic>();
            if (seq == null) seq = gameObject.AddComponent<SequentialEnemySpawnLogic>();
            _logics.Add(seq);
        }
    }

    public CharacterConfig Spawn(int roundIndex, FightContext context)
    {
        for (int i = 0; i < _logics.Count; i++)
        {
            var cfg = _logics[i]?.TrySpawn(roundIndex, context);
            if (cfg != null) return cfg;
        }
        return null;
    }
}


