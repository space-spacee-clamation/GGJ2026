using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 材料逻辑节点（树状）：条件节点可挂子节点；条件不满足时仅跳过该分支，不影响其它分支。
/// 说明：
/// - 节点可引用同一个组件（组件复用），因为 node 只是“引用 + 结构”。
/// - 具体执行能力由引用的组件接口决定（Gate / BattleStart / AttackModify / DamageApplied / BattleEnd / Bind）。
/// </summary>
[Serializable]
public sealed class MaterialLogicNode
{
    [Tooltip("仅用于编辑器显示/备注，不参与逻辑。")]
    public string Title;

    [Tooltip("编辑器折叠状态（仅用于编辑器显示）。")]
    public bool Expanded = true;

    [Tooltip("节点引用的组件（通常是材料 prefab 上的某个 MonoBehaviour）。")]
    public MonoBehaviour Component;

    [Tooltip("节点角色：Auto=自动推断；Condition=条件节点；Action=行动节点。")]
    public MaterialLogicNodeRole Role = MaterialLogicNodeRole.Auto;

    [Tooltip("行动侧筛选：仅在“行动/攻击事件相关阶段”（AttackModify/DamageApplied）生效。")]
    public MaterialActionSideFilter ActionSide = MaterialActionSideFilter.Both;

    [Tooltip("子节点（条件通过时/行动执行后继续遍历）。")]
    public List<MaterialLogicNode> Children = new();
}


