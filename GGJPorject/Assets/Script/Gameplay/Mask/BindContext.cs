using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BindContext
{
    public MaskObj Mask { get; }
    public IReadOnlyList<MaterialObj> Materials { get; }

    /// <summary>
    /// 绑定完成回调入口（用于即时材料的连锁/统计/UI 刷新）。
    /// </summary>
    public Action<MaterialObj> OnMaterialBound { get; }

    public BindContext(MaskObj mask, IReadOnlyList<MaterialObj> materials, Action<MaterialObj> onMaterialBound)
    {
        Mask = mask;
        Materials = materials;
        OnMaterialBound = onMaterialBound;
    }
}


