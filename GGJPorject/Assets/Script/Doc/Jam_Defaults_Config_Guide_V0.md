# Jam 默认配置修改指南（V0）

> 目标：**Game Jam 期间不创建/维护大量 ScriptableObject 配置资产**。  
> 所有“必须可调”的关键参数，统一集中在 **纯代码默认配置** 中，策划只需要改一个文件即可生效。

---

## 1) 默认配置文件路径（请策划主要改这里）

- **默认配置代码**：`Assets/Script/JamDefaults/JamDefaultSettings.cs`

该文件内的字段就是当前 Jam 的“官方默认值”，需要调参直接修改代码即可。

---

## 2) 当前已开放给策划调整的默认参数

文件：`Assets/Script/JamDefaults/JamDefaultSettings.cs`

- **玩家初始数值**：`DefaultPlayerBaseStats`
  - `MaxHP / Attack / Defense / CritChance / CritMultiplier / SpeedRate / Luck`
  - 生效位置：`GameManager.BootstrapPlayer()`（启动即创建 Player 单例）

- **材料资源文件夹**：`ResourcesMatFolder`
  - 默认值：`"Mat"`
  - 含义：会在运行时 `Resources.LoadAll<MaterialObj>(ResourcesMatFolder)` 扫描所有材料 prefab
  - 注意：材质 prefab 必须放在 `Assets/Resources/Mat/`（或你改成的目录）下

- **每场战斗掉落次数**：`DropCountPerBattle`
  - 含义：每次战斗结束后抽取多少次材料（不是品质概率）
  - 生效位置：`GameManager.EnsureDropConfigForJam()`

- **开局发放 Common 材料数量**：`InitialCommonMaterialCount`
  - 含义：开局自动给玩家多少个 Common 材料实例（方便第一回合制作面具）
  - 生效位置：`GameManager.SpawnInitialCommonMaterialsIfNeeded()`

---

## 3) 运行时会自动做的事（策划无需创建配置资产）

### 3.1 材料池（MaterialPool）

- Jam 版本 **不需要创建** `MaterialPool` 资产
- `GameManager` 启动时会自动扫描 `ResourcesMatFolder` 并按 `MaterialObj.Quality` 分组，生成运行时材料池

### 3.2 掉落方法（DropMethod）

- Jam 版本 **不需要创建** 掉落方法资产
- `GameManager` 会在运行时 `CreateInstance<SimpleLuckMaterialDropMethod>()` 作为默认实现

> 备注：掉落方法内部的权重目前仍在 `SimpleLuckMaterialDropMethod` 脚本的私有序列化字段中（Jam 先跑通为主）。

---

## 4) 策划常见修改流程（推荐）

1. 打开 `Assets/Script/JamDefaults/JamDefaultSettings.cs`
2. 修改你需要的数值（例如玩家初始攻击、掉落次数）
3. 回到 Unity，等待编译完成
4. 运行 Play
5. 通过 Console 的 `[GameManager]` 日志确认参数已生效（例如掉落次数、玩家初始数值）

---

## 5) 仍然需要在场景里拖拽/配置的内容（不是“默认配置”）

这些属于“场景引用”，不会放到 JamDefaultSettings：

- `GameManager` 上的 UI 引用：`MakeMuskUI`、`BattleUI`（不配也能跑，但会影响展示）
- `MaskMakeManager.baseMaskPrefab`（建议正式配一个底板面具 Prefab；Jam 仍有临时兜底）
- `SequentialEnemySpawnLogic.enemyConfigs`（如果不配会走测试怪逻辑）

---

## 6) 约束与注意事项

- **Jam 期间优先改默认值文件**，不要创建大量 SO 资产，减少资源管理成本与丢引用风险
- 正式版再把这些默认值迁回 SO/关卡配置/策划表


