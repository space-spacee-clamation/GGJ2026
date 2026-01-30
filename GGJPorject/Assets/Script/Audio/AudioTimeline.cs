using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class AudioTimeline : MonoBehaviour
{
    private sealed class TrackRuntime
    {
        public AudioSource a;
        public AudioSource b;
        public AudioSource active;

        // 每个 source 可能有自己的 fade tween
        public Tween aTween;
        public Tween bTween;
    }

    private readonly Dictionary<BgmTrack, TrackRuntime> tracks = new();

    private int barIndex = -1;
    private float timelineTime;

    public float TimelineTime => timelineTime;
    public int BarIndex => barIndex;

    public event Action OnBarBoundary;

    public void Initialize()
    {
        EnsureTracksCreated();
        barIndex = Mathf.FloorToInt(timelineTime / GameSetting.BarSeconds);
    }

    private void Update()
    {
        // Timeline 不受暂停影响
        timelineTime += Time.unscaledDeltaTime;

        int newBarIndex = Mathf.FloorToInt(timelineTime / GameSetting.BarSeconds);
        if (newBarIndex != barIndex)
        {
            barIndex = newBarIndex;
            OnBarBoundary?.Invoke();
        }
    }

    public float GetTimeToNextBar()
    {
        float t = timelineTime % GameSetting.BarSeconds;
        float remain = GameSetting.BarSeconds - t;
        // t 可能非常接近 0，避免返回 BarSeconds 造成 UI 误解
        return Mathf.Clamp(remain, 0f, GameSetting.BarSeconds);
    }

    public void PlayBgmOnTrack(AudioEntrySO entry, float fadeSecondsOverride = -1f)
    {
        if (entry == null || entry.clip == null) return;
        PlayBgmOnTrack(entry.track, entry.clip, entry.volume, fadeSecondsOverride);
    }

    public void PlayBgmOnTrack(BgmTrack track, AudioClip clip, float volume, float fadeSecondsOverride = -1f)
    {
        if (clip == null) return;

        EnsureTracksCreated();

        if (!tracks.TryGetValue(track, out var tr) || tr == null)
            return;

        float barRemain = GetTimeToNextBar();
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeSecondsOverride > 0f) ? Mathf.Min(fadeSecondsOverride, barRemain) : barRemain;

        // 选择即将承载新 clip 的 source
        AudioSource newSource = (tr.active == tr.a) ? tr.b : tr.a;
        AudioSource oldSource = tr.active;

        // 旧的在播：并行 cross-fade
        if (oldSource != null && oldSource.isPlaying && oldSource.clip != null)
        {
            FadeOutAndStop(oldSource, fadeSeconds, tr);
        }

        // 设置新 clip + 相位对齐
        newSource.clip = clip;
        newSource.volume = 0f;

        float clipLen = clip.length;
        if (clipLen > 0.001f)
        {
            float offset = Mathf.Repeat(timelineTime, clipLen);
            // Unity 限制：time 不能超过 length
            newSource.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clipLen - 0.001f));
        }

        newSource.Play();
        FadeTo(newSource, Mathf.Clamp01(volume), fadeSeconds, tr);

        tr.active = newSource;
    }

    public void StopBgmByKey(AudioEntrySO entry, float fadeSecondsOverride = -1f)
    {
        if (entry == null) return;
        StopClipOnTrack(entry.track, entry.clip, fadeSecondsOverride);
    }

    public void StopClipOnTrack(BgmTrack track, AudioClip clip, float fadeSecondsOverride = -1f)
    {
        if (clip == null) return;
        EnsureTracksCreated();

        if (!tracks.TryGetValue(track, out var tr) || tr == null)
            return;

        float barRemain = GetTimeToNextBar();
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeSecondsOverride > 0f) ? Mathf.Min(fadeSecondsOverride, barRemain) : barRemain;

        StopIfPlaying(tr.a, clip, fadeSeconds, tr);
        StopIfPlaying(tr.b, clip, fadeSeconds, tr);
    }

    public void StopAllBgm(float fadeSecondsOverride = -1f)
    {
        StopAllBgm(includeBackground: true, fadeSecondsOverride);
    }

    public void StopAllBgm(bool includeBackground, float fadeSecondsOverride = -1f)
    {
        EnsureTracksCreated();

        float barRemain = GetTimeToNextBar();
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeSecondsOverride > 0f) ? Mathf.Min(fadeSecondsOverride, barRemain) : barRemain;

        foreach (var kv in tracks)
        {
            if (!includeBackground && kv.Key == BgmTrack.Background)
                continue;

            var tr = kv.Value;
            if (tr == null) continue;

            if (tr.a != null && tr.a.isPlaying) FadeOutAndStop(tr.a, fadeSeconds, tr);
            if (tr.b != null && tr.b.isPlaying) FadeOutAndStop(tr.b, fadeSeconds, tr);
            tr.active = null;
        }
    }

    /// <summary>
    /// Background 默认“直接替换”用：不做并行叠加。先硬停轨道内所有 source，再播放新 clip 并淡入。
    /// </summary>
    public void ReplaceBackgroundNoOverlap(AudioEntrySO entry, float fadeInSecondsOverride = -1f)
    {
        if (entry == null || entry.clip == null) return;

        // Background 轨道固定
        EnsureTracksCreated();
        if (!tracks.TryGetValue(BgmTrack.Background, out var tr) || tr == null)
            return;

        // 硬停：避免叠加
        ForceStop(tr.a, tr);
        ForceStop(tr.b, tr);
        tr.active = null;

        // 直接播放并淡入（仍然相位对齐）
        float barRemain = GetTimeToNextBar();
        if (barRemain <= 0.001f) barRemain = GameSetting.BarSeconds;
        float fadeSeconds = (fadeInSecondsOverride > 0f) ? Mathf.Min(fadeInSecondsOverride, barRemain) : barRemain;

        AudioSource src = tr.a;
        src.clip = entry.clip;
        src.volume = 0f;

        float clipLen = entry.clip.length;
        if (clipLen > 0.001f)
        {
            float offset = Mathf.Repeat(timelineTime, clipLen);
            src.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clipLen - 0.001f));
        }

        src.Play();
        FadeTo(src, Mathf.Clamp01(entry.volume), fadeSeconds, tr);
        tr.active = src;
    }

    private void ForceStop(AudioSource src, TrackRuntime tr)
    {
        if (src == null) return;
        KillTweenFor(src, tr);
        src.Stop();
        src.clip = null;
        src.volume = 0f;
    }

    private void StopIfPlaying(AudioSource src, AudioClip clip, float fadeOutSeconds, TrackRuntime tr)
    {
        if (src == null || clip == null) return;
        if (!src.isPlaying) return;
        if (src.clip != clip) return;

        FadeOutAndStop(src, fadeOutSeconds, tr);

        if (tr.active == src)
            tr.active = null;
    }

    private void FadeOutAndStop(AudioSource src, float seconds, TrackRuntime tr)
    {
        if (src == null) return;
        KillTweenFor(src, tr);

        // DOTween 需要在 TimeScale=0 也能跑：SetUpdate(true)
        Tween tw = src.DOFade(0f, Mathf.Max(0.001f, seconds)).SetUpdate(true);
        RegisterTweenFor(src, tw, tr);
        tw.OnComplete(() =>
        {
            if (src == null) return;
            src.Stop();
            src.clip = null;
            src.volume = 0f;
        });
    }

    private void FadeTo(AudioSource src, float targetVolume, float seconds, TrackRuntime tr)
    {
        if (src == null) return;
        KillTweenFor(src, tr);

        Tween tw = src.DOFade(targetVolume, Mathf.Max(0.001f, seconds)).SetUpdate(true);
        RegisterTweenFor(src, tw, tr);
    }

    private void RegisterTweenFor(AudioSource src, Tween tw, TrackRuntime tr)
    {
        if (src == tr.a) tr.aTween = tw;
        else if (src == tr.b) tr.bTween = tw;
    }

    private void KillTweenFor(AudioSource src, TrackRuntime tr)
    {
        if (src == tr.a)
        {
            tr.aTween?.Kill();
            tr.aTween = null;
        }
        else if (src == tr.b)
        {
            tr.bTween?.Kill();
            tr.bTween = null;
        }
        else
        {
            // 理论上不会发生：所有 src 都应来自 a/b
            DOTween.Kill(src);
        }
    }

    private void EnsureTracksCreated()
    {
        // 将所有枚举值作为轨道创建出来（Jam：简单粗暴）
        foreach (BgmTrack t in Enum.GetValues(typeof(BgmTrack)))
        {
            if (tracks.ContainsKey(t)) continue;

            var tr = new TrackRuntime();

            tr.a = CreateTrackSource($"{t}_A");
            tr.b = CreateTrackSource($"{t}_B");
            tr.active = null;

            tracks[t] = tr;
        }
    }

    private AudioSource CreateTrackSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true; // BGM 默认 loop（若以后需要非 loop，可扩展到 SO）
        src.volume = 0f;
        return src;
    }
}


