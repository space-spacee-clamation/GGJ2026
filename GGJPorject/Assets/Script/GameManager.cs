using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Audio (Runtime Created)")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private AudioTimeline audioTimeline;

    [Header("Fight (Runtime Created)")]
    [SerializeField] private FightManager fightManager;

    [Header("Spawn (Runtime Created)")]
    [SerializeField] private MonsterSpawnSystem monsterSpawnSystem;

    [Header("Mask (Runtime Created)")]
    [SerializeField] private MaskMakeManager maskMakeManager;

    [Header("UI")]
    [SerializeField] private MakeMuskUI makeMuskUI;
    [SerializeField] private BattleUI battleUI;

    [Header("Mask Library (Runtime)")]
    [SerializeField] private Transform maskLibraryRoot;
    private readonly System.Collections.Generic.List<MaskObj> _maskLibrary = new();

    [Header("材料库存")]
    [SerializeField] private MaterialInventory materialInventory = new();
    [SerializeField] private Transform materialInventoryRoot;

    [Header("制造阶段（可选自动行为）")]
    [Tooltip("Jam 方便测试：进入制造回合时自动把库存材料尽量绑定到当前面具（按保质期优先）。")]
    [SerializeField] private bool autoBindInventoryOnMake = false;

    [Header("掉落配置（Jam 默认：纯代码，无 SO）")]
    [Tooltip("运行时生成的材料池（由 Resources/Mat 扫描得到）。Jam 默认不需要手动配置 MaterialPool SO。")]
    [SerializeField] private MaterialPool dropPool;
    [Tooltip("运行时创建的掉落方法（SO 实例，仅用于运行时；Jam 默认不需要创建 DropMethod 资产）。")]
    [SerializeField] private SimpleLuckMaterialDropMethod dropMethod;
    [SerializeField, Min(0)] private int dropCount = JamDefaultSettings.DropCountPerBattle;

    [Header("材料池/初始材料（Jam 默认：纯代码）")]
    [SerializeField] private string resourcesMatFolder = JamDefaultSettings.ResourcesMatFolder;
    [SerializeField, Min(0)] private int initialCommonMaterialCount = JamDefaultSettings.InitialCommonMaterialCount;
    private bool _initialMaterialsSpawned;

    [Header("Flow")]
    [SerializeField] private bool autoRunLoop = false;

    [Header("流程控制（UniTask）")]
    [Tooltip("进入制造阶段后，是否自动视为玩家已完成制造（仅用于快速测试）。")]
    [SerializeField] private bool autoCompleteMakePhase = false;

    [Header("Jam 自动化/调试")]
    [SerializeField] private bool enableJamAutoFixes = true;
    [SerializeField] private bool enablePhaseDebugLogs = true;
    private int _roundIndex;

    private UniTaskCompletionSource<bool> _makePhaseTcs;
    private UniTaskCompletionSource<bool> _battleEndTcs;
    private CancellationToken _destroyToken;
    private bool _manualAdvanceInProgress;

    private void Awake()
    {
        // Singleton bootstrap (Jam 简化：重复实例直接销毁)
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
        // 所有单例/管理类必须在此初始化（强约束）
        BootstrapAudio();
        BootstrapFight();
        BootstrapSpawn();
        BootstrapMask();
        BootstrapPlayer();
        BootstrapRuntimeRoots();

        _destroyToken = this.GetCancellationTokenOnDestroy();

        if (enablePhaseDebugLogs)
        {
            Debug.Log("[GameManager] Awake 完成：系统 Bootstrap 完毕。");
        }
    }

    private void Start()
    {
        // 其它初始化放在 Start（强约束）
        if (audioManager != null)
        {
            audioManager.LoadAllEntriesFromResources();
        }

        // Jam：自动构建材料池（用于掉落与开局发牌）。不依赖任何 SO 资产。
        BuildDropPoolFromResources();

        // Jam：自动化掉落配置（保证 dropMethod/dropCount 可用）
        EnsureDropConfigForJam();

        // Jam：开局发放默认数量的 Common 材质给玩家
        SpawnInitialCommonMaterialsIfNeeded();

        // Jam 容错：用临时测试补全未配置内容，保证流程可跑
        if (enableJamAutoFixes)
        {
            new JamTempFixer().Apply(this);
        }

        if (autoRunLoop)
        {
            RunMainLoopAsync().Forget();
        }
    }

    private void BuildDropPoolFromResources()
    {
        var mats = Resources.LoadAll<MaterialObj>(resourcesMatFolder);
        if (mats == null || mats.Length == 0)
        {
            Debug.LogWarning($"[GameManager] Resources/{resourcesMatFolder} 未找到任何 MaterialObj，无法自动生成材料池。");
            return;
        }

        var pool = ScriptableObject.CreateInstance<MaterialPool>();
        pool.name = $"RuntimeMaterialPool_{resourcesMatFolder}";

        for (int i = 0; i < mats.Length; i++)
        {
            var prefab = mats[i];
            if (prefab == null) continue;

            var entry = new MaterialPoolEntry { MaterialPrefab = prefab, Weight = 1 };
            switch (prefab.Quality)
            {
                case MaterialQuality.Common: pool.Common.Add(entry); break;
                case MaterialQuality.Uncommon: pool.Uncommon.Add(entry); break;
                case MaterialQuality.Rare: pool.Rare.Add(entry); break;
                case MaterialQuality.Epic: pool.Epic.Add(entry); break;
                case MaterialQuality.Legendary: pool.Legendary.Add(entry); break;
                default: pool.Common.Add(entry); break;
            }
        }

        dropPool = pool;
        Debug.Log($"[GameManager] 已从 Resources/{resourcesMatFolder} 自动生成材料池：Common={pool.Common.Count}, Uncommon={pool.Uncommon.Count}, Rare={pool.Rare.Count}, Epic={pool.Epic.Count}, Legendary={pool.Legendary.Count}");
    }

    private void EnsureDropConfigForJam()
    {
        // dropMethod: 如果没配，运行时创建一个默认实现（SO 实例）
        if (dropMethod == null)
        {
            dropMethod = ScriptableObject.CreateInstance<SimpleLuckMaterialDropMethod>();
            dropMethod.name = "Runtime_SimpleLuckDropMethod";
            if (enablePhaseDebugLogs) Debug.Log("[GameManager] dropMethod 未配置：已创建 Runtime_SimpleLuckDropMethod。");
        }

        // Jam：dropCount 直接走 JamDefaultSettings 的默认值（可在 JamDefaultSettings 改）
        dropCount = Mathf.Max(0, JamDefaultSettings.DropCountPerBattle);
    }

    private void SpawnInitialCommonMaterialsIfNeeded()
    {
        if (_initialMaterialsSpawned) return;
        _initialMaterialsSpawned = true;

        if (initialCommonMaterialCount <= 0) return;
        if (materialInventory == null)
        {
            Debug.LogError("[GameManager] materialInventory 为空，无法发放开局材料。", this);
            return;
        }
        if (materialInventoryRoot == null)
        {
            Debug.LogError("[GameManager] materialInventoryRoot 未初始化，无法实例化开局材料。", this);
            return;
        }

        if (dropPool == null)
        {
            Debug.LogWarning("[GameManager] dropPool 为空，无法从材料池发放开局材料。");
            return;
        }

        var list = dropPool.GetList(MaterialQuality.Common);
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("[GameManager] 材料池 Common 列表为空，无法发放开局材料。");
            return;
        }

        // 不强求不重复：数量不足时允许重复抽取（Jam 简化）
        var used = new HashSet<int>();
        for (int n = 0; n < initialCommonMaterialCount; n++)
        {
            int pick;
            if (list.Count >= initialCommonMaterialCount)
            {
                // 尽量不重复
                int safety = 200;
                do
                {
                    pick = Random.Range(0, list.Count);
                } while (!used.Add(pick) && --safety > 0);
            }
            else
            {
                pick = Random.Range(0, list.Count);
            }

            var prefab = list[pick]?.MaterialPrefab;
            if (prefab == null) continue;

            var inst = Instantiate(prefab, materialInventoryRoot, false);
            inst.ResetInventoryShelfLife();
            materialInventory.Add(inst);
        }

        if (makeMuskUI != null && makeMuskUI.gameObject.activeInHierarchy)
        {
            makeMuskUI.RefreshInventoryUI();
        }
    }

    /// <summary>
    /// UI/外部在玩家完成“制造面具回合”（材料附加结束）后调用，GameManager 会继续进入战斗。
    /// </summary>
    public void NotifyMakeMaskFinished()
    {
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 制造阶段完成（Next）。round={_roundIndex}");
        if (_makePhaseTcs != null)
        {
            _makePhaseTcs.TrySetResult(true);
            return;
        }

        // Jam/手动模式兜底：如果主循环没跑起来（或 UI 被直接打开），则 Next 直接推进一次完整流程
        if (!_manualAdvanceInProgress)
        {
            _manualAdvanceInProgress = true;
            AdvanceFromMakeUIAsync(_destroyToken).Forget();
        }
        else
        {
            if (enablePhaseDebugLogs) Debug.LogWarning("[GameManager] 手动推进中，忽略重复 Next。");
        }
    }

    private async UniTaskVoid AdvanceFromMakeUIAsync(CancellationToken ct)
    {
        try
        {
            // 2) 制造阶段结束：材料库存保质期结算
            if (enablePhaseDebugLogs) Debug.Log($"[GameManager]（手动）制造阶段结算：库存 TickEndOfMakePhase。round={_roundIndex}");
            materialInventory?.TickEndOfMakePhase(materialInventoryRoot);

            // 3) 战斗阶段：开始战斗并等待结束
            if (enablePhaseDebugLogs) Debug.Log($"[GameManager]（手动）进入战斗阶段。round={_roundIndex}");
            await StartBattlePhaseAsync(ct);

            if (enablePhaseDebugLogs) Debug.Log($"[GameManager]（手动）回合结束。round={_roundIndex}");
            _roundIndex++;

            // 回到制造阶段（方便继续点）
            EnterMakeMaskPhase();
        }
        finally
        {
            _manualAdvanceInProgress = false;
        }
    }

    private async UniTaskVoid RunMainLoopAsync()
    {
        while (!_destroyToken.IsCancellationRequested)
        {
            await RunOneRoundAsync(_destroyToken);
        }
    }

    private async UniTask RunOneRoundAsync(CancellationToken ct)
    {
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] RoundStart round={_roundIndex}");
        // 1) 制造阶段：生成底板面具，等待玩家完成合成
        EnterMakeMaskPhase();

        _makePhaseTcs = new UniTaskCompletionSource<bool>();
        if (autoCompleteMakePhase) _makePhaseTcs.TrySetResult(true);

        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 等待制造阶段结束... round={_roundIndex}");
        await _makePhaseTcs.Task.AttachExternalCancellation(ct);

        // 2) 制造阶段结束：材料库存保质期结算
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 制造阶段结算：库存 TickEndOfMakePhase。round={_roundIndex}");
        materialInventory?.TickEndOfMakePhase(materialInventoryRoot);

        // 3) 战斗阶段：开始战斗并等待结束
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 进入战斗阶段。round={_roundIndex}");
        await StartBattlePhaseAsync(ct);

        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] RoundEnd round={_roundIndex}");
        _roundIndex++;
    }

    private void BootstrapAudio()
    {
        if (audioTimeline == null)
        {
            var go = new GameObject("AudioTimeline");
            go.transform.SetParent(transform, false);
            audioTimeline = go.AddComponent<AudioTimeline>();
        }

        if (audioManager == null)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(transform, false);
            audioManager = go.AddComponent<AudioManager>();
        }

        audioTimeline.Initialize();
        audioManager.Initialize(audioTimeline);
    }

    private void BootstrapFight()
    {
        if (fightManager == null)
        {
            var go = new GameObject("FightManager");
            go.transform.SetParent(transform, false);
            fightManager = go.AddComponent<FightManager>();
        }

        fightManager.Initialize();
    }

    private void BootstrapSpawn()
    {
        if (monsterSpawnSystem == null)
        {
            var go = new GameObject("MonsterSpawnSystem");
            go.transform.SetParent(transform, false);
            monsterSpawnSystem = go.AddComponent<MonsterSpawnSystem>();
        }
        monsterSpawnSystem.Initialize();
    }

    private void BootstrapMask()
    {
        if (maskMakeManager == null)
        {
            var go = new GameObject("MaskMakeManager");
            go.transform.SetParent(transform, false);
            maskMakeManager = go.AddComponent<MaskMakeManager>();
        }

        maskMakeManager.Initialize();
    }

    private void BootstrapPlayer()
    {
        if (Player.I != null) return;
        // Jam：不依赖 PlayerConfigSO，直接使用默认值（改 JamDefaultSettings 即可调参）
        Player.CreateSingleton(JamDefaultSettings.DefaultPlayerBaseStats);
    }

    private void BootstrapRuntimeRoots()
    {
        if (maskLibraryRoot == null)
        {
            var go = new GameObject("MaskLibraryRoot");
            go.transform.SetParent(transform, false);
            maskLibraryRoot = go.transform;
        }
        if (materialInventoryRoot == null)
        {
            var go = new GameObject("MaterialInventoryRoot");
            go.transform.SetParent(transform, false);
            materialInventoryRoot = go.transform;
        }
    }

    /// <summary>
    /// 进入“制造面具/经营阶段”：按顺序生成一个新的 MaskObj。
    /// </summary>
    public void EnterMakeMaskPhase()
    {
        if (maskMakeManager == null)
        {
            Debug.LogError("[GameManager] MaskMakeManager 未初始化。", this);
            return;
        }

        var newMask = maskMakeManager.MakeNextMask();
        if (newMask == null) return;

        // 当前面具：用于本回合材料附加与本场战斗，同时也会参与“面具库注入”（但战后才正式入库）
        if (newMask.transform.parent != transform && newMask.transform.parent != maskMakeManager.transform)
        {
            newMask.transform.SetParent(maskMakeManager.transform, false);
        }

        if (autoBindInventoryOnMake)
        {
            AutoBindInventoryToCurrentMask();
        }

        if (makeMuskUI != null)
        {
            makeMuskUI.gameObject.SetActive(true);
            makeMuskUI.RefreshInventoryUI();
        }

        // 进入制造阶段：关闭战斗 UI
        if (battleUI != null) battleUI.gameObject.SetActive(false);
    }

    /// <summary>
    /// 进入战斗阶段：使用 FightManager 开始战斗。
    /// </summary>
    public void StartBattlePhase()
    {
        // 兼容旧入口：不再在这里做“严格顺序等待”，只作为手动开战入口。
        StartBattlePhaseAsync(_destroyToken).Forget();
    }

    private async UniTask StartBattlePhaseAsync(CancellationToken ct)
    {
        if (fightManager == null)
        {
            Debug.LogError("[GameManager] FightManager 未初始化。", this);
            return;
        }

        // 确保存在当前面具
        if (maskMakeManager != null && maskMakeManager.CurrentMask == null)
        {
            EnterMakeMaskPhase();
        }

        // 进入战斗阶段：关闭制造 UI，打开战斗 UI
        if (makeMuskUI != null) makeMuskUI.gameObject.SetActive(false);
        if (battleUI != null) battleUI.gameObject.SetActive(true);

        // 组装“面具库注入器”：面具库 + 当前面具（当前面具不一定已入库，但本场战斗需要生效）
        var injectors = new System.Collections.Generic.List<IMaskBattleInjector>();
        for (int i = 0; i < _maskLibrary.Count; i++)
        {
            if (_maskLibrary[i] != null) injectors.Add(_maskLibrary[i]);
        }
        if (maskMakeManager != null && maskMakeManager.CurrentMask != null)
        {
            injectors.Add(maskMakeManager.CurrentMask);
        }
        fightManager.SetMaskBattleInjector(new MaskLibraryInjector(injectors));

        _battleEndTcs = new UniTaskCompletionSource<bool>();

        fightManager.StartFight();
        Debug.Log("[GameManager] 进入战斗阶段（纯数值战斗）。");

        var ctx = fightManager.Context;
        if (ctx == null)
        {
            Debug.LogError("[GameManager] FightContext 为空，无法等待战斗结束。", this);
            _battleEndTcs.TrySetResult(false);
        }
        else
        {
            System.Action<FightContext> onEnd = null;
            onEnd = _ =>
            {
                ctx.OnVictory -= onEnd;
                ctx.OnDefeat -= onEnd;
                _battleEndTcs.TrySetResult(true);
            };
            ctx.OnVictory += onEnd;
            ctx.OnDefeat += onEnd;
        }

        await _battleEndTcs.Task.AttachExternalCancellation(ct);

        // 战后结算（严格在战斗结束后执行）
        PostBattleSettlement(ctx);

        // 战斗结束：关闭战斗 UI（制造阶段会在下一轮再打开）
        if (battleUI != null) battleUI.gameObject.SetActive(false);
    }

    private void PostBattleSettlement(FightContext ctx)
    {
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 战后结算开始。round={_roundIndex}");
        // 当前面具入库
        var cur = maskMakeManager != null ? maskMakeManager.DetachCurrentMaskForLibrary() : null;
        if (cur != null && !_maskLibrary.Contains(cur))
        {
            _maskLibrary.Add(cur);
            if (maskLibraryRoot != null) cur.transform.SetParent(maskLibraryRoot, false);
        }

        // 持久增值结算
        CollectAndApplyPersistentGrowth(ctx);
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 持久增值结算完成。round={_roundIndex}");

        // 掉落结算（入材料库存）
        RunDrops();
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 掉落结算完成。round={_roundIndex}");
    }

    // ---- UI helpers ----
    public IReadOnlyList<MaterialObj> GetMaterialInventoryItems()
    {
        return materialInventory != null ? materialInventory.Items : null;
    }

    public MaskObj GetCurrentMask()
    {
        return maskMakeManager != null ? maskMakeManager.CurrentMask : null;
    }

    /// <summary>
    /// UI 兜底：如果策划直接把 MakeMuskUI 打开但没有走 EnterMakeMaskPhase()，
    /// 则此方法会保证存在一个 CurrentMask（不会重复开启 UI，也不会触发流程等待）。
    /// </summary>
    public MaskObj EnsureCurrentMaskForMakeUI()
    {
        if (maskMakeManager == null)
        {
            Debug.LogError("[GameManager] MaskMakeManager 未初始化，无法 EnsureCurrentMaskForMakeUI。", this);
            return null;
        }

        if (maskMakeManager.CurrentMask != null) return maskMakeManager.CurrentMask;

        var m = maskMakeManager.MakeNextMask();
        if (m == null) return null;

        if (m.transform.parent != transform && m.transform.parent != maskMakeManager.transform)
        {
            m.transform.SetParent(maskMakeManager.transform, false);
        }

        if (enablePhaseDebugLogs) Debug.Log("[GameManager] EnsureCurrentMaskForMakeUI：已自动创建 CurrentMask。");
        return m;
    }

    public void RemoveMaterialFromInventory(MaterialObj mat)
    {
        if (mat == null) return;
        materialInventory?.Remove(mat);
    }

    private void CollectAndApplyPersistentGrowth(FightContext ctx)
    {
        if (Player.I == null) return;
        if (ctx == null) return;

        var delta = new PlayerGrowthDelta();

        // 面具库顺序 → 材料顺序 → 逻辑树遍历顺序（运行时只使用 logicTreeRoots）
        for (int mi = 0; mi < _maskLibrary.Count; mi++)
        {
            var mask = _maskLibrary[mi];
            if (mask == null) continue;

            var mats = mask.Materials;
            for (int i = 0; i < mats.Count; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;

                // 树状逻辑优先：PersistentGrowth 阶段（建议配 Gate_Phase(PersistentGrowth)）
                if (mat.LogicTreeRoots != null && mat.LogicTreeRoots.Count > 0)
                {
                    var tctx = new MaterialVommandeTreeContext(
                        MaterialTraversePhase.PersistentGrowth,
                        mask: null,
                        maskMaterials: null,
                        onMaterialBound: null,
                        fight: null,
                        side: FightSide.None,
                        defenderSide: FightSide.None,
                        actionNumber: ctx.BattleActionCount,
                        attackerAttackNumber: 0,
                        attackInfo: default,
                        damage: 0f,
                        player: null,
                        growthDelta: delta
                    );
                    TraverseMaterialGrowthTree(mat.LogicTreeRoots, in tctx, delta, ctx);
                    continue;
                }

                // 不再兼容链式：没有树就跳过并输出提示
                if (ctx.DebugVerbose)
                {
                    ctx.DebugLogger?.Invoke($"[GameManager] PersistentGrowth Skip：Material={mat.name} 未配置 logicTreeRoots。");
                }
            }
        }

        if (JamDefaultSettings.PersistentGrowthCalculator != null)
        {
            JamDefaultSettings.PersistentGrowthCalculator.Apply(Player.I, delta, ctx);
        }
        else
        {
            // 兜底：直接应用（不建议为 null）
            Player.I.ApplyGrowth(delta);
        }
    }

    private void TraverseMaterialGrowthTree(IReadOnlyList<MaterialLogicNode> nodes, in MaterialVommandeTreeContext tctx, PlayerGrowthDelta delta, FightContext fight)
    {
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var c = n.Component;

            if (c is IMaterialTraversalGate gate && gate.ShouldBreak(in tctx))
            {
                // 仅跳过该分支
                continue;
            }

            if (c is IPersistentGrowthProvider p)
            {
                p.OnCollectPersistentGrowth(Player.I, delta, fight);
            }

            // 只有逻辑节点（gate/空节点）允许子节点
            if ((c == null || c is IMaterialTraversalGate) && n.Children != null && n.Children.Count > 0)
            {
                TraverseMaterialGrowthTree(n.Children, in tctx, delta, fight);
            }
        }
    }

    private void RunDrops()
    {
        if (Player.I == null) return;

        if (dropPool == null || dropMethod == null) return;

        int luck = Player.I.ActualStats.Luck;
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] RollDrops luck={luck} dropCount={dropCount} round={_roundIndex}");
        var drops = dropMethod.Roll(dropPool, luck, dropCount);
        if (drops == null) return;

        for (int i = 0; i < drops.Count; i++)
        {
            var e = drops[i];
            if (e == null || e.MaterialPrefab == null) continue;

            int count = Mathf.Max(0, e.Count);
            for (int k = 0; k < count; k++)
            {
                var inst = Instantiate(e.MaterialPrefab, materialInventoryRoot != null ? materialInventoryRoot : transform, false);
                inst.name = $"{e.MaterialPrefab.name}_Inv";
                inst.ResetInventoryShelfLife();
                materialInventory.Add(inst);
            }
        }
    }

    private void AutoBindInventoryToCurrentMask()
    {
        if (maskMakeManager == null) return;
        var mask = maskMakeManager.CurrentMask;
        if (mask == null) return;
        if (materialInventory == null) return;

        var items = materialInventory.Items;
        if (items == null || items.Count == 0) return;

        // 按加入库存顺序尝试绑定。材料实例直接绑定到面具：绑定成功就从库存移除。
        // 注意：移除会改变 Items，故这里先拷贝一份快照。
        var snapshot = new List<MaterialObj>(items);
        for (int i = 0; i < snapshot.Count; i++)
        {
            var mat = snapshot[i];
            if (mat == null) continue;

            var result = mask.BindMaterial(mat);
            if (result.Success)
            {
                materialInventory.Remove(mat);
            }
            else
            {
                // 不消耗库存；继续尝试其它材料（可能更便宜）
                if (materialInventoryRoot != null && mat.transform.parent != materialInventoryRoot)
                {
                    mat.transform.SetParent(materialInventoryRoot, false);
                }
            }
        }
    }

    /// <summary>
    /// Jam 临时补全：在策划还没配置完 Inspector/Prefab 的情况下，让流程先跑通。
    /// 手动 new 出来（非 Mono），符合“临时测试类”需求。
    /// </summary>
    private sealed class JamTempFixer
    {
        public void Apply(GameManager gm)
        {
            if (gm == null) return;

            // Jam：玩家一律使用 JamDefaultSettings（不再依赖 PlayerConfigSO）
            if (Player.I == null)
            {
                Player.CreateSingleton(JamDefaultSettings.DefaultPlayerBaseStats);
                if (gm.enablePhaseDebugLogs) Debug.Log("[JamTempFixer] Player 已用 JamDefaultSettings 初始化。");
            }

            // 面具底板：确保存在（未配置时自动创建临时底板）
            if (gm.maskMakeManager != null)
            {
                gm.maskMakeManager.EnsureBaseMaskPrefabForTest(10);
            }

            // 怪物生成：如果当前链条生成不出怪物，自动挂一个测试逻辑并重建链
            if (gm.monsterSpawnSystem != null)
            {
                var testCfg = gm.monsterSpawnSystem.Spawn(0, null);
                if (testCfg == null)
                {
                    Debug.LogWarning("[JamTempFixer] 当前怪物生成链无法生成怪物：已自动挂 JamTestMonsterSpawnLogic。");
                    if (gm.monsterSpawnSystem.GetComponent<JamTestMonsterSpawnLogic>() == null)
                    {
                        gm.monsterSpawnSystem.gameObject.AddComponent<JamTestMonsterSpawnLogic>();
                    }
                    gm.monsterSpawnSystem.Initialize();
                }
            }

            // 制造 UI：如果没配 UI 且没开 autoCompleteMakePhase，会卡住等待
            if (gm.makeMuskUI == null && !gm.autoCompleteMakePhase)
            {
                gm.autoCompleteMakePhase = true;
                Debug.LogWarning("[JamTempFixer] makeMuskUI 未配置：已强制 autoCompleteMakePhase=true，避免流程卡住。");
            }

            // 战斗 UI：尽量自动补一个引用（不强制；没有也不影响流程，只是少展示）
            if (gm.battleUI == null)
            {
                gm.battleUI = Object.FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
                if (gm.battleUI == null)
                {
                    if (gm.enablePhaseDebugLogs)
                        Debug.LogWarning("[JamTempFixer] battleUI 未配置/未找到：战斗阶段将只输出 Log（不显示血条/速度条/飘字）。");
                }
                else
                {
                    if (gm.enablePhaseDebugLogs)
                        Debug.Log("[JamTempFixer] 已自动找到 BattleUI 并注入到 GameManager。");
                }
            }
        }
    }
}




