# 战斗系统设计文档（V0）

> 适用范围：本项目的“速度条自动战斗”部分（玩家 vs 敌人，一对一）。  
> 依赖文档：`AI_Skill_BaseSpec_Requirements_V0.md`（第 5 章战斗章节为 BaseSpec）。  
> Jam 取舍：**先实现闭环与可调参**，不做复杂可扩展架构；数值计算先用接口占位，后续再补公式。

---

## 0. 战斗目标与边界（强约束）

- **一对一战斗**：玩家与敌人是一对一。  
- **多面具生效**：战斗中会存在“面具库（多面具）”，并且面具库内所有面具都会持续生效（类似肉鸽道具库）。  
- **速度条自动战斗**：不再使用“玩家回合/敌人回合”，改为速度条累积触发攻击（见第 3 章）。  
- **材料可注入**：材料效果通过回调/处理器链介入，战斗系统本体不写材料分支逻辑。  
- **Config 不可变**：战斗中任何属性变更只能改“运行时实例”，不得改 `ScriptableObject` 配置数据。  
- **数值公式占位**：V0 不规定最终伤害公式，先用一个接口对 `AttackInfo` 进行 `ref` 修改来替代。
 - **玩家单例**：全程只有一个玩家实例（单例），战斗使用“战斗数据/战斗数值”进行结算，不直接改玩家的“实际数值”。

---

## 1. 核心对象定义（命名建议与职责）

> 这里的命名尽量贴近现有目录：`Assets/Script/Gameplay/Fight/*`

### 1.1 FightManager（战斗驱动器）

- **职责**：驱动战斗状态机/回合循环；创建 `FightContext`；在正确时机触发回调；调用攻击处理链与数值结算接口。  
- **不做**：不包含任何材料效果的具体实现；不负责管理掉落（掉落属于战后流程）。  
- **单例入口**：`FightManager` 使用单例（例如 `FightManager.I`）供其它系统调用“开始战斗/停止战斗”。

### 1.2 FightContext（战斗上下文 / BattleContext）

- **职责**：承载战斗“共享数据”和“回调入口”，供材料效果组件注入与读取。
- **至少包含**：
  - Player 实例（运行时）
  - Enemy 实例（运行时）
  - MaskLibrary（运行时，面具列表）
  - BattleStats（战斗数值，见第 3.2）
  - ArenaSpeed（场地速度阈值，见第 3.3）
  - 当前速度累积（见第 3.3）
  - 回调集合（见第 2 章）
  - 玩家攻击处理器 / 敌人攻击处理器（见第 4 章）
  - 数值计算接口引用（见第 5 章）

### 1.3 敌人配置（CharacterConfig / SO）

- **敌人静态属性**来自 `CharacterConfig`（SO）：HP/ATK/DEF/暴击/爆伤/速度等。  
- 本项目不再使用 `FightConfig`；敌人由“怪物生成系统”按回合动态生成（见第 6.2）。

---

## 2. 回调点设计（材料注入的唯一入口）

回调点定义为“能被材料效果订阅/注入”的钩子。V0 建议最小集合如下：

### 2.1 战斗级回调

- `OnBattleEnter`：进入战斗时（回合循环开始前）。  
- `OnBattleStart`：战斗正式开始（可用于开战类效果）。  
- `OnBattleEnd`：战斗结束（胜负已判定）。  
- `OnVictory` / `OnDefeat`：结果回调（可用于战后结算触发）。

### 2.2 攻击级回调（最关键）

- `OnBeforePlayerAttack(ref AttackInfo info)` / `OnAfterPlayerAttack(ref AttackInfo info)`  
- `OnBeforeEnemyAttack(ref AttackInfo info)` / `OnAfterEnemyAttack(ref AttackInfo info)`

> 说明：这里将攻击回调写成 `ref AttackInfo` 的形式，是为了强调“回调里就是允许修改本次攻击数据”；实际 C# 事件/委托是否用 ref 取决于实现方式，V0 可先用“处理器链”保证 ref 修改路径（见第 4 章）。 处理器作为一个接口实现，可以给战斗上下文注入处理器

补充：由于项目使用 Odin，可将“可注入内容”以**接口字段**方式配置（而不是 MonoBehaviour）：

- `IAttackInfoCalculator`（数值计算器）
- `IMaskBattleInjector`（面具/材料注入器）
- `List<IFightComponent>`（战斗组件列表）

推荐使用 Odin 的 `[OdinSerialize]` 来序列化接口字段，方便 Inspector 直接配置。

---

## 3. 战斗流程（状态机与顺序）

### 3.1 战斗进入（初始化 + 注入）

进入战斗时的硬顺序：

- `FightManager` 创建并填充 `FightContext`（绑定 Player/Enemy/MaskLibrary 等引用）。  
- 按顺序注入面具库中的所有面具（包含当前展示面具）：  
  - 面具顺序：面具库列表顺序  
  - 材料顺序：每个面具内部按材料链表顺序  
  - 组件顺序：每个材料内部按组件遍历顺序  
  - 注入到：回调集合（订阅回调）与攻击处理器链（注册修改器）
- 触发 `OnBattleEnter`  
- 触发 `OnBattleStart`

### 3.2 玩家三层数值（基础/实际/战斗）

本项目玩家数值分为三层（所有可配置的玩家属性都必须区分这三层）：

- **基础数值（BaseStats）**：只在配置中定义（第一回合的起点）。  
- **实际数值（ActualStats）**：跨战斗持久存在的数值（玩家“真正拥有”的成长），只在战斗结束后通过“持久增值收集”进行修改。  
- **战斗数值（BattleStats）**：仅本场战斗有效；开战时由 `ActualStats` 复制/构建，并叠加面具库材料的“战斗增益”后用于战斗结算。战斗中对战斗数值的改变不会直接改写玩家实际数值。

### 3.3 速度条机制（替代回合制）

场地存在一个**场地速度阈值（ArenaSpeedThreshold，整数）**，玩家/敌人各有：

- **SpeedRate（整数）**：表示每秒增长多少“速度值”。  
- **SpeedValue（运行时累积，浮点/整数均可）**：每帧按 \(SpeedRate \times dt\) 增加。

当某一方的 `SpeedValue >= ArenaSpeedThreshold` 时：

- 执行该方一次攻击（生成 `AttackInfo` → 处理链 → calculator → 结算伤害）。  
- 执行后 `SpeedValue -= ArenaSpeedThreshold`。  
- **溢出连续攻击**：如果扣除后仍满足 `SpeedValue >= ArenaSpeedThreshold`，允许再次攻击；但两次攻击之间必须等待“攻击动画间隔”，并且最小间隔不得低于该动画间隔。

约束：

- 攻击动画间隔作为 `GameSetting` 中的常量（例如 `GameSetting.AttackAnimIntervalSeconds`）。  
- 场地速度阈值可配置/可变（例如随关卡变化）。

### 3.3 战斗结束（胜负与回调）

- 判定胜负后：
  - `OnBattleEnd`
  - `OnVictory` 或 `OnDefeat`
- 战斗系统把“结果”抛给上层（例如 GameLoop/流程管理器），由上层执行掉落、面具入库、以及持久增值结算（见第 6 章）。

---

## 4. AttackInfo 与处理链（材料链表顺序）

### 4.1 AttackInfo（每次攻击创建）

每次攻击都创建一个 `AttackInfo`（引用类型/值类型均可，V0 不强约束），至少字段：

- `BaseValue`：基础攻击值（来源于攻击者静态属性 + 其它加成）。  
- `CritChance`：暴击概率。  
- `CritMultiplier`：暴击乘数。  
- `RawAttack`：**未结算暴击**的实际攻击（主要供处理链修改）。  

> 注意：是否在 `AttackInfo` 内携带“最终伤害/是否暴击”等字段，V0 可先不定，交给数值接口去填充也可。

### 4.2 双处理器（玩家/敌人各一套）

必须存在两套处理器：

- **PlayerAttackProcessor**：只处理玩家发起攻击的 `AttackInfo` 修改链  
- **EnemyAttackProcessor**：只处理敌人发起攻击的 `AttackInfo` 修改链

处理器职责：

- 接收 `AttackInfo`（推荐 `ref AttackInfo`）。  
- 按顺序执行修改器：
  - **材料效果修改器按“材料链表顺序”执行**  
  - 处理器自身可追加基础规则（尽量少）  
- 输出修改后的 `AttackInfo`，供数值计算接口使用。

> V0 约束：如果同一字段被多个材料修改，不引入额外优先级系统，**以链表顺序得到最终结果**。

---

## 5. 数值计算接口（V0 占位实现）

### 5.1 设计目标

- 让战斗流程可跑通（闭环）  
- 让材料/处理器可以通过修改 `AttackInfo` 影响结果  
- 不在 V0 固化最终伤害公式

### 5.2 接口定义（文档级规范）

数值计算先抽象为一个接口，接收 `AttackInfo` 并通过 `ref` 修改：

```csharp
public interface IAttackInfoCalculator
{
    // attacker/defender 可按项目现状替换为具体类型，V0 只规定“必须能取到双方运行时实例数据”
    void Calculate(ref AttackInfo info, FightContext context );
}
```

约定：

- **输入**：处理器链处理后的 `AttackInfo`（仍可能被进一步改写）。  
- **输出**：通过 `ref` 修改 `AttackInfo`（例如写入本次是否暴击、最终伤害、或直接改写 RawAttack 作为最终伤害）。  
- **应用**：由 `FightManager` 根据 `AttackInfo` 的输出字段去扣除 defender 的 HP，并判定胜负。

 > 说明：Calculator 通过 `FightContext` 获取“本次攻击方/防守方”的运行时实例（例如 `context.CurrentAttacker / context.CurrentDefender`），从而避免把 attacker/defender 类型写死在接口签名里。

---

## 6. 可配置项（最小集合）

V0 建议至少可配置：

- 玩家基础属性（BaseStats）与速度（SpeedRate）  
- 敌人基础属性与速度（SpeedRate）  
- 场地速度阈值默认值（`GameSetting.DefaultArenaSpeedThreshold`）  
- 攻击动画间隔常量（`GameSetting.AttackAnimIntervalSeconds`）  
- 掉落（材料池/方法/数量）：在 `GameManager` 中配置（Inspector），便于后续替换算法  

补充：掉落机制建议参考 BaseSpec 的“材料池 + 幸运值 + 品质 + 掉落方法接口”。

---

## 6.1 持久增值（Persistent Growth）结算（战后修改玩家实际数值）

### 6.1.1 目标

- 开战时：材料的“战斗增益/持久增益”都只作用于 **BattleStats**（不直接改玩家 ActualStats）。  
- 战斗结束：遍历所有面具库，收集“持久增值”，最终一次性写入玩家 **ActualStats**。

### 6.1.2 数据结构建议

- `PlayerGrowthDelta`：记录本场战斗结算后玩家应增加的值（可加可减，但“持久增值”建议以加为主）。  
  - 攻击增量、防御增量、暴击率增量、生命上限增量等（按项目需要扩展）

### 6.1.3 组件接口建议

材料/面具上的某些组件可以实现一个接口来参与战后结算：

```csharp
public interface IPersistentGrowthProvider
{
    // battleContext 可选，用于读取本场战斗信息（胜负、回合/时间等）
    void OnCollectPersistentGrowth(Player player, PlayerGrowthDelta delta, FightContext battleContext);
}
```

战后结算顺序建议：

- 面具库顺序 → 每个面具材料顺序 → 每个材料组件顺序  
- 若接口返回/写入冲突字段，按顺序累加到 `delta`，最后统一 Apply 到 `player.ActualStats`。

---

## 6.2 怪物生成系统（生成逻辑链）

### 6.2.1 目标

- 战斗系统不直接决定“生成什么怪”。  
- 提供一个“生成逻辑链”，按顺序尝试生成；某条逻辑返回 null 则继续下一条。

### 6.2.2 接口建议

```csharp
public interface IMonsterSpawnLogic
{
    // roundIndex：第几场战斗/第几回合（由你定义口径，V0 建议用“战斗场次”）
    // 返回 null 表示本逻辑不处理，交给后续逻辑
    CharacterConfig TrySpawn(int roundIndex, FightContext context);
}
```

### 6.2.3 初始化与调用

- 生成系统在系统初始化时实例化生成逻辑链（`GameManager.Awake()` 中）。  
- 战斗系统在需要生成敌人时调用：  
  - `enemy = MonsterSpawnSystem.Spawn(roundIndex, context)`  
  - 内部遍历逻辑链，返回第一个非 null 的怪物实例。

---

## 7. V0 落地清单（实现顺序建议）

只写执行顺序，避免过度架构：

- 打通 `FightManager` 的回合循环（能自动推进、能结束）。  
- 定义 `FightContext` 并把回调集合挂上去。  
- 实现 `AttackInfo` + 双处理器（先只做“空处理链”，保证流程跑通）。  
- 接上 `IAttackInfoCalculator` 的最简实现（例如最终伤害=RawAttack，先不算防御/暴击）。  
- 再逐步把材料效果通过“链表顺序”注入到回调/处理器中。


