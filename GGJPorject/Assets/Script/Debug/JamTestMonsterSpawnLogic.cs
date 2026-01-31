using UnityEngine;

/// <summary>
/// Jam 测试逻辑：当策划还没配置任何怪物列表时，提供一个可跑流程的默认怪物生成。
/// </summary>
public class JamTestMonsterSpawnLogic : MonoBehaviour, IMonsterSpawnLogic
{
    [Header("Base")]
    [SerializeField] private float baseHP = 150f;
    [SerializeField] private float baseATK = 30f;
    [SerializeField] private float baseDEF = 50f;
    [SerializeField] private int baseSpeedRate = 12;

    [Header("Per Round Growth")]
    [SerializeField] private float hpPerRound = 8f;
    [SerializeField] private float atkPerRound = 1.5f;
    [SerializeField] private float defPerRound = 0.4f;
    [SerializeField] private int speedPerRound = 0;

    public CharacterConfig TrySpawn(int roundIndex, FightContext context)
    {
        // roundIndex 从 0 开始
        //int r = Mathf.Max(0, roundIndex);
        int r = 0;
        if(roundIndex <= 5 && roundIndex >1)
        {
           r = (roundIndex + 1) * 2;  
        }
        else if(roundIndex <= 20 && roundIndex > 5)
        {
            r = (roundIndex + 1) * 3;
        }
        else if(roundIndex > 20)
        {
            r = (roundIndex - 1) / 20;
        }
        return new CharacterConfig
        {
            HPBase = baseHP + hpPerRound * r,
            ATKBase = baseATK + atkPerRound * r,
            DEFBase = baseDEF + defPerRound * r,
            CritChance = 0.05f,
            CritMultiplier = 1.5f,
            SpeedRate = Mathf.Max(0, baseSpeedRate + speedPerRound * r),
        };
    }
}



