using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Jam 用的“UnityTest 类”（运行时断言 + Debug.Log）。
/// 目标：验证逻辑树 + Gate_Phase 的算法流程是否符合预期：
/// - 每 1 次行动：攻击 +1
/// - 每 2 次行动：攻击 +3
/// - 结算（PersistentGrowth）：成长攻击 +10
/// </summary>
public sealed class JamMaterialLogicTreeUnityTest : MonoBehaviour
{
    [SerializeField] private bool runOnStart = true;

    private void Start()
    {
        if (!runOnStart) return;
        Run();
    }

    [ContextMenu("Run Jam Material LogicTree UnityTest")]
    public void Run()
    {
        Debug.Log("[JamMaterialLogicTreeUnityTest] Start");

        // ---- Build material prefab instance in memory ----
        var go = new GameObject("tmp_mat_test_go");
        try
        {
            var mat = go.AddComponent<MaterialObj>();

            // Components used by tree
            var phaseAttack = go.AddComponent<Gate_Phase>();
            phaseAttack.Phase = MaterialTraversePhase.AttackModify;

            var every1 = go.AddComponent<Gate_WaitEveryX>();
            every1.EveryX = 1;

            var add1 = go.AddComponent<TestAtk_AddRawAttack>();
            add1.Add = 1f;
            var node1 = go.AddComponent<Node_Effect>();
            node1.Effect = add1;

            var every2 = go.AddComponent<Gate_WaitEveryX>();
            every2.EveryX = 2;

            var add3 = go.AddComponent<TestAtk_AddRawAttack>();
            add3.Add = 3f;
            var node3 = go.AddComponent<Node_Effect>();
            node3.Effect = add3;

            var phaseGrowth = go.AddComponent<Gate_Phase>();
            phaseGrowth.Phase = MaterialTraversePhase.PersistentGrowth;

            var growthAdd = go.AddComponent<TestGrowth_AddAttack>();
            growthAdd.Add = 10f;
            var nodeGrowth = go.AddComponent<Node_Effect>();
            nodeGrowth.Effect = growthAdd;

            // ---- Compose logic tree ----
            var roots = new List<MaterialLogicNode>
            {
                new MaterialLogicNode
                {
                    Component = phaseAttack,
                    Children = new List<MaterialLogicNode>
                    {
                        new MaterialLogicNode
                        {
                            Component = every1,
                            Children = new List<MaterialLogicNode>
                            {
                                new MaterialLogicNode { Component = node1 }
                            }
                        },
                        new MaterialLogicNode
                        {
                            Component = every2,
                            Children = new List<MaterialLogicNode>
                            {
                                new MaterialLogicNode { Component = node3 }
                            }
                        }
                    }
                },
                new MaterialLogicNode
                {
                    Component = phaseGrowth,
                    Children = new List<MaterialLogicNode>
                    {
                        new MaterialLogicNode { Component = nodeGrowth }
                    }
                }
            };

            SetPrivateLogicTreeRoots(mat, roots);

            // ---- Validate AttackModify: action#1 => +1 ----
            var fc = new FightContext
            {
                CurrentAttackerSide = FightSide.Player,
                CurrentActionNumber = 1,
                CurrentAttackerAttackNumber = 1,
                DebugVerbose = false,
            };

            var runner = new MaterialRuntimeRunner(mat);
            var info = new AttackInfo { RawAttack = 0f, BaseValue = 0f, CritChance = 0f, CritMultiplier = 1f, IsCrit = false, FinalDamage = 0f };
            runner.Modify(ref info, fc);
            Assert.AreApproximatelyEqual(1f, info.RawAttack, 0.0001f, "Action#1 应仅触发 +1");

            // ---- Validate AttackModify: action#2 => +1 +3 ----
            fc.CurrentActionNumber = 2;
            fc.CurrentAttackerAttackNumber = 2;
            info.RawAttack = 0f;
            runner.Modify(ref info, fc);
            Assert.AreApproximatelyEqual(4f, info.RawAttack, 0.0001f, "Action#2 应触发 +1 与 +3");

            // ---- Validate AttackModify: action#3 => +1 ----
            fc.CurrentActionNumber = 3;
            fc.CurrentAttackerAttackNumber = 3;
            info.RawAttack = 0f;
            runner.Modify(ref info, fc);
            Assert.AreApproximatelyEqual(1f, info.RawAttack, 0.0001f, "Action#3 应仅触发 +1");

            // ---- Validate PersistentGrowth ----
            var delta = new PlayerGrowthDelta();
            var growthCtx = new MaterialVommandeTreeContext(
                MaterialTraversePhase.PersistentGrowth,
                mask: null,
                maskMaterials: null,
                onMaterialBound: null,
                fight: fc,
                side: FightSide.None,
                defenderSide: FightSide.None,
                actionNumber: 0,
                attackerAttackNumber: 0,
                attackInfo: default,
                damage: 0f,
                player: null,
                growthDelta: delta
            );
            TraverseTree_PersistentGrowth(mat.LogicTreeRoots, in growthCtx, delta, fc);
            Assert.AreApproximatelyEqual(10f, delta.AddAttack, 0.0001f, "PersistentGrowth 应写入 +10 攻击成长");

            Debug.Log("[JamMaterialLogicTreeUnityTest] PASS");
        }
        finally
        {
            Destroy(go);
        }
    }

    private static void SetPrivateLogicTreeRoots(MaterialObj mat, List<MaterialLogicNode> roots)
    {
        var f = typeof(MaterialObj).GetField("logicTreeRoots", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(f, "找不到 MaterialObj.logicTreeRoots 私有字段（字段名变了？）");
        f.SetValue(mat, roots);
    }

    private static void TraverseTree_PersistentGrowth(IReadOnlyList<MaterialLogicNode> nodes, in MaterialVommandeTreeContext tctx, PlayerGrowthDelta delta, FightContext fight)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate gate && gate.ShouldBreak(in tctx))
            {
                continue;
            }

            if (c is IPersistentGrowthProvider p)
            {
                p.OnCollectPersistentGrowth(null, delta, fight);
            }

            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseTree_PersistentGrowth(n.Children, in tctx, delta, fight);
            }
        }
    }

    private sealed class TestAtk_AddRawAttack : MonoBehaviour, IMaterialAttackInfoEffect, IMaterialDescriptionProvider
    {
        public float Add = 1f;

        public void Modify(ref AttackInfo info, in MaterialVommandeTreeContext context)
        {
            info.RawAttack += Add;
        }

        public void AppendDescription(System.Text.StringBuilder sb)
        {
            if (sb == null) return;
            sb.Append($"攻击 {(Add >= 0 ? "+" : "")}{Add:0.##}");
        }
    }

    private sealed class TestGrowth_AddAttack : MonoBehaviour, IMaterialEffect, IMaterialDescriptionProvider
    {
        public float Add = 10f;

        public void Execute(in MaterialVommandeTreeContext context)
        {
            if (context.GrowthDelta == null) return;
            context.GrowthDelta.AddAttack += Add;
        }

        public void AppendDescription(System.Text.StringBuilder sb)
        {
            if (sb == null) return;
            sb.Append($"成长攻击 {(Add >= 0 ? "+" : "")}{Add:0.##}");
        }
    }
}


