using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>
/// Jam 测试逻辑：当策划还没配置任何怪物列表时，提供一个可跑流程的默认怪物生成。
/// </summary>
public class JamTestMonsterSpawnLogic : MonoBehaviour, IMonsterSpawnLogic
{
    [Header("Base")]
    [SerializeField] private float baseHP = 100f;
    [SerializeField] private float baseATK = 35f;
    [SerializeField] private float baseDEF = 50f;
    [SerializeField] private int baseSpeedRate = 21;

    [Header("Per Round Growth")]
    [SerializeField] private float hpPerRound = 30f;
    [SerializeField]
    private float[] hpRounds = new float[5]
    {
        20f,32f,50f,64f,90f
    };
    [SerializeField] private float atkPerRound = 15f;
    [SerializeField]
    private float[] atkRounds = new float[5]
    {
        30f,48f,75f,96f,135f
    };
    [SerializeField] private float defPerRound = 20f;
    [SerializeField]
    private float[] defRounds = new float[5]
    {
        25f,40f,63f,80f,113f
    };
    [SerializeField] private int speedPerRound = 5;
    [SerializeField]
    private float[] spdRounds = new float[5]
    {
        10f,16f,25f,32f,45f
    };
    [SerializeField]
    private float[] cirtRounds = new float[5]
    {
        0.02f,0.032f,0.05f,0.064f,0.09f
    };
    [SerializeField]
    private float[] cirtDamRounds = new float[5]
    {
        0.01f,0.03f,0.025f,0.032f,0.045f
    };
    int hpUp = 0;
    int atkUp = 0;
    int defUp = 0;
    int critUp = 0;
    int critMUp = 0;
    int spdUp = 0;

    public CharacterConfig TrySpawn(int roundIndex, FightContext context)
    {
        // roundIndex 从 0 开始
        //int r = Mathf.Max(0, roundIndex);
        //基础数值
        if (roundIndex > 0)
        {
            int num = Random.Range(0, 2);

            for (int i = 0; i < num; i++)
            {
                int a = Random.Range(1, 3);
                switch (a)
                {
                    case 1:
                        hpUp += 1;
                        break;
                    case 2:
                        atkUp += 1;
                        break;
                    case 3:
                        defUp += 1;
                        break;
                    case 4:
                        spdUp += 1;
                        break;
                    case 5:
                        hpUp += 1;
                        atkUp += 1;
                        defUp += 1;
                        critUp += 1;
                        critMUp += 1;
                        spdUp += 1;
                        break;
                }
            }
            for (int i = 0; i < num; i++)
            {
                int a = Random.Range(1, 3);
                switch (a)
                {
                    case 1:
                        hpUp += 1;
                        break;
                    case 2:
                        atkUp += 1;
                        break;
                    case 3:
                        defUp += 1;
                        break;
                    case 4:
                        spdUp += 1;
                        break;
                    case 5:
                        hpUp += 1;
                        atkUp += 1;
                        defUp += 1;
                        critUp += 1;
                        critMUp += 1;
                        spdUp += 1;
                        break;
                }
            }
            for (int i = 0; i < num; i++)
            {
                int a = Random.Range(1, 3);
                switch (a)
                {
                    case 1:
                        hpUp += 1;
                        break;
                    case 2:
                        atkUp += 1;
                        break;
                    case 3:
                        defUp += 1;
                        break;
                    case 4:
                        spdUp += 1;
                        break;
                    case 5:
                        hpUp += 1;
                        atkUp += 1;
                        defUp += 1;
                        critUp += 1;
                        critMUp += 1;
                        spdUp += 1;
                        break;
                }
            }
            for (int i = 0; i < num; i++)
            {
                int a = Random.Range(1, 3);
                switch (a)
                {
                    case 1:
                        hpUp += 1;
                        break;
                    case 2:
                        atkUp += 1;
                        break;
                    case 3:
                        defUp += 1;
                        break;
                    case 4:
                        spdUp += 1;
                        break;
                    case 5:
                        hpUp += 1;
                        atkUp += 1;
                        defUp += 1;
                        critUp += 1;
                        critMUp += 1;
                        spdUp += 1;
                        break;
                }
            }
            for (int i = 0; i < num; i++)
            {
                int a = Random.Range(1, 3);
                switch (a)
                {
                    case 1:
                        hpUp += 1;
                        break;
                    case 2:
                        atkUp += 1;
                        break;
                    case 3:
                        defUp += 1;
                        break;
                    case 4:
                        spdUp += 1;
                        break;
                    case 5:
                        hpUp += 1;
                        atkUp += 1;
                        defUp += 1;
                        critUp += 1;
                        critMUp += 1;
                        spdUp += 1;
                        break;
                }
            }
        }
        //增长数值
        return new CharacterConfig
        {

            HPBase = baseHP + hpPerRound * hpUp,
            ATKBase = baseATK + atkPerRound * atkUp,
            DEFBase = baseDEF + defPerRound * defUp,
            CritChance = 0.05f + 0.05f*critUp + roundIndex/100f,
            CritMultiplier = 1.5f + 1.5f*critMUp,
            SpeedRate = Mathf.Max(0, baseSpeedRate + speedPerRound * spdUp),
        };
    }
}
/*using UnityEngine;

/// <summary>
/// Jam 测试逻辑：当策划还没配置任何怪物列表时，提供一个可跑流程的默认怪物生成。
/// 修改版：采用复合增长公式，前期平滑后期指数膨胀，以跟上玩家装备转化成长
/// </summary>
public class JamTestMonsterSpawnLogic : MonoBehaviour, IMonsterSpawnLogic
{
    [Header("Base")]
    [SerializeField] private float baseHP = 100f;
    [SerializeField] private float baseATK = 20f;
    [SerializeField] private float baseDEF = 100f;
    [SerializeField] private int baseSpeedRate = 6;

    [Header("Per Round Growth")]
    [SerializeField] private float hpPerRound = 50f;  // 增加基础增量
    [SerializeField] private float[] hpRounds = new float[5]
    {
        20f, 50f, 100f, 200f, 400f  // 增强后期加成
    };
    [SerializeField] private float atkPerRound = 10f; // 增加基础增量
    [SerializeField] private float[] atkRounds = new float[5]
    {
        30f, 60f, 120f, 240f, 480f  // 增强后期加成
    };
    [SerializeField] private float defPerRound = 15f; // 防御增长稍慢
    [SerializeField] private float[] defRounds = new float[5]
    {
        25f, 50f, 100f, 200f, 400f  // 增强后期加成
    };
    [SerializeField] private int speedPerRound = 5;
    [SerializeField] private float[] spdRounds = new float[5]
    {
        10f, 20f, 40f, 80f, 160f    // 增强后期加成
    };
    [SerializeField] private float[] cirtRounds = new float[5]
    {
        0.02f, 0.04f, 0.08f, 0.16f, 0.32f  // 增强后期加成
    };
    [SerializeField] private float[] cirtDamRounds = new float[5]
    {
        0.01f, 0.02f, 0.04f, 0.08f, 0.16f  // 增强后期加成
    };

    [Header("Growth Parameters")]
    [SerializeField] private float linearPhaseEnd = 2f;     // 线性阶段结束轮次
    [SerializeField] private float exponentialStart = 3f;   // 指数阶段开始轮次
    [SerializeField] private float exponentialFactor = 1.15f; // 指数系数
    [SerializeField] private float playerCompensationFactor = 0.3f; // 玩家强度补偿系数

    // 缓存玩家强度（简化实现）
    private float cachedPlayerPower = 100f;

    public CharacterConfig TrySpawn(int roundIndex, FightContext context)
    {
        // 更新玩家强度缓存
        UpdatePlayerPowerCache(context);

        // 计算复合增长倍率
        float growthMultiplier = CalculateGrowthMultiplier(roundIndex);

        // 获取阶段索引（每5轮一个阶段）
        int stageIndex = Mathf.Min(roundIndex / 5, hpRounds.Length - 1);

        // 计算基础线性增长部分
        float linearGrowth = roundIndex; // 直接使用轮次作为增长基数

        // 计算各个属性
        float hp = CalculateAttribute(
            baseHP, 
            hpPerRound, 
            hpRounds, 
            roundIndex, 
            stageIndex, 
            growthMultiplier, 
            linearGrowth,
            1.0f  // 生命值增长最快
        );

        float atk = CalculateAttribute(
            baseATK, 
            atkPerRound, 
            atkRounds, 
            roundIndex, 
            stageIndex, 
            growthMultiplier, 
            linearGrowth,
            0.8f  // 攻击增长稍慢
        );

        float def = CalculateAttribute(
            baseDEF, 
            defPerRound, 
            defRounds, 
            roundIndex, 
            stageIndex, 
            growthMultiplier, 
            linearGrowth,
            0.6f  // 防御增长最慢
        );

        // 计算速度（速度增长相对独立）
        float speedGrowth = linearGrowth * 0.5f; // 速度增长减半
        float spd = baseSpeedRate + speedPerRound * speedGrowth + 
                   spdRounds[stageIndex] * Mathf.Min(growthMultiplier, 2f);

        // 计算暴击属性
        float critChance = 0.05f + 
                          cirtRounds[stageIndex] * Mathf.Min(growthMultiplier, 3f);
        float critMultiplier = 1.5f + 
                              cirtDamRounds[stageIndex] * Mathf.Min(growthMultiplier, 2f);

        return new CharacterConfig
        {
            HPBase = hp,
            ATKBase = atk,
            DEFBase = def,
            CritChance = Mathf.Clamp(critChance, 0.05f, 0.5f), // 限制暴击率上限
            CritMultiplier = Mathf.Clamp(critMultiplier, 1.5f, 3.0f), // 限制暴击伤害上限
            SpeedRate = Mathf.Max(1, (int)spd), // 确保速度至少为1
        };
    }

    /// <summary>
    /// 计算复合增长倍率
    /// </summary>
    private float CalculateGrowthMultiplier(int roundIndex)
    {
        float r = roundIndex;

        if (r <= linearPhaseEnd && r > 0)
        {
            // 前期：线性平缓增长
            // 1-10轮：从1.0增长到2.0
            return 1.0f + (r / linearPhaseEnd) * 1.0f;
        }
        else if (r <= exponentialStart)
        {
            // 中期：多项式加速增长
            // 11-20轮：从2.0增长到5.0
            float normalized = (r - linearPhaseEnd) / (exponentialStart - linearPhaseEnd);
            return 2.0f + Mathf.Pow(normalized, 1.5f) * 3.0f;
        }
        else if( r > exponentialStart)
        {
            // 后期：指数爆炸增长
            // 21+轮：指数增长
            int expRounds = Mathf.Max(0, roundIndex - (int)exponentialStart);
            return 5.0f * Mathf.Pow(exponentialFactor, expRounds * 0.5f);
        }
        return r;
    }

    /// <summary>
    /// 计算单个属性值
    /// </summary>
    private float CalculateAttribute(
        float baseValue, 
        float perRound, 
        float[] roundMultipliers, 
        int roundIndex, 
        int stageIndex, 
        float growthMultiplier, 
        float linearGrowth,
        float attributeFactor)
    {
        // 1. 基础线性部分
        float linearPart = baseValue + perRound * linearGrowth * attributeFactor;

        // 2. 阶段加成部分
        float stagePart = roundMultipliers[stageIndex] * Mathf.Min(growthMultiplier, 3f);

        // 3. 指数增长部分（仅后期生效）
        float exponentialPart = 0;
        if (roundIndex > exponentialStart)
        {
            int expRounds = roundIndex - (int)exponentialStart;
            exponentialPart = baseValue * (Mathf.Pow(exponentialFactor, expRounds * 0.3f) - 1) * attributeFactor;
        }

        // 4. 玩家强度补偿（基于缓存的玩家强度）
        float playerCompPart = cachedPlayerPower * playerCompensationFactor * 
                              (roundIndex / 100f) * attributeFactor;

        // 组合所有部分
        float result = linearPart + stagePart + exponentialPart + playerCompPart;

        // 确保最小值
        return Mathf.Max(baseValue * 0.5f, result);
    }

    /// <summary>
    /// 更新玩家强度缓存
    /// </summary>
    private void UpdatePlayerPowerCache(FightContext context)
    {
        if (context == null) return;

        // 简化的玩家强度估算公式
        // 假设玩家强度 ≈ 生命值 × 攻击力 × 0.01
        if (Player.I.BuildBattleStats().MaxHP > 0 && Player.I.BuildBattleStats().Attack > 0)
        {
            cachedPlayerPower = Player.I.BuildBattleStats().MaxHP * Player.I.BuildBattleStats().Attack * 0.01f;
        }
        else
        {
            // 默认强度增长（每轮增加5%）
            cachedPlayerPower *= 1.05f;
        }
    }

    /// <summary>
    /// 工具方法：打印某轮的怪物属性（用于调试）
    /// </summary>
    public void DebugRound(int roundIndex)
    {
        var config = TrySpawn(roundIndex, null);
        Debug.Log($"Round {roundIndex}: HP={config.HPBase:F0}, ATK={config.ATKBase:F0}, " +
                 $"DEF={config.DEFBase:F0}, SPD={config.SpeedRate}, " +
                 $"CRIT={config.CritChance:P1}, CRITDMG={config.CritMultiplier:F2}x");
    }
}
*/



