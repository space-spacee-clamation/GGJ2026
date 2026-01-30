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

    [Header("Player")]
    [SerializeField] private PlayerConfigSO playerConfig;

    [Header("Mask Library (Runtime)")]
    [SerializeField] private Transform maskLibraryRoot;
    private readonly System.Collections.Generic.List<MaskObj> _maskLibrary = new();

    [Header("材料库存")]
    [SerializeField] private MaterialInventory materialInventory = new();
    [SerializeField] private Transform materialInventoryRoot;

    [Header("制造阶段（可选自动行为）")]
    [Tooltip("Jam 方便测试：进入制造回合时自动把库存材料尽量绑定到当前面具（按保质期优先）。")]
    [SerializeField] private bool autoBindInventoryOnMake = false;

    [Header("掉落配置")]
    [SerializeField] private MaterialPool dropPool;
    [SerializeField] private SimpleLuckMaterialDropMethod dropMethod;
    [SerializeField, Min(0)] private int dropCount = 1;

    [Header("Flow")]
    [SerializeField] private bool autoRunLoop = false;

    [Header("流程控制（UniTask）")]
    [Tooltip("进入制造阶段后，是否自动视为玩家已完成制造（仅用于快速测试）。")]
    [SerializeField] private bool autoCompleteMakePhase = false;

    private UniTaskCompletionSource<bool> _makePhaseTcs;
    private UniTaskCompletionSource<bool> _battleEndTcs;
    private CancellationToken _destroyToken;

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
    }

    private void Start()
    {
        // 其它初始化放在 Start（强约束）
        if (audioManager != null)
        {
            audioManager.LoadAllEntriesFromResources();
        }

        if (autoRunLoop)
        {
            RunMainLoopAsync().Forget();
        }
    }

    /// <summary>
    /// UI/外部在玩家完成“制造面具回合”（材料附加结束）后调用，GameManager 会继续进入战斗。
    /// </summary>
    public void NotifyMakeMaskFinished()
    {
        _makePhaseTcs?.TrySetResult(true);
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
        // 1) 制造阶段：生成底板面具，等待玩家完成合成
        EnterMakeMaskPhase();

        _makePhaseTcs = new UniTaskCompletionSource<bool>();
        if (autoCompleteMakePhase) _makePhaseTcs.TrySetResult(true);

        await _makePhaseTcs.Task.AttachExternalCancellation(ct);

        // 2) 制造阶段结束：材料库存保质期结算
        materialInventory?.TickEndOfMakePhase(materialInventoryRoot);

        // 3) 战斗阶段：开始战斗并等待结束
        await StartBattlePhaseAsync(ct);
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
        if (playerConfig == null)
        {
            Debug.LogError("[GameManager] PlayerConfigSO 未配置，无法初始化 Player 单例。", this);
            return;
        }
        Player.CreateSingleton(playerConfig.BaseStats);
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
    }

    private void PostBattleSettlement(FightContext ctx)
    {
        // 当前面具入库
        var cur = maskMakeManager != null ? maskMakeManager.DetachCurrentMaskForLibrary() : null;
        if (cur != null && !_maskLibrary.Contains(cur))
        {
            _maskLibrary.Add(cur);
            if (maskLibraryRoot != null) cur.transform.SetParent(maskLibraryRoot, false);
        }

        // 持久增值结算
        CollectAndApplyPersistentGrowth(ctx);

        // 掉落结算（入材料库存）
        RunDrops();
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

        // 面具库顺序 → 材料顺序 → 组件顺序（MaterialObj 缓存顺序）
        for (int mi = 0; mi < _maskLibrary.Count; mi++)
        {
            var mask = _maskLibrary[mi];
            if (mask == null) continue;

            var mats = mask.Materials;
            for (int i = 0; i < mats.Count; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;

                var behaviours = mat.GetComponents<MonoBehaviour>();
                for (int bi = 0; bi < behaviours.Length; bi++)
                {
                    if (behaviours[bi] is IPersistentGrowthProvider p)
                    {
                        p.OnCollectPersistentGrowth(Player.I, delta, ctx);
                    }
                }
            }
        }

        Player.I.ApplyGrowth(delta);
    }

    private void RunDrops()
    {
        if (Player.I == null) return;

        if (dropPool == null || dropMethod == null) return;

        int luck = Player.I.ActualStats.Luck;
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
}




