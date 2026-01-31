# 战斗 UI 设计文档（V0）

> 适用范围：战斗阶段 UI（血条/速度条/角色图像/面具环绕/攻击位移动画/伤害飘字）。
> 依赖系统：`Fight_System_Spec_V0.md`（速度条战斗）、`Mask_System_Spec_V0.md`（面具库）、`MakeMask_UI_Spec_V0.md`（合成结束的面具显示结果）。
> Jam 取舍：先实现闭环展示与基础动画；不做复杂状态机/Timeline；动画与 UI 刷新以稳定为优先。

---

## 0. 目标

- 在战斗中实时显示：
  - 玩家/怪物血条（HP）
  - 玩家/怪物速度条（SpeedValue 相对场地阈值 ArenaSpeedThreshold 的比值）
  - 玩家/怪物立绘（Image）
  - 玩家当前佩戴面具显示在“脸”位置，其它面具围绕脸做天体运动
  - 攻击动画：DOTween 往返位移，达到命中距离触发回调（生成伤害数字）
  - 伤害飘字：显示最终伤害；暴击时红色描边

---

## 1. UI 组件清单（Prefab 结构建议）

### 1.1 `BattleUI`

必须引用：

- **玩家区域**
  - `playerSpriteImage`：玩家立绘（不变）
  - `playerFaceAnchor`：脸部锚点（RectTransform，用于挂面具）
  - `playerHPBar`：血条 Image（使用 BarFill Shader）
  - `playerSpeedBar`：速度条 Image（使用 BarFill Shader）
  - `playerHPText`（可选）：数值显示（如 `HP 32/100`）
  - `playerSpeedText`（可选）：速度显示（如 `75%`）

- **怪物区域**
  - `enemySpriteImage`
  - `enemyHPBar`
  - `enemySpeedBar`
  - 对应文本（可选）

- **飘字层**
  - `damageTextPrefab`：TextMeshProUGUI（或 TMP）
  - `damageTextRoot`：父节点（Canvas 下 overlay 层）

- **动画参数（可从 GameSetting 读取）**
  - 攻击总时间（秒）
  - 命中触发距离（单位：UI 位置距离，px）

### 1.2 `MaskOrbitView`

用于展示“当前佩戴面具 + 环绕面具”：

- `faceAnchor`：中心点（脸）
- `radius`：半径（px）
- `angularSpeed`：角速度（deg/s 或 rad/s）
- `currentMaskImage`：当前佩戴面具（贴在脸上，位置固定）
- `orbitMaskPrefab`：环绕面具的 Image 预制体

表现规则：

- 当前面具：贴在脸 anchor 上（固定 offset）
- 其它面具：围绕脸 anchor 等角度分布，持续匀速旋转

---

## 2. 数据来源与刷新规则

### 2.1 Fight 数据来源

战斗 UI 不直接计算战斗逻辑，只读取：

- 玩家：
  - `FightManager.Context.Player.CurrentHP / MaxHP`
  - `FightManager.Context.PlayerSpeedValue`
  - `FightManager.Context.ArenaSpeedThreshold`
- 怪物：
  - `FightManager.Context.Enemy.CurrentHP / MaxHP`
  - `FightManager.Context.EnemySpeedValue`

> 速度条比值：`ratio = clamp01(SpeedValue / ArenaSpeedThreshold)`

### 2.2 刷新频率

- HP/速度条：每帧刷新（Update/LateUpdate）即可（Jam 简化）
- 伤害数字：在“命中回调”触发时生成
- 面具环绕：每帧更新角度

---

## 3. 血条/速度条 Shader（BarFill）

需求：

- 一个 UI Shader（或 UI 材质）可用于血条/速度条的“完整度”显示
- 传入：
  - `_Fill01`：0~1
  - `_Reverse`：bool（0/1），决定从左到右 / 从右到左
- 输出：
  - 超出 fill 的部分透明（或显示底色由 UI 结构决定）

V0 建议：

- Shader 名称：`UI/BarFill`
- 用 UV.x 与 `_Fill01` 比较裁剪 alpha
  - LeftToRight：`uv.x <= _Fill01`
  - RightToLeft：`uv.x >= 1 - _Fill01`

---

## 4. 攻击动画（DOTween）

需求：

- 使用 DOTween 做“来回移动”的攻击动画
- 参数来自 `GameSetting`：
  - `AttackTweenTotalSeconds`：往返总时间（例如 0.3s）
  - `AttackHitDistance`：移动到对方方向的距离（px）
- 过程：
  1) 攻击者从初始位置 tween 到 “向目标方向偏移 AttackHitDistance 的位置”
  2) 到达该位置时触发 **命中回调**（用于生成伤害飘字、播放音效等）
  3) 再 tween 回初始位置
- 总时间约束：
  - 往返总时长 = `AttackTweenTotalSeconds`
  - 可拆分：去程 50% + 回程 50%

触发点：

- 命中回调触发时机：到达“偏移位置”的 OnComplete（去程完成）

---

## 5. 伤害飘字（Damage Text）

需求：

- 显示最终伤害值（整数或保留小数由数值系统决定）
- 暴击时添加红色描边（Outline）
- 生成位置：
  - 默认生成在被攻击者角色图片附近（例如头顶/胸口）
- 动画：
  - 上飘 + 淡出（DOTween 或 UniTask tween 均可）

表现规则：

- 非暴击：白色文字，无描边（或细描边）
- 暴击：文字描边为红色（颜色与宽度可写死或放在 GameSetting）

---

## 6. 面具显示（战斗 UI 与合成 UI 的衔接）

需求：

- 玩家立绘不变，但会带面具：
  - 当前佩戴面具显示在脸上
  - 面具库其它面具绕脸旋转

数据来源：

- 合成结束时记录“当前面具的最终 sprite”
  - V0 建议：在 Make 阶段 Compose 后，把 `MaskObj` 的展示 sprite 写到 `MaskObj`（或由 UI 传给 GameManager 存档）
- 战斗开始时，BattleUI 读取面具库：
  - 当前面具 sprite
  - 面具库其它 mask 的 sprite 列表

> V0 不要求面具真正影响战斗 UI 刷新；仅展示层变化。

---

## 7. V0 开放问题（实现前确认）

- 血条/速度条是使用一张 Image + shader 裁剪，还是使用两张 Image（背景+前景）？
  - V0 推荐：背景用普通 Image，前景用 `UI/BarFill`（更直观）
- 面具 sprite 的来源：
  - 由 `MaskObj` 持有一个 `Sprite DisplaySprite`（推荐）
  - 或由 `MakeMuskUI` 合成后设置到 GameManager 的“当前面具展示 sprite”
- 伤害飘字坐标系：ScreenSpace-Overlay 还是跟随 WorldSpace？
  - V0 推荐：UI 纯屏幕坐标（Canvas overlay）



