using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Runtime")]
    [SerializeField] private AudioTimeline timeline;

    [Header("SFX")]
    [SerializeField] private Transform sfxPoolRoot;

    [Header("BGM (Parallel Pool)")]
    [SerializeField] private Transform bgmPoolRoot;

    private readonly Dictionary<string, AudioEntrySO> entries = new();

    // Pending requests (applied at bar boundary)
    private readonly Dictionary<BgmTrack, AudioEntrySO> pendingReplaceByTrack = new();
    private readonly Dictionary<BgmTrack, float> pendingReplaceFadeInByTrack = new();

    private readonly List<AudioEntrySO> pendingParallel = new();
    private readonly List<float> pendingParallelFadeIn = new();

    private AudioEntrySO pendingBackground;
    private float pendingBackgroundFadeIn = -1f;

    // Odin 试音（做法A）
    [FoldoutGroup("Audio Tester"), SerializeField]
    private AudioKey testerKey = AudioKey.BGM_TEST_1;

    [FoldoutGroup("Audio Tester"), SerializeField, Range(0f, 2f)]
    private float testerSfxVolumeMul = 1f;

    public void Initialize(AudioTimeline audioTimeline)
    {
        // 不允许在 AudioManager 自己 Awake 里抢初始化（生命周期强约束：由 GameManager 统一初始化）
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        timeline = audioTimeline;
        if (timeline != null)
        {
            timeline.OnBarBoundary += HandleBarBoundary;
        }

        if (sfxPoolRoot == null)
        {
            var go = new GameObject("SfxPool");
            go.transform.SetParent(transform, false);
            sfxPoolRoot = go.transform;
        }

        if (bgmPoolRoot == null)
        {
            var go = new GameObject("BgmPool");
            go.transform.SetParent(transform, false);
            bgmPoolRoot = go.transform;
        }
    }

    public void LoadAllEntriesFromResources()
    {
        entries.Clear();

        var loaded = Resources.LoadAll<AudioEntrySO>(GameSetting.AudioEntriesResourcesPath);
        foreach (var e in loaded)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.key))
            {
                Debug.LogError($"[Audio] AudioEntrySO missing key: {e.name}", e);
                continue;
            }
            if (entries.ContainsKey(e.key))
            {
                Debug.LogError($"[Audio] Duplicate key '{e.key}'. Keeping first, ignoring: {e.name}", e);
                continue;
            }
            entries.Add(e.key, e);
        }

        Debug.Log($"[Audio] Loaded AudioEntrySO: {entries.Count} (Resources/{GameSetting.AudioEntriesResourcesPath})");
    }

    // ----------------------
    // Public API
    // ----------------------

    public void Play(AudioKey key) => Play(key.ToString());

    /// <summary>
    /// BGM 专用：key 是资源 key（不是轨道），会进入缓冲池，在小节边界处理。
    /// </summary>
    public void Play(string key)
    {
        if (!TryGetEntry(key, out var entry)) return;
        EnqueueBgm(entry, -1f);
    }

    // 兼容文档 API：自定义 fade 秒数（实现仍按 bar 对齐；会 clamp 到当前 bar 剩余时间）
    public void PlayBgmFadeIn(string key, float fadeInSeconds)
    {
        if (!TryGetEntry(key, out var entry)) return;
        EnqueueBgm(entry, fadeInSeconds);
    }

    public void Stop(AudioKey key) => Stop(key.ToString());

    /// <summary>
    /// BGM 专用：key 是资源 key（不是轨道），停止该 key 对应 BGM（若不在播则忽略）。
    /// </summary>
    public void Stop(string key)
    {
        if (!TryGetEntry(key, out var entry)) return;
        if (timeline == null) return;

        // Background：固定走 Background 轨道
        if (entry.isBackground)
        {
            timeline.StopClipOnTrack(BgmTrack.Background, entry.clip, -1f);
            return;
        }

        // 先尝试停止“替换逻辑”轨道内的 clip
        timeline.StopBgmByKey(entry, -1f);

        // 再停止并行池里所有同 clip 的 source
        StopParallelByClip(entry.clip, -1f);
    }

    public void StopBgmFadeOut(string key, float fadeOutSeconds)
    {
        if (!TryGetEntry(key, out var entry)) return;
        if (timeline == null) return;
        if (entry.isBackground)
        {
            timeline.StopClipOnTrack(BgmTrack.Background, entry.clip, fadeOutSeconds);
            return;
        }

        timeline.StopBgmByKey(entry, fadeOutSeconds);
        StopParallelByClip(entry.clip, fadeOutSeconds);
    }

    public void StopAllBgmFadeOut(float fadeOutSeconds)
    {
        if (timeline == null) return;

        // StopAll 不停止 Background（强约束）
        timeline.StopAllBgm(includeBackground: false, fadeSecondsOverride: fadeOutSeconds);
        StopAllParallel(fadeOutSeconds);

        // 清空 pending
        pendingReplaceByTrack.Clear();
        pendingReplaceFadeInByTrack.Clear();
        pendingParallel.Clear();
        pendingParallelFadeIn.Clear();
    }

    public void PlaySfxOnce(AudioKey key, float volumeMul = 1f) => PlaySfxOnce(key.ToString(), volumeMul);

    /// <summary>
    /// SFX：立即播放，不参与 Timeline。
    /// </summary>
    public void PlaySfxOnce(string key, float volumeMul = 1f)
    {
        if (!TryGetEntry(key, out var entry)) return;
        if (entry.clip == null) return;

        float vol = Mathf.Clamp01(entry.volume) * Mathf.Clamp(volumeMul, 0f, 10f);
        PlaySfxPooled(entry.clip, vol);
    }

    // ----------------------
    // SFX pool (overlap + auto recycle)
    // ----------------------

    private readonly Stack<AudioSource> sfxFree = new();
    private readonly HashSet<AudioSource> sfxInUse = new();
    private readonly Dictionary<AudioSource, Tween> sfxTweens = new();

    private void PlaySfxPooled(AudioClip clip, float volume)
    {
        if (clip == null) return;
        var src = GetSfxSource();
        KillSfxTween(src);

        src.loop = false;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.Play();

        // 用 realtime，保证 TimeScale=0 也能回收
        StartCoroutine(RecycleSfxAfter(src, clip.length));
    }

    private AudioSource GetSfxSource()
    {
        if (sfxFree.Count > 0)
        {
            var src = sfxFree.Pop();
            if (src != null)
            {
                sfxInUse.Add(src);
                return src;
            }
        }

        var go = new GameObject($"SFX_{sfxInUse.Count + sfxFree.Count}");
        go.transform.SetParent(sfxPoolRoot, false);
        var created = go.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.loop = false;
        sfxInUse.Add(created);
        return created;
    }

    private IEnumerator RecycleSfxAfter(AudioSource src, float clipSeconds)
    {
        if (src == null) yield break;
        // 最小等待，避免 0 长度导致立即回收
        float wait = Mathf.Max(0.01f, clipSeconds);
        yield return new WaitForSecondsRealtime(wait);

        // 若还在播（例如 pitch/时间被改变），再等到真正结束
        while (src != null && src.isPlaying)
            yield return null;

        ReturnSfxSource(src);
    }

    private void ReturnSfxSource(AudioSource src)
    {
        if (src == null) return;
        if (!sfxInUse.Remove(src)) return;

        KillSfxTween(src);
        src.Stop();
        src.clip = null;
        src.volume = 1f;
        sfxFree.Push(src);
    }

    private void KillSfxTween(AudioSource src)
    {
        if (src == null) return;
        if (sfxTweens.TryGetValue(src, out var tw) && tw != null)
            tw.Kill();
        sfxTweens.Remove(src);
    }

    // ----------------------
    // BGM buffer + timeline hook
    // ----------------------

    private void EnqueueBgm(AudioEntrySO entry, float fadeInSecondsOverride)
    {
        if (entry == null) return;

        // Background：单独处理（独立轨道；StopAll 不停）
        if (entry.isBackground)
        {
            pendingBackground = entry;
            pendingBackgroundFadeIn = fadeInSecondsOverride;
            return;
        }

        if (entry.useReplaceLogic)
        {
            // 同一 track 同一小节内多次请求：只保留最后一次
            pendingReplaceByTrack[entry.track] = entry;
            if (fadeInSecondsOverride > 0f) pendingReplaceFadeInByTrack[entry.track] = fadeInSecondsOverride;
            else pendingReplaceFadeInByTrack.Remove(entry.track);
        }
        else
        {
            // 默认：并行叠加（忽略 track）
            pendingParallel.Add(entry);
            pendingParallelFadeIn.Add(fadeInSecondsOverride);
        }
    }

    private void HandleBarBoundary()
    {
        if (timeline == null) return;

        // 1) Background：默认直接替换（不叠加）。若勾选 useReplaceLogic，则走排挤 cross-fade。
        if (pendingBackground != null && pendingBackground.clip != null)
        {
            var bgEntry = pendingBackground;
            float fadeOverride = pendingBackgroundFadeIn;

            if (bgEntry.useReplaceLogic)
                timeline.PlayBgmOnTrack(BgmTrack.Background, bgEntry.clip, bgEntry.volume, fadeOverride);
            else
                timeline.ReplaceBackgroundNoOverlap(bgEntry, fadeOverride);

            pendingBackground = null;
            pendingBackgroundFadeIn = -1f;
        }

        // 2) useReplaceLogic == true：按 track 排挤
        foreach (var kv in pendingReplaceByTrack)
        {
            var track = kv.Key;
            var entry = kv.Value;
            if (entry == null || entry.clip == null) continue;

            if (pendingReplaceFadeInByTrack.TryGetValue(track, out var fadeOverride) && fadeOverride > 0f)
                timeline.PlayBgmOnTrack(entry, fadeOverride);
            else
                timeline.PlayBgmOnTrack(entry);
        }

        pendingReplaceByTrack.Clear();
        pendingReplaceFadeInByTrack.Clear();

        // 3) useReplaceLogic == false & 非Background：并行叠加
        for (int i = 0; i < pendingParallel.Count; i++)
        {
            var entry = pendingParallel[i];
            if (entry == null || entry.clip == null) continue;

            float fadeOverride = pendingParallelFadeIn[i];
            PlayParallelInternal(entry, fadeOverride);
        }
        pendingParallel.Clear();
        pendingParallelFadeIn.Clear();
    }

    // ----------------------
    // Parallel pool (non-background, non-replace)
    // ----------------------

    private readonly List<AudioSource> parallelSources = new();
    private readonly Dictionary<AudioSource, Tween> parallelTweens = new();

    private void PlayParallelInternal(AudioEntrySO entry, float fadeInSecondsOverride)
    {
        if (entry == null || entry.clip == null) return;

        float barRemain = timeline != null ? timeline.GetTimeToNextBar() : GameSetting.BarSeconds;
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeInSecondsOverride > 0f) ? Mathf.Min(fadeInSecondsOverride, barRemain) : barRemain;

        var src = CreateParallelSource();
        KillParallelTween(src);

        src.loop = true;
        src.clip = entry.clip;
        src.volume = 0f;

        float clipLen = entry.clip.length;
        if (clipLen > 0.001f && timeline != null)
        {
            float offset = Mathf.Repeat(timeline.TimelineTime, clipLen);
            src.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clipLen - 0.001f));
        }

        src.Play();
        var tw = src.DOFade(Mathf.Clamp01(entry.volume), Mathf.Max(0.001f, fadeSeconds)).SetUpdate(true);
        parallelTweens[src] = tw;
    }

    private AudioSource CreateParallelSource()
    {
        // Jam 取舍：并行叠加意味着几乎不会“空闲”，因此直接增长池即可
        var go = new GameObject($"BGM_Parallel_{parallelSources.Count}");
        go.transform.SetParent(bgmPoolRoot, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.volume = 0f;
        parallelSources.Add(src);
        return src;
    }

    private void StopParallelByClip(AudioClip clip, float fadeOutSecondsOverride)
    {
        if (clip == null) return;

        float barRemain = timeline != null ? timeline.GetTimeToNextBar() : GameSetting.BarSeconds;
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeOutSecondsOverride > 0f) ? Mathf.Min(fadeOutSecondsOverride, barRemain) : barRemain;

        foreach (var src in parallelSources)
        {
            if (src == null || !src.isPlaying) continue;
            if (src.clip != clip) continue;
            FadeOutStopParallel(src, fadeSeconds);
        }
    }

    private void StopAllParallel(float fadeOutSecondsOverride)
    {
        float barRemain = timeline != null ? timeline.GetTimeToNextBar() : GameSetting.BarSeconds;
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeOutSecondsOverride > 0f) ? Mathf.Min(fadeOutSecondsOverride, barRemain) : barRemain;

        foreach (var src in parallelSources)
        {
            if (src == null || !src.isPlaying) continue;
            FadeOutStopParallel(src, fadeSeconds);
        }
    }

    private void FadeOutStopParallel(AudioSource src, float seconds)
    {
        if (src == null) return;
        KillParallelTween(src);
        var tw = src.DOFade(0f, Mathf.Max(0.001f, seconds)).SetUpdate(true);
        parallelTweens[src] = tw;
        tw.OnComplete(() =>
        {
            if (src == null) return;
            src.Stop();
            src.clip = null;
            src.volume = 0f;
        });
    }

    private void KillParallelTween(AudioSource src)
    {
        if (src == null) return;
        if (parallelTweens.TryGetValue(src, out var tw) && tw != null)
        {
            tw.Kill();
        }
        parallelTweens.Remove(src);
    }

    private bool TryGetEntry(string key, out AudioEntrySO entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogError("[Audio] key is null/empty");
            return false;
        }

        if (entries.TryGetValue(key, out entry) && entry != null)
            return true;

        Debug.LogError($"[Audio] key not found: '{key}'");
        return false;
    }

    // ----------------------
    // Odin “试音”按钮（做法A）
    // ----------------------

    [FoldoutGroup("Audio Tester"), ShowInInspector, ReadOnly]
    public float DebugTimelineTime => timeline != null ? timeline.TimelineTime : 0f;

    [FoldoutGroup("Audio Tester"), ShowInInspector, ReadOnly]
    public int DebugBarIndex => timeline != null ? timeline.BarIndex : -1;

    [FoldoutGroup("Audio Tester"), ShowInInspector, ReadOnly]
    public float DebugTimeToNextBar => timeline != null ? timeline.GetTimeToNextBar() : 0f;

    [FoldoutGroup("Audio Tester"), Button("Play(BGM)")]
    public void Tester_PlayBgm()
    {
        Play(testerKey);
    }

    [FoldoutGroup("Audio Tester"), Button("Stop(BGM)")]
    public void Tester_StopBgm()
    {
        Stop(testerKey);
    }

    [FoldoutGroup("Audio Tester"), Button("PlayOnce(SFX)")]
    public void Tester_PlaySfxOnce()
    {
        PlaySfxOnce(testerKey, testerSfxVolumeMul);
    }
}
