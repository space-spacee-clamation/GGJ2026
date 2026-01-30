# 面具/材料系统设计文档（V0）

> 适用范围：经营/合成阶段的“面具 + 材料”构筑系统，以及进入战斗前的“注入 FightContext”流程。  
> 依赖文档：`AI_Skill_BaseSpec_Requirements_V0.md`（第 3/4 章）与 `Fight_System_Spec_V0.md`（注入/链表顺序/接口注入约定）。  
> Jam 取舍：先保证闭环落地；材料效果用“组件组合 + 链表顺序”实现；不做复杂编辑器/DSL/反射注册。

---

## 0. 强约束（必须遵守）

- **面具库（多面具）**：存在“面具库（MaskLibrary）”，会持有多个面具。  
- **战后入库持续生效**：战斗结束后旧面具不会丢弃，而是进入面具库；面具库中的所有面具会在后续战斗持续生效（类似肉鸽道具库）。  
- **当前面具仅表现差异**：当前面具与面具库中的面具差别仅在表现/UI，逻辑一致。  
- **材料来源唯一**：经营阶段无商店/刷新/购买；材料只来自战斗掉落；掉落内容为手动配置。  
- **法力值恢复=材料效果**：不存在金币或其它交互系统，恢复行为只能由材料（即时生效）驱动。  
- **链表顺序规则**：材料效果以“已附加材料列表顺序（链表）”触发/注入；冲突与叠加以顺序得到最终结果（V0 不引入优先级系统）。

---

## 1. 核心对象与职责（建议命名）

### 1.1 MaskObj.StaticConfig（面具基础配置 / 组件内嵌）

- **职责**：面具基础法力值与展示信息。  
- **字段**：
  - 基础法力值（BaseMana）  
  - 名称、图标、描述  

### 1.2 MaskObj（面具运行时对象）

- **职责**：
  - 持有当前法力值（CurrentMana）  
  - 持有已附加材料列表（链表/列表）  
  - 提供“绑定材料”能力  
  - 进入战斗前：把材料效果按链表顺序注入 `FightContext`
  - 战后：面具进入面具库，并在后续战斗持续生效

### 1.3 MaskMakeManager（面具制造管理器）

- **职责**：管理“制造面具阶段”的流程，并按顺序从 `List<MaskObj.StaticConfig>` 生成 `MaskObj`。  
- **规则**：每次制造取 `nextMaskIndex` 对应配置，制造后 `nextMaskIndex++`（循环）。  
- **初始化**：必须在 `GameManager.Awake()` 中创建并 `Initialize()`（遵循项目初始化强约束）。

补充：底板面具机制

- `MaskMakeManager` 持有一个“底板面具预制体（BaseMaskPrefab）”。  
- 每次进入制造面具回合：都先实例化这个底板面具，再应用本次 `StaticConfig`，然后再进行材料附加。  

补充：面具库的管理方式（V0 建议）：

- `MaskMakeManager` 负责“制造新面具”并维护一个面具库列表（或交给上层 GameFlow 管理也可）。  
- 战斗开始前，上层把“面具库内所有面具”依次注入 `FightContext`（见第 4 章）。  

### 1.4 Material Prefab（材料预制体）

每个材料是一个 Prefab，至少包含：

- **`MaterialObj`**：材料根组件（提供 `ManaCost`），并在 `Awake` 自动缓存所有效果组件。  
- **品质（MaterialQuality）**：每个材料带有品质枚举（0~4 低到高），供掉落系统基于幸运值调整概率。  
- **保质期（ShelfLifeTurns）**：每个材料带有保质期回合数；材料在“材料库”中会在每次制造回合结束时衰减，变 0 自动销毁；绑定到面具的材料不参与该结算。  
- **若干效果组件**：通过实现接口决定在何处生效：  
  - `IMaterialBindEffect`：绑定阶段即时生效  
  - `IFightComponent`：订阅战斗回调  
  - `IAttackInfoModifier`：攻击处理链修改  
  - `IPersistentGrowthProvider`（建议新增）：战斗结束时产出“持久增值”写入玩家 ActualStats（详见战斗文档 6.1）  

> 关键边界：`MaskObj` 不判断“会不会触发”，只负责 foreach 注入/触发；组件自己决定是否执行逻辑。

---

## 2. 材料绑定（Attach/Bind）流程

### 2.1 Bind 输入/输出

- **输入**：Material Prefab  
- **输出**：BindResult（成功/失败 + 原因），用于 UI/日志。

失败原因（至少）：

- 材料不合法（缺 `MaterialObj`）  
- 当前法力值不足（CurrentMana < ManaCost）  

### 2.2 Bind 执行顺序（硬顺序）

- 校验材料合法性  
- 校验法力值足够  
- 扣除法力值  
- 将材料实例加入“已附加材料列表”（链表）  
- 构造并传入 `BindContext`，调用 `MaterialObj` 执行所有 `IMaterialBindEffect.OnBind`（见 3 章）  
- 触发“绑定完成回调”（供 UI/统计/连锁效果使用）

> 说明：即时生效材料**只在绑定阶段**执行，不会在战斗阶段再次触发（除非材料也挂了战斗效果组件）。

---

## 3. 即时生效材料（经营阶段效果）

### 3.1 BindContext（面具合成上下文）

即时材料执行时必须拿到 `BindContext`，至少包含：

- 当前 `MaskObj`  
- 当前已附加材料列表（绑定前/后需明确；V0 建议传“绑定后列表”）  
- “绑定完成回调/事件入口”（允许即时材料订阅/触发）  

### 3.2 即时效果的边界

- 只允许修改：面具实例、材料列表、以及与合成阶段相关的状态  
- 典型用例：
  - 恢复法力值（符合“恢复=材料效果”约束）
  - 删除/替换某些材料（如果玩法需要）
  - 立即给下一场战斗加一条“战斗组件”（可选）

---

## 4. 进入战斗前的注入（Mask → FightContext）

### 4.1 注入入口（接口）

进入战斗前必须把“面具库中的所有面具”按顺序注入到 `FightContext` 上；每个面具内部再按材料链表顺序注入。

- 推荐接口：`IMaskBattleInjector.InjectBattleContext(FightContext context)`

> 项目现状：已存在 `IMaskBattleInjector`（并允许用 Odin 接口序列化注入到 `FightManager`），后续材料系统只需要实现该接口即可对接战斗系统。

### 4.2 注入内容（必须覆盖）

材料效果在战斗中可介入两条路径（通过 `MaterialObj` 缓存的组件实现）：

- **回调订阅**：订阅 `FightContext` 的战斗/回合/攻击回调（例如开战减敌攻）  
- **处理器链注册**：把材料组件（实现 `IAttackInfoModifier`）注册到：
  - `context.PlayerAttackProcessor`（修改玩家发起攻击的 AttackInfo）
  - `context.EnemyAttackProcessor`（修改敌人发起攻击的 AttackInfo）

补充：关于“持久增值”的位置

- 持久增值不在战斗过程中直接改玩家 ActualStats。  
- 建议在战斗结束时，由上层流程遍历面具库中所有材料组件，调用 `IPersistentGrowthProvider.OnCollectPersistentGrowth(...)` 填充增长值，再统一写回玩家 ActualStats。  

### 4.3 顺序与冲突规则

- 注入顺序严格按“已附加材料列表顺序（链表）”。  
- 多个材料修改同一字段时，不做优先级系统，按链表顺序得到最终结果。

---

## 5. 战后：掉落材料与新面具模板

> 掉落配置在 BaseSpec 第 6.2 章定义；本章只定义“系统交互顺序”。

战斗结束后流程（最小闭环）：

- 战斗系统触发 `OnVictory/OnDefeat` 回调并返回结果  
- 上层流程根据结果：  
  - 读取掉落配置，生成材料（Prefab + Count）加入玩家库存  
  - 战斗结束后旧面具入库；面具库持续存在并在后续战斗持续生效  

---

## 6. 配置建议（SO 与 Odin）

### 6.1 面具配置

- `MaskObj.StaticConfig`：BaseMana + 名称/描述（配置数据不在运行时被修改）  

### 6.2 材料配置

- 材料基础数据（例如法力消耗）放在 `MaterialObj.ManaCost`（Prefab 根组件）  
- 效果逻辑放在若干普通组件上，实现对应接口即可（`IMaterialBindEffect` / `IFightComponent` / `IAttackInfoModifier`）

### 6.3 Odin 配置方式（接口序列化）

若需要在 Inspector 里直接配置“材料效果/注入器/战斗组件”这种接口实现，可使用 Odin 的：

- `[OdinSerialize]` 序列化接口字段（与战斗系统一致）  

---

## 7. V0 落地清单（实现顺序建议）

- 先实现 `MaskMakeManager` 的“按列表顺序生成 MaskObj 并加入面具库”流程  
- 实现 `MaskObj.BindMaterial(material)`：法力值校验/扣除/加入链表/运行 `IMaterialBindEffect.OnBind`  
- 实现 `IMaskBattleInjector.InjectBattleContext`：按链表顺序把战斗效果注入 `FightContext`  


