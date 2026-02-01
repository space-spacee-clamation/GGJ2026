// === UI/EndPass.cs ===
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;

public class EndPass : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI summaryText;

    [SerializeField] private Animator animator;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float textTypeSpeed = 0.05f; // 打字机效果速度

    private void Awake()
    {
        // 初始状态隐藏
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 开始结束流程（动画 -> 生成文本 -> 显示）
    /// </summary>
    public void StartEndingSequence()
    {
        PlayEndingAnimation().Forget();
    }

    private async UniTaskVoid PlayEndingAnimation()
    {
        // 1. 淡入黑色背景/结算界面
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            await canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).ToUniTask();
        }
        animator.Play("End");
        await UniTask.Delay(1000, ignoreTimeScale: true);
        // 2. 生成总结文本
        string finalContent = GetSummaryContent();
 // 3. 显示文本 (使用 maxVisibleCharacters 方案)
        if (summaryText != null)
        {
            // A. 先把完整文本赋值进去
            summaryText.text = finalContent;
            
            // B. 初始设置为 0，完全不可见
            summaryText.maxVisibleCharacters = 0;

            // C. 强制刷新一次 Mesh，这样 TMP 才能算出 textInfo.characterCount (有效字符数)
            // 如果不调用这个，characterCount 可能是旧的或者 0
            summaryText.ForceMeshUpdate();

            // D. 获取除去富文本标签后的 实际字符数量
            int totalVisibleCharacters = summaryText.textInfo.characterCount;

            // E. 循环增加可见字符数
            for (int i = 1; i <= totalVisibleCharacters; i++)
            {
                summaryText.maxVisibleCharacters = i;

                // 可以在这里播放打字音效
                // if (i % 3 == 0) AudioManager.I?.PlaySfxOnce(AudioKey.TypeWriter);

                // 简单的停顿控制：这里使用固定速度
                // 如果需要根据标点停顿，可以检查 summaryText.textInfo.characterInfo[i-1].character
                await UniTask.Delay(System.TimeSpan.FromSeconds(textTypeSpeed), ignoreTimeScale: true);
            }
        }
        await UniTask.Delay(3000, ignoreTimeScale: true);
        OnRestartClicked();
    }

    /// <summary>
    /// 【核心需求】生成总结内容的函数
    /// </summary>
    /// <returns>格式化后的总结文本</returns>
    public string GetSummaryContent()
    {
        if (Player.I == null || GameManager.I == null) return "数据丢失...";

        var stats = Player.I.ActualStats;
        int rounds = GameManager.I.CurrentRoundIndex;
        // 面具库数量即制作总数 (假设库里存的是历史所有，或者近似值)
        int maskCount = GameManager.I.GetMaskLibrary().Count; 
        if (GameManager.I.GetCurrentMask() != null) maskCount++; // 加上当前手上这一个

        var bestMat = GameManager.I.GetMostUsedMaterialInfo();

        StringBuilder sb = new StringBuilder();

        sb.Append($"<size=120%><b>旅途终焉</b></size>\n\n");
        
        sb.Append($"在这次漫长的游戏中，你一共帮助勇者经受了 <color=yellow>{rounds}</color> 轮生死的考验。\n");
        sb.Append($"你的工坊炉火未熄，共计锻造了 <color=orange>{maskCount}</color> 副蕴含心血的魔法面具。\n\n");

        if (bestMat.count > 0)
        {
            sb.Append($"在你手中，<color=cyan>{bestMat.name}</color> 绽放了最耀眼的光芒，\n");
            sb.Append($"它被你使用了 <color=yellow>{bestMat.count}</color> 次，是你最忠实的素材。\n\n");
        }
        else
        {
            sb.Append("你似乎还在探索素材的奥秘，未曾偏爱某一种材料。\n\n");
        }

        sb.Append("最终，你的勇者获得了：\n");
        sb.Append($"<color=red> 攻击力：{stats.Attack:F1}</color>   ");
        sb.Append($"<color=blue> 防御力：{stats.Defense:F1}</color>\n");
        sb.Append($"<color=green> 速度：{stats.SpeedRate}</color>      ");
        sb.Append($"<color=#FF69B4> 最大生命：{stats.MaxHP:F0}</color>\n");

        // 根据属性生成一个趣味评价（Flavor Text）
        string title = DeterminePlayerTitle(stats);
        sb.Append($"\n他已然成为了一名 <size=110%><color=purple><b>{title}</b></color></size>！");

        return sb.ToString();
    }

    /// <summary>
    /// 根据数值生成趣味称号
    /// </summary>
    private string DeterminePlayerTitle(PlayerStats stats)
    {
        // 简单的逻辑判断
        if (stats.Attack > stats.Defense * 2.5f) return "舍身狂战士";
        if (stats.Defense > stats.Attack * 2.5f) return "不动如山的堡垒";
        if (stats.SpeedRate > 30) return "疾风行者";
        if (stats.CritChance > 0.8f) return "幸运的裁决者";
        if (stats.MaxHP > 500) return "不朽之躯";
        if (stats.Luck >= 80) return "天选之子";
        
        // 均衡型
        if (stats.Attack > 100 && stats.Defense > 100) return "传说中的勇者";
        
        return "初出茅庐的冒险家";
    }

    private void OnRestartClicked()
    {
        // 简单的场景重载
        UnityEngine.SceneManagement.SceneManager.LoadScene("Enter");
    }
}