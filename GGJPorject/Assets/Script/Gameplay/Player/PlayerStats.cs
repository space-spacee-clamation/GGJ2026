using System;
using UnityEngine;

[Serializable]
public struct PlayerStats
{
    public float MaxHP;
    public float Attack;
    public float Defense;

    [Range(0f, 1f)] public float CritChance;
    [Min(1f)] public float CritMultiplier;

    [Min(0)] public int SpeedRate;

    [Range(0, 100)] public int Luck;

    public void Clamp()
    {
        MaxHP = Mathf.Max(1f, MaxHP);
        Attack = Mathf.Max(0f, Attack);
        Defense = Mathf.Max(0f, Defense);
        CritChance = Mathf.Clamp01(CritChance);
        CritMultiplier = Mathf.Max(1f, CritMultiplier);
        SpeedRate = Mathf.Max(0, SpeedRate);
        Luck = Mathf.Clamp(Luck, 0, 100);
    }
}



