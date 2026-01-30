## 目的（给策划 / 给 AI 的交接）

这份文档的目标是：**策划在主程不在场时，也能把需求交给 AI，让 AI 在有限资源下安全地修改代码**。

项目是 GameJam 版本，设计偏“简单粗暴”，性能不是优先级，但有一些硬约束必须遵守（尤其是初始化与 `.csproj`）。

---

## 硬约束（非常重要）

- **所有管理类只能在 `GameManager` 中创建/初始化**  
  例如：`FightManager / MaskMakeManager / MonsterSpawnSystem` 都由 `GameManager.Awake()` 负责创建 GameObject 并调用 `Initialize()`。

- **本项目的 `Assembly-CSharp.csproj` 是显式文件列表**  
  新增 `.cs` 文件后，**必须把它加入 `Assembly-CSharp.csproj`** 的 `<Compile Include="...">`，否则 Unity/IDE 会报“找不到类型/命名空间”。  
  文件：`Assembly-CSharp.csproj`

- **UniTask 引用可能会被 Unity 生成器弄丢**  
  如果出现 `Cysharp.Threading.Tasks` 找不到，检查 `Assembly-CSharp.csproj` 是否包含：
  - `Library\ScriptAssemblies\UniTask.dll`

---

## 游戏主循环（代码入口）

文件：`Assets/Script/GameManager.cs`

关键流程（按顺序）：
- `RunMainLoopAsync()` 循环跑回合
- 每回合：
  - `EnterMakeMaskPhase()`：进入制造面具 UI，等待玩家点 Next
  - `materialInventory.TickEndOfMakePhase(...)`：制造阶段结束，库存保质期结算
  - `StartBattlePhaseAsync(...)`：进入战斗（目前可纯数值 + Log）
  - `PostBattleSettlement(...)`：战后结算（面具入库、持久成长、掉落入库存）

策划最常改的配置字段（都在 `GameManager` Inspector 上）：
- **怪物生成系统**：由 `MonsterSpawnSystem` 管理（见下）
- **掉落**：`dropPool / dropMethod / dropCount`（中文 Topic 已标注）
- **调试**：`autoCompleteMakePhase`（跳过制作等待）、`autoRunLoop`

常用 API（UI/脚本可能会调用）：
- `GameManager.I.NotifyMakeMaskFinished()`：制造面具阶段完成（Next 按钮触发）
- `GameManager.I.GetMaterialInventoryItems()`：读取库存列表（用于 UI）
- `GameManager.I.RemoveMaterialFromInventory(MaterialObj mat)`：绑定成功后从库存移除
- `GameManager.I.GetCurrentMask()`：获取当前正在制作的面具

---

## 战斗系统（纯数值 + Log）

文件：`Assets/Script/Gameplay/Fight/FightManager.cs`

特点：
- **速度制**：`ArenaSpeedThreshold` 达到阈值就攻击；攻击之间有最小间隔 `GameSetting.AttackAnimIntervalSeconds`
- **AttackInfo** 每次攻击创建，走处理链（材料可以修改 AttackInfo）
- **Log 输出**：即使战斗 UI 未完成，也会通过 `Debug.Log` 输出 BattleStart/每次攻击/BattleEnd

战斗上下文：`Assets/Script/Gameplay/Fight/FightContext.cs`
- 提供回调：`OnBattleStart/OnVictory/OnDefeat` 等
- 提供计数器：`CurrentActionNumber / CurrentAttackerAttackNumber`（给“第X攻击/每X回合”类词条使用）

数值计算接口：
- `IAttackInfoCalculator`：计算最终伤害（可替换实现）

---

## 怪物生成（策划常改）

系统：
- `Assets/Script/Gameplay/Spawn/MonsterSpawnSystem.cs`
- `Assets/Script/Gameplay/Spawn/IMonsterSpawnLogic.cs`
- `Assets/Script/Gameplay/Spawn/Logics/SequentialEnemySpawnLogic.cs`

机制：
- 生成系统内部是一条 **Spawn Logic Chain**：按组件顺序尝试 `TrySpawn(...)`
- 只要某个 logic 返回了 `CharacterConfig`，就用它作为本回合怪物
- 返回 null 则继续尝试下一个 logic

策划改法（最简单）：
- 在场景/Prefab 上找到 `MonsterSpawnSystem` 对象
- 在同一个 GameObject 上找到/添加 `SequentialEnemySpawnLogic`
- 修改它内部的怪物列表（顺序就是回合顺序）

---

## 掉落系统（策划常改）

文件：
- `Assets/Script/Gameplay/Drop/MaterialPool.cs`（SO：按品质分组）
- `Assets/Script/Gameplay/Drop/IMaterialDropMethod.cs`
- `Assets/Script/Gameplay/Drop/SimpleLuckMaterialDropMethod.cs`（基于 Luck 的简单实现）
- `Assets/Script/Gameplay/Drop/MaterialQuality.cs`（0-4，白绿紫金红）

配置位置：
- 在 `GameManager` Inspector 上设置：
  - `dropPool`：材料池 SO
  - `dropMethod`：掉落算法（SO/实现）
  - `dropCount`：每次战斗掉落数量

Luck：
- 来自 `Player.I.ActualStats.Luck`

---

## 玩家三层数值（策划常改的方向）

文件：
- `Assets/Script/Gameplay/Player/Player.cs`
- `Assets/Script/Gameplay/Player/PlayerConfigSO.cs`（BaseStats 配置）
- `Assets/Script/Gameplay/Player/PlayerStats.cs`
- `Assets/Script/Gameplay/Player/PlayerGrowthDelta.cs`

三层概念：
- **BaseStats**：配置（SO）
- **ActualStats**：持久（跨战斗）
- **BattleStats**：每场战斗临时（由 ActualStats + 材料效果叠加得到）

持久成长收集点：
- `GameManager.CollectAndApplyPersistentGrowth(FightContext ctx)`  
  面具库 → 材料顺序 → **材料组件顺序** 遍历，调用 `IPersistentGrowthProvider`

---

## 面具/材料系统（策划 & AI 修改重点）

### 面具
文件：`Assets/Script/Gameplay/Mask/MaskObj.cs`
- `BindMaterial(MaterialObj material)`：把库存里的 MaterialObj 绑定到面具（re-parent，不实例化）
- `Materials`：材料链表顺序非常重要（影响战斗注入顺序）

### 材料对象（Prefab）
文件：`Assets/Script/Gameplay/Mask/MaterialObj.cs`
- 基础字段：`Id / DisplayName / BaseSprite / ManaCost / Quality / ShelfLifeTurns`
- **关键：`orderedComponents`**  
  材料组件的执行顺序由该列表决定（用于 Gate 跳出与所有效果执行顺序）。

### 材料组件执行与“跳出 Gate”
相关文件：
- `Assets/Script/Gameplay/Mask/IMaterialTraversalGate.cs`
- `Assets/Script/Gameplay/Mask/MaterialTraverseContext.cs`
- `Assets/Script/Gameplay/Mask/Gates/*.cs`

规则：
- 进入某个阶段时，会按 `orderedComponents` 从上到下遍历
- 如果遇到实现了 `IMaterialTraversalGate` 的组件，且 `ShouldBreak(...) == true`，则 **提前 break**（后续组件不执行）
- Gate 支持：每X回合/前X回合/第X攻击/前X攻击，并支持 `Invert` 取反

---

## 策划如何新增/编辑材料（工具）

材料编辑器：
- 菜单：`GGJ2026/材料编辑器`
- 代码：`Assets/Script/Editor/MaterialEditorWindow.cs`

默认目录：
- `Assets/Resources/Mat`

用法要点：
- 左侧选择/创建 `MaterialObj` prefab（Id 自动递增）
- 右侧编辑基本字段（名字、Sprite、品质、消耗、保质期）
- “Add Component” 会反射列出所有材料组件类型
- **添加组件后会自动写入 `MaterialObj.orderedComponents`，确保顺序可控**
- “保存”会按 `"{id}_{材质名}"` 命名（会尝试 MoveAsset 保留 GUID）

---

## 常见改动示例（给 AI 的任务模板）

### 示例 1：想让某个材料“前 3 次攻击生效”
做法：
- 在材料 prefab 上添加 `Gate_FirstXAttacks`
- `FirstX = 3`
- 把 Gate 放在 `orderedComponents` 列表中要控制的效果组件之前

### 示例 2：想让某个材料“每 2 回合不生效”
做法：
- 添加 `Gate_EveryXTurns`
- `EveryX = 2`
- `Invert = true`

### 示例 3：策划要改第 5 回合怪物血量
做法：
- 在 `SequentialEnemySpawnLogic` 里找到第 5 个 `CharacterConfig`
- 修改其 HP/ATK/DEF/Crit/SpeedRate 等字段

---

## 给 AI 的注意事项（避免把项目改炸）

- 不要绕开 `GameManager` 去创建管理器单例
- 新增脚本后务必同步 `Assembly-CSharp.csproj`
- 修改材料逻辑时尽量遵循：
  - “阶段入口”只做注入/订阅，不要到处 new 单例
  - 材料效果尽量做成组件（MonoBehaviour），用接口驱动


