using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    private AudioManager audioManager => AudioManager.I;

    [SerializeField] private CanvasGroup bookCanvasGroup;
    [Header("Fight (Runtime Created)")]
    [SerializeField] private FightManager fightManager;

    [Header("Spawn (Runtime Created)")]
    [SerializeField] private MonsterSpawnSystem monsterSpawnSystem;

    [Header("Mask (Runtime Created)")]
    [SerializeField] private MaskMakeManager maskMakeManager;

    [Header("UI")]
    [SerializeField] private MakeMuskUI makeMuskUI;
    [SerializeField] public BattleUI battleUI;
    [Tooltip("黑屏 CanvasGroup（用于场景切换时的淡入淡出）。")]
    [SerializeField] private CanvasGroup blackScreen;
    [Tooltip("费用不足警告的 CanvasGroup（用于 fadeIn/fadeOut 动画）。")]
    [SerializeField] private CanvasGroup costWarningCanvasGroup;

    [Header("Mask Library (Runtime)")]
    [SerializeField] private Transform maskLibraryRoot;
    private readonly System.Collections.Generic.List<MaskObj> _maskLibrary = new();

    [Header("持久成长（未应用）")]
    [Tooltip("跨回合累积的成长值（从上次结算后开始累积，直到下次战斗结束应用）。")]
    private PlayerGrowthDelta _pendingGrowthDelta;

    /// <summary>
    /// 获取未应用的提升值（跨回合累积，从上次结算后开始累积，直到下次战斗结束应用）。
    /// </summary>
    public PlayerGrowthDelta PendingGrowthDelta => _pendingGrowthDelta;

    [Header("材料库存")]
    [SerializeField] private MaterialInventory materialInventory = new();

    // [新增] 统计材料使用次数的字典 <材料名, 次数>
    private Dictionary<string, int> _materialUsageHistory = new Dictionary<string, int>();

   [SerializeField] private Transform materialInventoryRoot;

    [Header("制造阶段（可选自动行为）")]
    [Tooltip("Jam 方便测试：进入制造回合时自动把库存材料尽量绑定到当前面具（按保质期优先）。")]
    [SerializeField] private bool autoBindInventoryOnMake = false;

    [Header("掉落配置（Jam 默认：纯代码，无 SO）")]
    [Tooltip("运行时生成的材料池（由 Resources/Mat 扫描得到）。Jam 默认不需要手动配置 MaterialPool SO。")]
    [SerializeField] private MaterialPool dropPool;
    [Tooltip("运行时创建的掉落方法（SO 实例，仅用于运行时；Jam 默认不需要创建 DropMethod 资产）。")]
    [SerializeField] public SimpleLuckMaterialDropMethod dropMethod;
    [SerializeField, Min(0)] public int dropCount = JamDefaultSettings.DropCountPerBattle;

    [Header("材料池/初始材料（Jam 默认：纯代码）")]
    [SerializeField] private string resourcesMatFolder = JamDefaultSettings.ResourcesMatFolder;
    [SerializeField, Min(0)] private int initialCommonMaterialCount = JamDefaultSettings.InitialCommonMaterialCount;
    private bool _initialMaterialsSpawned;

    [Header("面具 Sprite 配置")]
    [Tooltip("默认面具 Sprite（如果池为空或随机失败时使用）。")]
    [SerializeField] private Sprite defaultMaskSprite;
    [Tooltip("面具 Sprite 池（Compose 后随机选择）。")]
    [SerializeField] private List<Sprite> maskSpritePool = new();

    [Header("Flow")]
    [SerializeField] private bool autoRunLoop = false;

    [Header("流程控制（UniTask）")]
    [Tooltip("进入制造阶段后，是否自动视为玩家已完成制造（仅用于快速测试）。")]
    [SerializeField] private bool autoCompleteMakePhase = false;

    [Header("Jam 自动化/调试")]
    [SerializeField] private bool enableJamAutoFixes = true;
    [SerializeField] private bool enablePhaseDebugLogs = true;
    private int _roundIndex;
    // [新增] 引用 EndPass
    [Header("End Game")]
    [SerializeField] private EndPass endPass;  
    private UniTaskCompletionSource<bool> _makePhaseTcs;
    private UniTaskCompletionSource<bool> _battleEndTcs;
    private CancellationToken _destroyToken;
    private bool _manualAdvanceInProgress;
    private Tween _costWarningTween; // 费用不足警告动画
    private readonly System.Threading.SemaphoreSlim _blackScreenTransitionGate = new(1, 1);
    private AudioKey? _currentMainBgm;

    private void OnEnable()
    {
        // Singleton bootstrap (Jam 简化：重复实例直接销毁)
        if (I != null && I != this)
        {
            Destroy(I.gameObject);
        }
        I = this;
        // 所有单例/管理类必须在此初始化（强约束）
        BootstrapAudio();
        BootstrapFight();
        BootstrapSpawn();
        BootstrapMask();
        BootstrapPlayer();
        BootstrapRuntimeRoots();
        _pendingGrowthDelta= new PlayerGrowthDelta();
        _destroyToken = this.GetCancellationTokenOnDestroy();

        if (enablePhaseDebugLogs)
        {
            Debug.Log("[GameManager] Awake 完成：系统 Bootstrap 完毕。");
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
        SwitchMainBgm(AudioKey.Game_Shop_Music);
        bookCanvasGroup.DOFade(1f, 1f);
    }

    private void Start()
    {

      
    }

    private void SwitchMainBgm(AudioKey key)
    {
        if (audioManager == null) return;
        audioManager.Play(key);
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

        // 制造阶段结束时：收集当前面具材料的成长值（回合结束奖励）
        CollectMakePhaseGrowth();

        // 3) 战斗阶段：开始战斗并等待结束
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 进入战斗阶段。round={_roundIndex}");
        await StartBattlePhaseAsync(ct);

        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] RoundEnd round={_roundIndex}");
        _roundIndex++;
    }

    private void BootstrapAudio()
    {

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

    /// <summary>
    /// [新增] 记录材料使用（在 MakeMuskUI 合成成功时调用）
    /// </summary>
    public void RecordMaterialUsage(MaterialObj mat)
    {
        if (mat == null) return;
        string key = !string.IsNullOrEmpty(mat.DisplayName) ? mat.DisplayName : mat.name;
        
        if (_materialUsageHistory.ContainsKey(key))
            _materialUsageHistory[key]++;
        else
            _materialUsageHistory[key] = 1;
    }

    /// <summary>
    /// [新增] 获取使用次数最多的材料
    /// </summary>
    public (string name, int count) GetMostUsedMaterialInfo()
    {
        string bestName = "无";
        int bestCount = 0;
        foreach (var kv in _materialUsageHistory)
        {
            if (kv.Value > bestCount)
            {
                bestCount = kv.Value;
                bestName = kv.Key;
            }
        }
        return (bestName, bestCount);
    }

    /// <summary>
    /// [新增] 获取当前回合数
    /// </summary>
    public int CurrentRoundIndex => _roundIndex;

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
    /// 进入"制造面具/经营阶段"：按顺序生成一个新的 MaskObj。
    /// </summary>
    public void EnterMakeMaskPhase()
    {
        EnterMakeMaskPhaseAsync(_destroyToken).Forget();
    }

    private async UniTaskVoid EnterMakeMaskPhaseAsync(CancellationToken ct)
    {
        SwitchMainBgm(AudioKey.Game_Shop_Music);
        // 黑屏淡入淡出：从战斗到制造（内容切换在黑屏期间执行）
        await PlayBlackScreenTransition(_ =>
        {
            // 商店/制造阶段音乐：尽量在黑屏中切换

            if (maskMakeManager == null)
            {
                Debug.LogError("[GameManager] MaskMakeManager 未初始化。", this);
                return UniTask.CompletedTask;
            }

            var newMask = maskMakeManager.MakeNextMask();
            if (newMask == null) return UniTask.CompletedTask;

            // 当前面具：用于本回合材料附加与本场战斗，同时也会参与"面具库注入"（但战后才正式入库）
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
                SetUIActiveWithCanvasGroup(makeMuskUI.gameObject, true);
                makeMuskUI.RefreshInventoryUI();
            }

            // 进入制造阶段：关闭战斗 UI
            if (battleUI != null) {SetUIActiveWithCanvasGroup(battleUI.gameObject, false)
            ;
            };
            return UniTask.CompletedTask;
        }, ct);
    }

    /// <summary>
    /// 播放黑屏淡入淡出过渡动画。
    /// 流程：fadeIn 0.5s -> 执行内容切换回调 -> 等待 0.5s -> fadeOut 0.5s
    /// </summary>
    /// <param name="onContentSwitch">在 fadeIn 完成后、等待期间执行的内容切换回调</param>
    private async UniTask PlayBlackScreenTransition(System.Func<CancellationToken, UniTask> onContentSwitch, CancellationToken ct)
    {
        if (blackScreen == null) return;

        // 强约束：黑屏转场不允许并发/重入（否则会造成 Tween Kill、时序错乱、流程异常）
        await _blackScreenTransitionGate.WaitAsync(ct);
        try
        {
        // 确保黑屏 GameObject 激活
        if (!blackScreen.gameObject.activeSelf)
        {
            blackScreen.gameObject.SetActive(true);
        }

        // 初始化：alpha = 0，blocksRaycasts = true（防止点击穿透）
        blackScreen.alpha = 0f;
        blackScreen.blocksRaycasts = true;
        blackScreen.interactable = false;
        

        // FadeIn: 0 -> 1 (0.5s)
        var fadeInTcs = new UniTaskCompletionSource();
        var fadeInTween = blackScreen.DOFade(1f, 0.5f).SetUpdate(true);
        fadeInTween.OnComplete(() => fadeInTcs.TrySetResult());
        fadeInTween.OnKill(() =>
        {
            // 被外部 Kill 时尽量保证状态一致，避免 await 抛异常导致流程中断
            if (blackScreen != null) blackScreen.alpha = 1f;
            fadeInTcs.TrySetResult();
        });
        await fadeInTcs.Task.AttachExternalCancellation(ct);

    

        // 等待 0.5s（让内容切换完成）
        await UniTask.Delay(200, cancellationToken: ct);
        // 在黑屏完全显示后立即执行内容切换
        if (onContentSwitch != null) await onContentSwitch(ct);
        await UniTask.Delay(200, cancellationToken: ct);

        // FadeOut: 1 -> 0 (0.5s)
        var fadeOutTcs = new UniTaskCompletionSource();
        var fadeOutTween = blackScreen.DOFade(0f, 0.5f).SetUpdate(true);
        fadeOutTween.OnComplete(() => fadeOutTcs.TrySetResult());
        fadeOutTween.OnKill(() =>
        {
            if (blackScreen != null) blackScreen.alpha = 0f;
            fadeOutTcs.TrySetResult();
        });
        await fadeOutTcs.Task.AttachExternalCancellation(ct);

        // 淡出后禁用 blocksRaycasts
        blackScreen.blocksRaycasts = false;
        }
        finally
        {
            _blackScreenTransitionGate.Release();
        }
    }

    /// <summary>
    /// 游戏结束（方法后续补充）。
    /// </summary>
    private void GameOver()
    {
        Debug.Log("[GameManager] TriggerGameOver Called.");
        
        // 1. 停止主循环 (通过 Cancel Token)
        if (_destroyToken.CanBeCanceled)
        {
            // 注意：这里只是示例，通常建议用专门的 CancellationTokenSource 来控制逻辑循环
            // 如果 runMainLoopAsync 绑定的是 destroyToken，销毁物体会自动停。
            // 建议增加一个专门的 _gameLoopCts
        }
        // 简单暴力法：设置一个标志位让 RunMainLoopAsync 退出
        // (需修改 RunMainLoopAsync 增加 !isGameOver 判断)
        
        // 2. 停止战斗 (如果正在进行)
        if (fightManager != null) fightManager.StopFight();
        // 3. 播放 BGM (可选，比如悲伤的音乐)
        // SwitchMainBgm(AudioKey.Game_Over_Music); 

        // 4. 呼叫 EndPass
        if (endPass != null)
        {
            endPass.gameObject.SetActive(true);
            endPass.StartEndingSequence();
        }
        else
        {
            Debug.LogError("EndPass reference is missing in GameManager!");
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

        // 注意：不再在这里清空 _pendingGrowthDelta，因为它是跨回合的
        // 会在 CollectAndApplyPersistentGrowth 中应用后立即创建新的

        // 确保存在当前面具（在进入战斗前先创建，但不等待黑屏过渡）
        if (maskMakeManager != null && maskMakeManager.CurrentMask == null)
        {
            if (maskMakeManager != null)
            {
                var newMask = maskMakeManager.MakeNextMask();
                if (newMask != null)
                {
                    if (newMask.transform.parent != transform && newMask.transform.parent != maskMakeManager.transform)
                    {
                        newMask.transform.SetParent(maskMakeManager.transform, false);
                    }
                    if (autoBindInventoryOnMake)
                    {
                        AutoBindInventoryToCurrentMask();
                    }
                }
            }
        }
        SwitchMainBgm(AudioKey.Game_Fight_Music);

        // 黑屏淡入淡出：从制造到战斗（内容切换在黑屏期间执行）
        await PlayBlackScreenTransition(_ =>
        {
            // 战斗音乐：尽量在黑屏中切换

            // 进入战斗阶段：关闭制造 UI，打开战斗 UI（CanvasGroup 会同时控制透明度与射线）
            if (makeMuskUI != null) {
                SetUIActiveWithCanvasGroup(makeMuskUI.gameObject, false);
            }
            if (battleUI != null) 
            {
                SetUIActiveWithCanvasGroup(battleUI.gameObject, true);
            }

            // 注入面具库 + 当前面具（不依赖 battleUI 是否存在）
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
            fightManager.StartFight();

            return UniTask.CompletedTask;
        }, ct);
        _battleEndTcs = new UniTaskCompletionSource<bool>();

        var ctx = fightManager.Context;
        fightManager.StartFightUpdate();

        if (ctx == null)
        {
            Debug.LogError("[GameManager] FightContext 为空，无法等待战斗结束。", this);
            _battleEndTcs.TrySetResult(false);
        }
        else
        {
            System.Action<FightContext> onVictory = null;
            System.Action<FightContext> onDefeat = null;
            
            onVictory = async (fightCtx) =>
            {
                ctx.OnVictory -= onVictory;
                ctx.OnDefeat -= onDefeat;
                
                // 战斗胜利：停止战斗计算
                if (fightManager != null)
                {
                    fightManager.StopFight();
                }
                
                await UniTask.Delay(2000, cancellationToken: ct); // 等待 2 秒
                
                _battleEndTcs.TrySetResult(true);
            };
            
            onDefeat = _ =>
            {
                ctx.OnVictory -= onVictory;
                ctx.OnDefeat -= onDefeat;

                _battleEndTcs.TrySetResult(true);
                GameOver();

            };
            
            ctx.OnVictory += onVictory;
            ctx.OnDefeat += onDefeat;
        }

        await _battleEndTcs.Task.AttachExternalCancellation(ct);

        // 战后结算（严格在战斗结束后执行，等待 UI 掉落动画完成）
        await PostBattleSettlementAsync(ctx, ct);

        // 战斗结束：关闭战斗 UI（制造阶段会在下一轮再打开）
        // 重要：不要在这里关 battleUI；否则会出现"还没黑屏战斗界面就关闭"的观感问题。
        // battleUI 的关闭应统一放到 EnterMakeMaskPhaseAsync 的黑屏内容切换中执行。
    }

    private static void SetUIActiveWithCanvasGroup(GameObject go, bool active)
    {
        if (go == null) return;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            if (active)
            {
                // 先激活，再恢复可见/可交互（避免 OnEnable 期间状态不一致）
                go.SetActive(true);
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            else
            {
                // 先禁止交互/射线，再隐藏
                cg.interactable = false;
                cg.blocksRaycasts = false;
                cg.alpha = 0f;
                go.SetActive(false);
            }
            return;
        }

        // 没有 CanvasGroup 就保持旧逻辑
        go.SetActive(active);
    }

    private async UniTask PostBattleSettlementAsync(FightContext ctx, CancellationToken ct)
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

        // 等待 UI 掉落动画完成（掉落物会在动画完成后自动加入库存）
        if (battleUI != null)
        {
            if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 等待掉落动画完成... round={_roundIndex}");
            await battleUI.WaitForDropAnimationAsync().AttachExternalCancellation(ct);
            if (enablePhaseDebugLogs) Debug.Log($"[GameManager] 掉落动画完成。round={_roundIndex}");
        }

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
    /// 获取面具库（只读列表）。
    /// </summary>
    public IReadOnlyList<MaskObj> GetMaskLibrary()
    {
        return _maskLibrary;
    }

    /// <summary>
    /// 为面具分配随机 Sprite（Compose 后调用）。
    /// </summary>
    public void AssignRandomMaskSprite(MaskObj mask)
    {
        if (mask == null) return;

        Sprite sprite = null;

        // 从池中随机选择
        if (maskSpritePool != null && maskSpritePool.Count > 0)
        {
            var validSprites = new List<Sprite>();
            foreach (var s in maskSpritePool)
            {
                if (s != null) validSprites.Add(s);
            }

            if (validSprites.Count > 0)
            {
                sprite = validSprites[Random.Range(0, validSprites.Count)];
            }
        }

        // 如果池为空或随机失败，使用默认 sprite
        if (sprite == null)
        {
            sprite = defaultMaskSprite;
        }

        mask.DisplaySprite = sprite;

        if (enablePhaseDebugLogs)
        {
            Debug.Log($"[GameManager] 已为面具分配 Sprite：{mask.name} -> {(sprite != null ? sprite.name : "null")}");
        }
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

    /// <summary>
    /// Jam 调试工具：手动生成一个材料实例并加入库存。
    /// </summary>
    public MaterialObj DebugSpawnMaterialToInventory(MaterialObj prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[GameManager] DebugSpawnMaterialToInventory：prefab 为空。", this);
            return null;
        }
        if (materialInventory == null)
        {
            Debug.LogError("[GameManager] DebugSpawnMaterialToInventory：materialInventory 为空。", this);
            return null;
        }

        var parent = materialInventoryRoot != null ? materialInventoryRoot : transform;
        var inst = Instantiate(prefab, parent, false);
        inst.name = $"{prefab.name}_Inv_Debug";
        inst.ResetInventoryShelfLife();
        materialInventory.Add(inst);

        if (makeMuskUI != null && makeMuskUI.gameObject.activeInHierarchy)
        {
            makeMuskUI.RefreshInventoryUI();
        }

        Debug.Log($"[GameManager] DebugSpawnMaterialToInventory：已入库 {inst.name}", inst);
        return inst;
    }

    /// <summary>
    /// 收集制造阶段的成长值（回合结束奖励）。
    /// 遍历当前面具的材料，收集 IPersistentGrowthProvider 的成长值到 _pendingGrowthDelta。
    /// </summary>
    private void CollectMakePhaseGrowth()
    {
        if (Player.I == null) return;

        // 确保 _pendingGrowthDelta 存在（如果不存在则创建）
        if (_pendingGrowthDelta == null)
        {
            _pendingGrowthDelta = new PlayerGrowthDelta();
        }

        // 获取当前面具
        var currentMask = maskMakeManager != null ? maskMakeManager.CurrentMask : null;
        if (currentMask == null) return;

        var mats = currentMask.Materials;
        if (mats == null || mats.Count == 0) return;

        // 遍历当前面具的材料，收集成长值
        for (int i = 0; i < mats.Count; i++)
        {
            var mat = mats[i];
            if (mat == null) continue;

            // 树状逻辑：PersistentGrowth 阶段
            if (mat.LogicTreeRoots != null && mat.LogicTreeRoots.Count > 0)
            {
                var tctx = new MaterialVommandeTreeContext(
                    MaterialTraversePhase.PersistentGrowth,
                    mask: currentMask,
                    maskMaterials: mats,
                    onMaterialBound: null,
                    fight: null,
                    side: FightSide.None,
                    defenderSide: FightSide.None,
                    actionNumber: 0,
                    attackerAttackNumber: 0,
                    attackInfo: default,
                    damage: 0f,
                    player: null,
                    growthDelta: _pendingGrowthDelta
                );
                TraverseMaterialGrowthTree(mat.LogicTreeRoots, in tctx, _pendingGrowthDelta, null);
            }
        }

        if (enablePhaseDebugLogs)
        {
            Debug.Log($"[GameManager] 制造阶段成长值收集完成。round={_roundIndex}");
        }
    }

    private void CollectAndApplyPersistentGrowth(FightContext ctx)
    {
        if (Player.I == null) return;
        if (ctx == null) return;

        // 使用现有的 _pendingGrowthDelta（如果存在），否则创建新的
        // 这样可以从上次结算后开始累积（包括制造阶段的成长值）
        var delta = _pendingGrowthDelta ?? new PlayerGrowthDelta();

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

        // 先存储未应用的提升值（用于 UI 显示"提升的数值（还未运用）"）
        // 注意：这里存储的是收集到的提升值，在应用前 UI 可以显示
        _pendingGrowthDelta = new PlayerGrowthDelta
        {
            AddMaxHP = delta.AddMaxHP,
            AddAttack = delta.AddAttack,
            AddDefense = delta.AddDefense,
            AddCritChance = delta.AddCritChance,
            AddCritMultiplier = delta.AddCritMultiplier,
            AddSpeedRate = delta.AddSpeedRate,
            AddLuck = delta.AddLuck
        };

        // 然后应用提升值
        if (JamDefaultSettings.PersistentGrowthCalculator != null)
        {
            JamDefaultSettings.PersistentGrowthCalculator.Apply(Player.I, delta, ctx);
        }
        else
        {
            // 兜底：直接应用（不建议为 null）
            Player.I.ApplyGrowth(delta);
        }

        if (enablePhaseDebugLogs)
        {
            Debug.Log($"[GameManager] 持久成长值已应用，已创建新的 _pendingGrowthDelta 用于下一轮。round={_roundIndex}");
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

    /// <summary>
    /// 获取本次战斗的掉落列表（不直接加入库存，用于 UI 表现）。
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<MaterialDropEntry> GetBattleDrops()
    {
        if (Player.I == null) return null;
        if (dropPool == null || dropMethod == null) return null;

        int luck = Player.I.ActualStats.Luck;
        if (enablePhaseDebugLogs) Debug.Log($"[GameManager] RollDrops luck={luck} dropCount={dropCount} round={_roundIndex}");
        return dropMethod.Roll(dropPool, luck, dropCount);
    }

    /// <summary>
    /// 将掉落物加入库存（在掉落动画完成后调用）。
    /// </summary>
    public void AddDropsToInventory(System.Collections.Generic.IReadOnlyList<MaterialDropEntry> drops)
    {
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

    /// <summary>
    /// 显示费用不足警告动画：fadeIn -> 持续1秒 -> fadeOut
    /// </summary>
    public void ShowCostWarning()
    {
        if (costWarningCanvasGroup == null) return;

        // 播放费用不足音效
        if (audioManager != null)
        {
            audioManager.PlaySfxOnce(AudioKey.FBX_Cost_Not_Enough);
        }

        // 停止当前动画
        _costWarningTween?.Kill();

        // 确保 CanvasGroup 激活
        if (!costWarningCanvasGroup.gameObject.activeSelf)
        {
            costWarningCanvasGroup.gameObject.SetActive(true);
        }

        // 初始化：alpha = 0
        costWarningCanvasGroup.alpha = 0f;

        // 创建动画序列：fadeIn (0.3s) -> 等待 (1s) -> fadeOut (0.3s)
        _costWarningTween = DOTween.Sequence()
            .SetUpdate(true) // 即使 TimeScale=0 也能播 UI
            .Append(costWarningCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad)) // fadeIn
            .AppendInterval(1f) // 持续1秒
            .Append(costWarningCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad)) // fadeOut
            .OnComplete(() => {
                _costWarningTween = null;
                // 动画完成后可以隐藏 GameObject（可选）
                // costWarningCanvasGroup.gameObject.SetActive(false);
            });
    }
}




