## Jam 临时兜底/自动化清单（后续需要补全/替换的点）

这份文档记录了当前项目为了 **“流程先跑起来”** 添加的临时逻辑、自动化逻辑、容错逻辑。  
后续做正式版本时，应按本清单逐条替换为“策划可配置 + UI/流程完整”的实现，避免临时代码长期残留导致行为不一致。

---

## 1) GameManager：手动 Next 推进（主循环未启动也能跑）

文件：`Assets/Script/GameManager.cs`

- **位置**：`NotifyMakeMaskFinished()` + `AdvanceFromMakeUIAsync(CancellationToken ct)`
- **触发条件**：
  - `MakeMuskUI` 被直接打开，或 `autoRunLoop=false` 导致主循环没跑
  - `_makePhaseTcs == null` 时，点击 Next
- **当前行为**：
  - 不走 `RunMainLoopAsync/RunOneRoundAsync` 的等待链
  - 直接按顺序执行：`TickEndOfMakePhase → StartBattlePhaseAsync(等待战斗结束+战后结算) → round++ → EnterMakeMaskPhase`
  - 用 `_manualAdvanceInProgress` 防止重复点击并发
- **后续 TODO**：
  - 正式版应确保主循环始终由 `autoRunLoop` 或某个“开始游戏”按钮启动
  - UI 的 Next 仅负责 `TrySetResult`，不应兼顾整段流程
  - 可考虑把手动推进改成 Editor-only 或 Debug-only（例如 `#if UNITY_EDITOR`）

---

## 2) GameManager：MakeUI 直接打开时自动创建 CurrentMask

文件：`Assets/Script/GameManager.cs`

- **位置**：`EnsureCurrentMaskForMakeUI()`
- **触发条件**：
  - `maskMakeManager.CurrentMask == null`，但 UI 需要合成（Compose）
- **当前行为**：
  - 直接调用 `maskMakeManager.MakeNextMask()` 创建一个 `CurrentMask`
  - 不触发主流程等待，不重复打开 UI
- **后续 TODO**：
  - 正式版应强制 UI 只能由 `EnterMakeMaskPhase()` 打开（流程一致）
  - `EnsureCurrentMaskForMakeUI` 可保留为 Debug，但不应成为常态路径

---

## 3) MakeMuskUI：已选材料字典的“无 ChoicedMaterialPrefab 兜底”

文件：`Assets/Script/UI/MakeMask/MakeMuskUI.cs`

- **位置**：`OnClickMaterialButton` 对 `_choiced` 的写入逻辑
- **触发条件**：
  - `choicedMaterialPrefab == null` 或 `chosenSpawnArea == null`
- **当前行为**：
  - 即使不生成贴图 UI，也会把材料记录为“已选”（`_choiced[mat] = null`）
  - 保证 `Compose` 能执行绑定逻辑
- **后续 TODO**：
  - 正式版应保证 Prefab/区域都配置正确，且合成可视化一致
  - 当前 `_choiced` 是 `Dictionary`，合成顺序不稳定（TODO：改为显式 List 记录点击顺序）

---

## 4) GameManager：掉落配置自动化（dropMethod/dropCount）

文件：`Assets/Script/GameManager.cs`

- **位置**：`EnsureDropConfigForJam()`
- **触发条件**：
  - `dropMethod == null` 或 `dropCount <= 0`
- **当前行为**：
  - 运行时创建 `SimpleLuckMaterialDropMethod`（`ScriptableObject.CreateInstance`）
  - `dropCount<=0` 时自动设为 3
- **后续 TODO**：
  - 正式版应由策划配置（SO/Inspector），不要运行时偷偷创建
  - 可保留 Debug 默认值，但需明确 UI/日志提示 “使用了默认配置”

---

## 5) GameManager：材料池自动从 Resources/Mat 扫描生成

文件：`Assets/Script/GameManager.cs`

- **位置**：`BuildDropPoolFromResources()`（受 `autoBuildDropPoolFromResources` 控制）
- **触发条件**：
  - 启动时 `autoBuildDropPoolFromResources == true`
- **当前行为**：
  - `Resources.LoadAll<MaterialObj>("Mat")` 扫描所有材料 prefab
  - 按 `MaterialObj.Quality` 分组，运行时生成一个 `MaterialPool` 并覆盖 `dropPool`
- **后续 TODO**：
  - 正式版建议用一个明确的 `MaterialPool` SO 作为“官方配置”，Resources 扫描只做校验/工具
  - 或做成 Editor 工具：一键生成/更新 `MaterialPool` SO

---

## 6) GameManager：开局自动发 4 个品质0材料

文件：`Assets/Script/GameManager.cs`

- **位置**：`SpawnInitialCommonMaterialsIfNeeded()`（`initialCommonMaterialCount=4`）
- **触发条件**：
  - 启动时 `_initialMaterialsSpawned == false`
- **当前行为**：
  - 从 `dropPool.Common` 随机挑选 prefab，`Instantiate` 到 `materialInventoryRoot`，加入 `MaterialInventory`
- **后续 TODO**：
  - 正式版应由“开局配置/关卡配置/教程配置”决定发什么
  - 是否允许重复、是否与引导绑定，应交给策划数据

---

## 7) MonsterSpawnSystem：无配置时的测试怪物生成

文件：
- `Assets/Script/Debug/JamTestMonsterSpawnLogic.cs`
- `Assets/Script/GameManager.cs`（`JamTempFixer.Apply` 会自动挂载）

- **触发条件**：
  - 现有生成链 `Spawn(0, null)` 返回 null（生成不出怪物）
- **当前行为**：
  - 自动 `AddComponent<JamTestMonsterSpawnLogic>()`
  - 重新 `monsterSpawnSystem.Initialize()` 重建逻辑链
- **后续 TODO**：
  - 正式版应由 `SequentialEnemySpawnLogic` 或更复杂 SpawnLogicChain 配置
  - `JamTestMonsterSpawnLogic` 应仅用于 Debug/Editor，避免正式行为混入

---

## 8) MaskMakeManager：未配置 baseMaskPrefab 时自动生成临时底板

文件：`Assets/Script/Gameplay/Mask/MaskMakeManager.cs`

- **位置**：`EnsureBaseMaskPrefabForTest(int baseMana = 10)`
- **触发条件**：
  - `baseMaskPrefab == null` 时调用 `MakeNextMask()`
- **当前行为**：
  - 在运行时创建 `TempBaseMaskPrefab`（隐藏于层级）
  - 给一个默认 `BaseMana=10` 的 `MaskObj` 作为“模板”
- **后续 TODO**：
  - 正式版必须由策划配置一个真正的底板面具 Prefab
  - 运行时生成“模板 prefab”不直观，也可能导致资源/表现不一致

---

## 9) MaskObj：config 为空的默认容错

文件：`Assets/Script/Gameplay/Mask/MaskObj.cs`

- **位置**：`Awake()` / `RebuildFromConfig(StaticConfig cfg)`
- **触发条件**：
  - `config == null` 或传入 `cfg == null`
- **当前行为**：
  - 自动创建默认 config（`BaseMana=10` 等），避免 NRE
- **后续 TODO**：
  - 正式版建议明确：MaskPrefab 必须内置 StaticConfig
  - 该容错可保留，但应在日志中提示“使用了默认配置”

---

## 10) FightManager：强制 Log 战斗过程（无战斗 UI 也可看过程）

文件：`Assets/Script/Gameplay/Fight/FightManager.cs`

- **位置**：`forceLogsInJam = true`
- **当前行为**：
  - 输出 BattleStart、每次攻击伤害、BattleEnd
- **后续 TODO**：
  - 正式版应由战斗 UI 驱动表现；Log 只在 Debug 模式开启

---

## 11) GameManager：阶段 Debug 输出

文件：`Assets/Script/GameManager.cs`

- **位置**：`enablePhaseDebugLogs`
- **当前行为**：
  - 输出 RoundStart/End、制造等待、库存结算、战斗阶段、战后结算等关键节点
- **后续 TODO**：
  - 正式版可保留（对定位问题很有用），但建议接入统一 Logger/开关

---

## 12) “战斗开始/结束 Gate + 可跳出遍历”的抽象化（非临时，但需要规范使用）

文件：
- `Assets/Script/Gameplay/Mask/Gates/Gate_BattleStart.cs`
- `Assets/Script/Gameplay/Mask/Gates/Gate_BattleEnd.cs`
- `Assets/Script/Gameplay/Mask/MaterialRuntimeRunner.cs`

说明：
- 这不是“临时兜底”，是你当前决定的正式设计：用 Gate 分段，后续组件按顺序执行并支持 break。
- 但要注意：
  - 描述阶段不再 break（避免文案被截断）
  - “战斗结束时” Gate 目前也覆盖了持久成长收集（`GameManager.CollectAndApplyPersistentGrowth` 以 BattleEnd phase 遍历）

后续 TODO：
- 需要明确策划规范：某些效果必须放在某个 Gate 后面，否则不会触发
- 可在材质编辑器里加“阶段预览/触发预览”帮助策划


