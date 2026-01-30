using System;
using UnityEngine;

[Serializable]
public sealed class MaterialDropEntry
{
    public MaterialObj MaterialPrefab;
    [Min(1)] public int Count = 1;
}


