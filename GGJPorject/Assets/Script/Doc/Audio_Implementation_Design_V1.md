# Audio 实现理解与实现思路（V1）

> 本文件用于在写代码前，固定我对需求的理解与落地方式（避免“我理解错了但已经写了很多代码”）。  
> 你已在聊天中确认：`BarSeconds = 1.6s`、Timeline 不受暂停影响、Play/Stop 的参数是“资源 key”。

---

## 1. 需求理解（与当前文档口径一致）

- **对外入口**：所有播放/停止都走 `AudioManager`，外部不直接调用 `AudioTimeline`
- **索引方式**：外部使用 `AudioKey` 枚举，调用时 `audioKey.ToString()` → string key
- **资源配置**：用 `AudioEntrySO`（Resources 自动加载）提供 `key/clip/volume/track`
  - 不需要 `type` 字段：BGM 由 `Play(...)` 系列 API 触发，SFX 由 `PlaySfxOnce(...)` 触发
- **Timeline（音乐时间基准）**：
  - `timelineTime` 用 **`Time.unscaledDeltaTime`** 推进
  - 以 `GameSetting.BarSeconds = 1.6f` 作为小节长度
- **BGM 多轨**：
  - 多条 `BgmTrack` 可同时播放（不同轨并行）
  - 同一轨道被新 BGM 请求占用时，触发“排挤”
  - 每条轨道使用 **两个 AudioSource（A/B）** 来实现并行 FadeOut+FadeIn（cross-fade）
- **排挤与 cross-fade**：
  - Play 请求不会立刻开声：会在“小节边界”统一处理（缓冲池）
  - 在小节边界处理时，如果同轨已有在播，则：
    - 旧 source FadeOut
    - 新 source 同时 FadeIn
    - Fade 时长 = 本小节剩余时间（在边界处理时等于 1.6s）
  - Fade 使用 DOTween 且必须 `SetUpdate(true)`，保证 TimeScale=0 时也能跑
- **Stop(key)**：
  - `key` 是资源 key（不是轨道名）
  - Stop 时根据 key 找到对应 entry 的 `track`，并只停止该 key 对应的 BGM（若当前未在播则忽略）
- **SFX**：
  - `PlaySfxOnce` 立即播放，不参与 timeline、不参与对齐、不进入缓冲池

---

## 2. 实现思路（尽量简单、符合 Jam）

### 2.1 将新增/修改的脚本

- `Assets/Script/GameSetting.cs`：放 `BarSeconds=1.6f` 等常量
- `Assets/Script/GameManager.cs`：统一初始化（Awake 初始化单例；Start 触发资源加载）
- `Assets/Script/Audio/AudioEntrySO.cs`：SO 定义
- `Assets/Script/Audio/AudioTimeline.cs`：维护 timelineTime + bar 边界事件 + 多轨 A/B source
- `Assets/Script/Audio/AudioManager.cs`：对外 API + 缓冲池 + Resources.LoadAll + Odin 试音按钮
- `Assets/Script/Audio/AudioKey.cs`、`Assets/Script/Audio/BgmTrack.cs`：已创建（占位）

### 2.2 生命周期与初始化

- `GameManager.Awake()`：
  - 创建 `AudioTimeline`、`AudioManager` 并建立引用
  - 设置 `AudioManager.I`（不允许 AudioManager 自己在 Awake 里抢初始化）
- `GameManager.Start()`：
  - 调用 `AudioManager.LoadAllEntriesFromResources()`

### 2.3 风险点与规避

- **TimeScale=0 时 Fade 不走**：所有 DOTween tween 必须 `SetUpdate(true)`  
- **重复 key**：LoadAll 时打印错误并保留第一个  
- **同轨同小节多次请求**：缓冲池按 Track 只保留最后一次  




