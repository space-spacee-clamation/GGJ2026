# 合成面具 UI 设计文档（V0）

> 适用范围：制造面具阶段（Make Phase）的 UI：材料库存可视化、材料选择、信息展示、合成绑定、进入战斗。
> 依赖系统：`Mask_System_Spec_V0.md`、`AI_Skill_BaseSpec_Requirements_V0.md`、`Fight_System_Spec_V0.md`（流程：制造 → 等待玩家完成 → 战斗）。
> 工程约束：`GameManager` 使用 UniTask 严格顺序等待；UI 必须在玩家完成操作后调用 `GameManager.NotifyMakeMaskFinished()`。

---

## 0. 核心交互目标

- 玩家在制造阶段：
  - 浏览材料库存（带保质期）
  - 点击材料生成“已选材料（ChoicedMaterial）”实例到面具区域（随机位置）
  - 查看材料信息（名字/描述/保质期）
  - 点击已选材料可收回库存
  - 点击 Compose 将已选材料“绑定到面具”（消耗库存中的材料实例）
  - 当已选材料区不为空时禁止 Next
  - 点击 Next 结束制造阶段并进入战斗阶段

---

## 1. UI 组件清单（Prefab 结构建议）

### 1.1 `MakeMuskUI`

必须引用的组件/预制体：

- **InfoNode**：显示鼠标/当前选择的材料信息（名字/描述/保质期）
- **MaskImage**：用于显示面具图像的 `UnityEngine.UI.Image`
  - `baseMaskSprite`：默认显示
  - `composedMaskSprite`：合成后替换显示（V0 规则：发生任意一次 Compose 后替换）
- **ScrollView**：材料库存列表容器（建议结构：`ScrollRect` + `Viewport` + `Content(VerticalLayoutGroup + ContentSizeFitter)`）
- **NextButton**：结束制造阶段按钮（触发 `GameManager.NotifyMakeMaskFinished()`）
- **ComposeButton**：绑定选中材料按钮（把已选材料绑定到 `MaskObj`，并删除对应库存实例与 ChoicedMaterial UI）
- **MaterialButton Prefab**：库存一行一个
- **ChoicedMaterial Prefab**：面具区域随机生成的已选材料 UI

### 1.2 `InfoNode`

包含三个 `TextMeshProUGUI`：

- 名字（Name）
- 描述（Description）
- 保质期（TTL，显示 RemainingShelfLifeTurns）

对外接口建议：

- `Show(string name, string desc, int remainingTurns)`
- `Clear()`

### 1.3 `MaterialButton`

代表“库存中的一个材料实例（MaterialObj）”：

- `Image`：使用 `MaterialObj.BaseSprite` 作为主图
- **品质描边**：根据 `MaterialQuality` 自带一个更细的颜色描边（颜色来自 `GameSetting`）
- **选中描边**：被选中时显示绿色描边（shader 实现）

交互：

- 点击后：
  - 选中该材料（从库存移到已选区）
  - 调用 `InfoNode.Show(...)`
  - 在面具区域随机生成一个 `ChoicedMaterial`

### 1.4 `ChoicedMaterial`

表示已选材料（仍对应同一个 `MaterialObj` 实例）：

- `Image`：同样使用 `BaseSprite`，尺寸与 sprite 原始长宽一致（保持比例）
- **选中描边**：点击/悬停时显示红色描边（shader）

交互：

- 点击后：收回库存（删除该 ChoicedMaterial UI，并让 MaterialButton 回到列表）

---

## 2. 材料描述生成（组件顺序）

规则：材料描述由材料组件按组件顺序依次写入一个 `StringBuilder`。

建议接口：

```csharp
public interface IMaterialDescriptionProvider
{
    void AppendDescription(System.Text.StringBuilder sb);
}
```

材料描述生成入口建议放在 `MaterialObj`：

- `BuildDescription(StringBuilder sb)`：遍历本 `GameObject` 上所有 `MonoBehaviour`，按组件顺序调用 `AppendDescription`。

---

## 3. 描边实现（shader）

需求：

- 基于 `Image.sprite` 的 alpha 通道做描边（非矩形）
- 颜色可配置：
  - 品质描边：白/绿/紫/金/红（低→高）
  - 选中描边：绿色（MaterialButton）、红色（ChoicedMaterial）

V0 建议实现方式：

- 一个 UI Shader：`UI/AlphaOutline`
  - Properties: `_OutlineColor`, `_OutlineWidth`
  - 通过周围采样 alpha 判断边缘并输出描边颜色
- 叠层结构：
  - 品质描边（细）
  - 选中描边（粗，覆盖在品质描边外）

---

## 4. 阶段与流程对接（UniTask）

- 制造阶段开始：`GameManager` 生成底板面具，并显示 `MakeMuskUI`
- 玩家点击 Next：
  - `MakeMuskUI` 必须检查：已选区为空才允许 Next
  - 调用 `GameManager.NotifyMakeMaskFinished()`
  - UI 可隐藏/禁用（战斗结束后再显示）

---

## 5. V0 简化与开放问题

- V0 暂不实现“面具图片更换规则”，先用 `composedMaskSprite` 作为合成后统一替换。
- 材料库存排序：V0 建议按保质期（更快过期优先）显示；也可后续加品质排序。
- `ScrollView` 需要的组件：`ScrollRect + Mask + Image(Viewport) + VerticalLayoutGroup + ContentSizeFitter`。


