using UnityEngine;

public static class GameSetting
{
    /// <summary>
    /// 小节长度（秒）。AudioTimeline 的 bar 对齐与排挤 cross-fade 都基于这个值。
    /// </summary>
    public const float BarSeconds = 1.6f;

    /// <summary>
    /// Resources 下 AudioEntrySO 的加载路径（相对 Resources 根目录）。
    /// </summary>
    public const string AudioEntriesResourcesPath = "AudioEntries";

    /// <summary>
    /// 攻击动画间隔（秒）。速度条机制下的最小攻击间隔不允许低于此值。
    /// </summary>
    public const float AttackAnimIntervalSeconds = 0.25f;

    /// <summary>
    /// 场地速度阈值默认值（整数）。后续如果需要“根据默认值生成关卡速度”，再补逻辑。
    /// </summary>
    public const int DefaultArenaSpeedThreshold = 10;

    // ---- UI：品质描边颜色（白/绿/紫/金/红，低->高）----
    public static readonly Color32 QualityOutline_Common = new(255, 255, 255, 255);
    public static readonly Color32 QualityOutline_Uncommon = new(80, 255, 80, 255);
    public static readonly Color32 QualityOutline_Rare = new(170, 90, 255, 255);
    public static readonly Color32 QualityOutline_Epic = new(255, 210, 60, 255);
    public static readonly Color32 QualityOutline_Legendary = new(255, 80, 80, 255);

    // ---- UI：选中描边颜色----
    public static readonly Color32 SelectedOutline_Green = new(80, 255, 80, 255);
    public static readonly Color32 SelectedOutline_Red = new(255, 80, 80, 255);
}




