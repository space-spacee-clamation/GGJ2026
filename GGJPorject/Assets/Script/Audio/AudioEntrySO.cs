using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "GGJ/Audio/Audio Entry", fileName = "AudioEntry")]
public class AudioEntrySO : ScriptableObject
{
    [Tooltip("必须与 AudioKey 枚举名一致（通过 ToString 调用）。")]
    [ValueDropdown(nameof(GetAudioKeyNames))]
    public string key;

    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("该资源的默认音量（基准音量）。")]
    public float volume = 1f;

    [Tooltip("BGM 使用：声轨。SFX 可忽略/保持默认值。")]
    public BgmTrack track = BgmTrack.Track1;

    [Tooltip("勾选后启用“同轨排挤/cross-fade 替换逻辑”。默认不勾选：非Background并行叠加，Background直接替换。")]
    public bool useReplaceLogic = false;

    [Tooltip("是否为 Background：StopAll 不会停止 Background。Background 使用独立轨道（BgmTrack.Background）。")]
    public bool isBackground = false;

    private static IEnumerable<string> GetAudioKeyNames()
    {
        return System.Enum.GetNames(typeof(AudioKey));
    }
}




