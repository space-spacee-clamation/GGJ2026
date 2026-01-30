# Audio 系统设定与需求文档（V0）

> **项目背景**：48 小时 Game Jam。目标是“能用、效果完整、排查成本低”。  
> **取舍**：不做复杂通用音频框架；避免为了扩展性引入不必要抽象。

---

## 0. 开发流程约束（强约束）

- **先文档后实现**：任何代码实现前，必须先提交一份「需求理解与实现思路」供你确认；你修正后再写代码。  
- **生命周期一致**：所有管理类/单例由 `GameManager` 统一创建与初始化：  
  - `GameManager.Awake()`：初始化所有单例（包含 `AudioManager`、`AudioTimeline` 等）。  
  - `GameManager.Start()`：进行其它内容初始化（例如加载资源、开局播放 BGM 等）。

---

## 1. 需求理解（按你提供的 1-6 条落地）

### 1.1 总体目标

实现一套 **按“小节(Bar)”对齐的音乐播放机制**：

- 外部只通过 `AudioManager` 提交播放/淡入淡出请求（使用 `string key` 索引资源）。
- 实际播放由 `AudioTimeline` 执行（持有 `AudioSource` 引用）。
- **音乐类(BGM/Loop)**：播放与淡入需对齐到“小节结束”时刻，并且按 `timelineTime` 进行相位对齐（取余计算起播偏移）。
- **音效类(SFX/PlayOnce)**：不参与 Timeline 对齐，收到请求立即播放。

### 1.2 关键约束

- 不要求可扩展到复杂编曲/多轨编辑器；实现以“游戏可用”为第一目标。
- 为避免时序 bug，强制由 `GameManager` 负责初始化与依赖注入（见第 0 章）。

---

## 2. 系统组成与职责

### 2.1 `AudioManager`（对外 API 层）

**职责**

- 提供对外调用 API：通过 `string key` 播放/停止/淡入/淡出。
- 维护音乐请求的 **缓冲池/队列**：音乐请求不立即播放，而是等待“当前小节结束”再执行。
- 在游戏开始时使用 `Resources` 读取所有音频资源配置 SO，并建立 `key -> entry` 的索引表。
- 屏蔽外部直接访问 `AudioTimeline`（外部不得直接调用 Timeline）。
- `AudioManager` 可以是 **MonoBehaviour**（挂在场景物体上），由 `GameManager` 负责创建与初始化引用。

**对外 API（文档级约定，后续实现可按需要微调）**

- `PlayBgmFadeIn(string key, float fadeInSeconds)`
- `StopBgmFadeOut(string key, float fadeOutSeconds)` 或 `StopCurrentBgmFadeOut(float fadeOutSeconds)`
- `PlaySfxOnce(string key, float volumeMul = 1f)`
- `StopAllBgmFadeOut(float fadeOutSeconds)`（可选，便于切场）

**StopAll 与 Background（强约束）**

- `StopAllBgmFadeOut(...)` **不会停止** `isBackground == true` 的音频
- Background 使用独立轨道 `BgmTrack.Background`

**新增便捷 API（默认参数）**

> 你要求“再提供 play/stop API 可直接按默认参数进行 fadeIn/fadeOut”，因此补充：

- `Play(string key)`：**BGM 专用**，参数 `key` 为**资源 key**（不是轨道），按默认规则播放（小节对齐 + 相位对齐 + Track 占用/排挤）  
- `Stop(string key)`：**BGM 专用**，参数 `key` 为**资源 key**（不是轨道），停止该 key 对应的 BGM（若不在播则忽略）  

> 说明：key 统一走资源 SO 的映射，不允许外部直接传 `AudioClip`。

**key 约定（强约束）**

- 项目内以 `enum AudioKey` 维护所有可用 key  
- 调用时用 `audioKey.ToString()` 转为 string（减少手打拼写错误）  
- SO 的 `key` 字段必须与枚举名一致

### 2.2 `AudioTimeline`（实际播放执行层）

**职责**

- 持有并管理多个 BGM Track（每条 Track 两个 `AudioSource`：A/B，用于 cross-fade）。
- 提供底层能力：设置 clip、设置播放起点偏移、开始播放、按时间更新淡入淡出。
- 持有并维护一个 **Timeline Time**（`timelineTime`，单位秒），作为音乐相位对齐的时间基准。

**外部访问限制**

- `AudioTimeline` 不对业务脚本公开播放入口；只能由 `AudioManager` 调用其内部方法（或由 `AudioManager` 持有引用并驱动）。

---

## 3. 小节(Bar)机制与相位对齐规则（核心）

### 3.1 小节长度来源：`GameSetting`

新增（或补充）一个 `GameSetting` 类，包含：

- `public const float BarSeconds = 1.6f;`（单位：秒）

> **确认**：`BarSeconds` 暂定为 **1.6 秒**。

### 3.2 “缓冲池”规则（音乐类请求）

`AudioManager` 收到 **音乐类 FadeIn/Play 请求**后：

- **不立即开始播放**  
- 将请求放入“缓冲池/队列”
- 当检测到 **当前小节结束** 的瞬间，取出请求执行播放并开始 FadeIn

### 3.3 小节边界判定（建议实现方式）

为减少抖动与帧率影响，建议用累积时间判定：

- `timelineTime += unscaledDeltaTime`（Timeline 不受暂停影响）
- `barIndex = floor(timelineTime / BarSeconds)`
- 当 `barIndex` 从 N 变为 N+1 时，认为“进入新小节”，此刻处理缓冲池

> 允许用 `AudioSettings.dspTime` 做更稳的时间源；但 Game Jam 场景下优先保证实现简单与可调试。

**暂停相关口径（强约束）**

- Timeline **不需要暂停**：TimeScale=0 时仍然推进（必须使用 `unscaledDeltaTime` 或等价时间源）
- 暂停时可能播放其它音频、也可能不暂停游戏：Audio 系统不得假设“暂停一定发生/一定不发生”

### 3.4 相位对齐：按 Timeline Time 取余决定起播偏移（音乐类）

规则来自你的描述：

- 已知 `timelineTime`（单位秒），clip 长度 `clipLen`（单位秒）
- 计算起播偏移：`offset = timelineTime % clipLen`
- **播放时从 `offset` 秒开始播放**（而不是从 0 秒）

示例：

- `timelineTime = 14s`
- `clipLen = 3s`
- `offset = 14 % 3 = 2s`
- 则开始播放时从 **第 2 秒**位置进入

> 目的：切入时保持音乐“相位一致”，即便是中途换轨也能听起来对齐。

### 3.5 FadeIn / FadeOut 行为（音乐类）

- **FadeIn**：只在“小节边界”执行开始播放，并从 0 音量插值到目标音量  
- **FadeOut**：可立即开始淡出（不强制对齐小节），淡出结束后停止/清空 clip  

> 若你希望 FadeOut 也必须对齐小节边界，请明确说明；当前按需求只对 FadeIn 做小节对齐。

---

## 3.6 Fade 实现约定：使用 DOTween（强烈建议）

你已导入 DOTween，因此 V0 的 Fade 约定为：

- FadeIn / FadeOut 使用 DOTween 对 `AudioSource.volume` 做 Tween（例如 `DOFade` 或 `DOTween.To`）  
- 每个 `AudioSource` 的 Fade 在开始新的 Fade 前，应 **Kill 旧 Tween**（避免叠加导致音量错误）  
- Timeline 不受暂停影响：Fade Tween 必须 **`SetUpdate(true)`**，保证 TimeScale=0 时也能执行  
- 默认参数（供 `Play(key)` / `Stop(key)` 使用）建议放在 `GameSetting` 中作为 const 或 readonly 字段（Jam 场景无需做复杂配置系统）

---

## 3.7 多轨 BGM（强约束，按“轨道占用/排挤”规则）

> 你要求“会有很多个 BGM 同时播放”，因此系统必须支持 **多轨并行**，但“是否需要排挤替换”由资源 SO 控制。

**关于 cross-fade**

- 你描述的“同轨排挤：旧的 fadeOut，新的一条并行 fadeIn，且同一轨使用两个 Audio 组件”本质上就是 **cross-fade**  
- 本文档对 cross-fade 的定义：**同一轨道内 A/B 两个 AudioSource 并行淡出/淡入以完成切换**

### 3.7.0 行为总览（按 SO 开关）

- **默认（SO 未勾选 `useReplaceLogic`）**
  - **非 Background**：无论 `track` 填什么，都在“下一小节开始”**并行叠加播放**（不做排挤替换）
  - **Background**：走 Background 专用轨道，默认 **直接替换**（不做并行叠加）
- **勾选 `useReplaceLogic`**
  - 非 Background：按 `track` 执行“同轨 A/B cross-fade 排挤”
  - Background：也允许执行“同轨 A/B cross-fade 排挤”

> **记住**：无论哪种模式，仍然必须遵守第 3 章“小节对齐 + 相位对齐”的设计。

### 3.7.1 声轨（Track）定义

- 每个 BGM 资源在 SO 中标记一个 `BgmTrack track`
- **Background**：使用 `BgmTrack.Background`（独立轨道）
- `AudioTimeline` 内部维护多个 Track（数量与 `BgmTrack` 枚举一致）

### 3.7.2 每条 Track 的播放结构：双 AudioSource

为实现“排挤时并行 FadeOut + FadeIn”，每条 Track 采用 **两个 AudioSource**（A/B）：

- 任意时刻只有一个为“Active”（当前在播/占用）
- 另一个为“Inactive”（用于下一次切换时承载新 clip）

### 3.7.3 Track 占用与排挤规则（核心）

当 `AudioManager` 在“小节边界”处理到一个 BGM 播放请求时：

- 若 `useReplaceLogic == false`：
  - 非 Background：并行播放（不进入本节“排挤规则”）
  - Background：直接替换（停止旧背景后播放新背景，不并行叠加）
- 若 `useReplaceLogic == true`：
  - 进入“排挤规则”（本节）

排挤规则流程：

- 找到该 BGM 的 `track`（Background 固定为 `BgmTrack.Background`）
- 若该 Track 当前没有在播：
  - 直接在 Active（任选 A/B）设置 clip 并播放（按 3.4 的相位对齐 offset），同时从 0 用 DOTween Fade 到目标音量
- 若该 Track 当前已有在播（存在 ActiveSource）：
  - 计算 **当前小节剩余时间**：`barRemain = BarSeconds - (timelineTime % BarSeconds)`
  - 对旧的 ActiveSource 执行 **FadeOut**，时长 = `barRemain`（结束后 Stop 并清空/释放）
  - 同时在 InactiveSource 上设置新 clip：
    - 按 3.4 相位对齐：从 `offset = timelineTime % clipLen` 秒处开始播放
    - 音量从 0 开始，用 DOTween **FadeIn** 到目标音量，时长 = `barRemain`
  - 交换 Active/Inactive 身份（切换完成后新 source 成为 Active）

> 说明：这里的 `barRemain` 用于让切换“在当前小节结束前完成”，听感更自然；Jam 场景优先规则清晰可验证。

### 3.7.4 缓冲池与 Track 的关系

- 缓冲池仍以“小节边界”为统一结算点（避免任意时刻切换导致难以调试）
- **同一条 Track** 在同一小节内如果收到多次请求：只保留最后一次（避免排挤链式发生）

---

## 4. PlayOnce 音效规则（不参与 Timeline）

### 4.1 行为

- `PlaySfxOnce(key)`：收到请求后 **立刻播放**  
- 不使用 `timelineTime` 取余  
- 不进入“缓冲池”

### 4.2 播放方式建议

为了省时间与降低管理成本，建议：

- 直接使用 `AudioSource.PlayOneShot(clip, volume)`  
- 或使用一个简单 `sfxSource` 专用 AudioSource

---

## 5. 资源配置：Audio 资源 ScriptableObject（SO）

### 5.1 SO 内容（V0 最小字段）

需要一个资源配置 SO（命名示例：`AudioEntrySO`），至少包含：

- `string key`：对外索引键  
- `AudioClip clip`：音频文件引用  
- `float volume`：资源默认音量（**必填**，用于统一调音与试音）
- `BgmTrack track`：声轨枚举（**仅 BGM 使用**；SFX 可忽略/固定为默认值）
- `bool useReplaceLogic`：是否启用“同轨排挤/cross-fade 替换逻辑”（**默认 false**）
- `bool isBackground`：是否为 Background（StopAll 不会停止 Background；默认 false）

### 5.2 Resources 加载约定（强约束）

- 所有 AudioEntry SO 必须放在 `Resources` 目录下（示例：`Assets/Resources/AudioEntries/`）  
- `AudioManager` 在启动时使用 `Resources.LoadAll<AudioEntrySO>(path)` 读取  
- 建立 `Dictionary<string, AudioEntrySO>` 索引：
  - key 重复：打印错误并保留第一个（或覆盖，需固定规则，避免随机）

> 注意：你要求“文件使用 Resource Api 进行读取”，因此 V0 不做 Addressables/AssetBundle。

---

## 7. 试音工具（Odin）（V0 强烈建议实现）

> 目的：Game Jam 现场“快速验证 key 是否正确、音量是否合适、Fade 是否符合预期、BGM 是否对齐小节”。

### 7.1 形式

允许两种做法，二选一即可：

- **做法 A（推荐）**：在 `AudioManager` 上用 Odin 画按钮/下拉（Inspector 工具区）  
- **做法 B**：做一个 Odin `OdinEditorWindow`（菜单 `Tools/Audio Tester`）

> 本项目按你的要求选择 **做法 A**。

### 7.2 工具能力（最小集）

- 输入/选择 `AudioKey`（Odin 直接下拉枚举；播放时用 `audioKey.ToString()` 转 string key）  
- 按钮：
  - `Play(key)`（默认淡入/默认音量）  
  - `Stop(key)`（默认淡出）  
  - `PlaySfxOnce(key)`（立即播放）  
- 显示：
  - 当前 `timelineTime`  
  - 当前 `barIndex` / 距离下一小节还有多少秒（便于验证“缓冲池对齐”）

### 7.3 重要约束

- 工具只用于调试与验证，不要求完善权限/保存配置。  
- 允许把工具代码直接放在 `AudioManager` 内（Jam 取舍），但要尽量与运行时逻辑隔离（例如用 `#if UNITY_EDITOR` 包裹 EditorWindow 相关代码）。

---

## 8. 关键边界情况（必须在实现中明确处理）

- **key 不存在**：不崩溃，输出清晰日志（包含 key）并忽略请求  
- **clip 为空**：同上  
- **clipLen <= 0**：忽略相位对齐，直接从 0 播放（并报警）  
- **timelineTime 巨大**：允许取余计算（float 精度问题可接受，Jam 场景不深究）  
- **同一小节内多次 FadeIn 请求**：需要一个明确策略（V0 建议“只保留最后一次请求”，避免堆积）

---

## 9. 验收标准（V0）

- **音乐淡入对齐**：在任意时刻触发 `PlayBgmFadeIn(key)`，实际开始播放一定发生在“下一次小节边界”  
- **相位对齐**：在 `timelineTime = 14s` 时触发播放 3s 音频，起播偏移为 2s（可通过调试日志验证）  
- **音效不对齐**：`PlaySfxOnce` 立刻有声音，不等待小节  
- **资源自动读取**：新增一个 AudioEntry SO 放入 Resources 后，无需手动注册即可通过 key 播放  

---

## 10. 需要你补充确认的信息（用于落地实现）

- `GameSetting.BarSeconds` 的具体值是多少？（已确认：暂定 **1.6s**）  
- Timeline 是否在“暂停/TimeScale=0”时仍然推进？（已确认：**仍然推进**，因此用 `unscaledDeltaTime`）  
- BGM 是否需要 cross-fade？（已确认：**需要**，同轨 A/B 双 AudioSource 并行淡出/淡入）  



