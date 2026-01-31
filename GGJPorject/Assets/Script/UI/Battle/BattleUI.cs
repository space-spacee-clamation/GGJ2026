using TMPro;
using UnityEngine;

/// <summary>
/// 战斗阶段 UI：血条/速度条/伤害飘字（纯 UI，不走 WorldSpace）。
/// 依赖：FightManager/FightContext。
/// </summary>
public sealed class BattleUI : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private UIBarFillImage playerHPBar;
    [SerializeField] private UIBarFillImage playerSpeedBar;
    [SerializeField] private UIBarFillImage enemyHPBar;
    [SerializeField] private UIBarFillImage enemySpeedBar;

    [Header("Optional Text")]
    [SerializeField] private TextMeshProUGUI playerHPText;
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI playerSpeedText;
    [SerializeField] private TextMeshProUGUI enemySpeedText;

    [Header("Damage Text")]
    [SerializeField] private UIDamageText damageTextPrefab;
    [SerializeField] private RectTransform damageTextRoot;
    [SerializeField] private RectTransform playerDamageAnchor;
    [SerializeField] private RectTransform enemyDamageAnchor;
    [SerializeField] private Vector2 damageSpawnOffset = new Vector2(0f, 60f);

    private FightContext _bound;

    private void OnEnable()
    {
        TryRebind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        TryRebind();
        RefreshBars();
    }

    private void TryRebind()
    {
        var fm = FightManager.I;
        var ctx = fm == null ? null : fm.Context;
        if (ctx == _bound) return;
        Bind(ctx);
    }

    private void Bind(FightContext ctx)
    {
        Unbind();
        _bound = ctx;
        if (_bound == null) return;
        _bound.OnDamageApplied += OnDamageApplied;
    }

    private void Unbind()
    {
        if (_bound != null)
        {
            _bound.OnDamageApplied -= OnDamageApplied;
        }
        _bound = null;
    }

    private void RefreshBars()
    {
        var c = _bound;
        if (c == null || c.Player == null || c.Enemy == null) return;

        var p = c.Player;
        var e = c.Enemy;
        var threshold = Mathf.Max(1, c.ArenaSpeedThreshold);

        var pHp01 = p.MaxHP <= 0f ? 0f : Mathf.Clamp01(p.CurrentHP / p.MaxHP);
        var eHp01 = e.MaxHP <= 0f ? 0f : Mathf.Clamp01(e.CurrentHP / e.MaxHP);
        var pSp01 = Mathf.Clamp01(c.PlayerSpeedValue / threshold);
        var eSp01 = Mathf.Clamp01(c.EnemySpeedValue / threshold);

        if (playerHPBar != null) playerHPBar.SetFill01(pHp01);
        if (enemyHPBar != null) enemyHPBar.SetFill01(eHp01);
        if (playerSpeedBar != null) playerSpeedBar.SetFill01(pSp01);
        if (enemySpeedBar != null) enemySpeedBar.SetFill01(eSp01);

        if (playerHPText != null) playerHPText.text = $"HP {p.CurrentHP:0}/{p.MaxHP:0}";
        if (enemyHPText != null) enemyHPText.text = $"HP {e.CurrentHP:0}/{e.MaxHP:0}";
        if (playerSpeedText != null) playerSpeedText.text = $"{pSp01:P0}";
        if (enemySpeedText != null) enemySpeedText.text = $"{eSp01:P0}";
    }

    private void OnDamageApplied(FightContext ctx, FightSide attackerSide, FightSide defenderSide, AttackInfo info, float damage)
    {
        if (damageTextPrefab == null || damageTextRoot == null) return;

        var anchor = defenderSide == FightSide.Player ? playerDamageAnchor : enemyDamageAnchor;
        if (anchor == null) return;

        var inst = Instantiate(damageTextPrefab, damageTextRoot);
        var rt = inst.transform as RectTransform;
        if (rt != null)
        {
            // 纯 UI：直接用屏幕/世界坐标对齐（同一 Canvas 下最稳妥）
            rt.position = anchor.position;
            rt.anchoredPosition += damageSpawnOffset;
        }

        inst.Play(damage, info.IsCrit);
    }
}


