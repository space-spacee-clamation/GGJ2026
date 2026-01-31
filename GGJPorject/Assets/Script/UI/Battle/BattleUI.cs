using DG.Tweening;
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

    [SerializeField] private RectTransform playerRect;
    [SerializeField] private RectTransform enemyRect;

    [SerializeField] private Vector2 damageSpawnOffset = new Vector2(0f, 60f);

    [SerializeField] private float attackHitDistance = 50f;

    private FightContext _bound;
    private Vector2 _playerInitialPos;
    private Vector2 _enemyInitialPos;
    private Tween _playerAttackTween;
    private Tween _enemyAttackTween;

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
        _bound.OnBeforePlayerAttack += OnBeforePlayerAttack;
        _bound.OnBeforeEnemyAttack += OnBeforeEnemyAttack;

        // 保存初始位置
        if (playerRect != null) _playerInitialPos = playerRect.anchoredPosition;
        if (enemyRect != null) _enemyInitialPos = enemyRect.anchoredPosition;
    }

    private void Unbind()
    {
        if (_bound != null)
        {
            _bound.OnDamageApplied -= OnDamageApplied;
            _bound.OnBeforePlayerAttack -= OnBeforePlayerAttack;
            _bound.OnBeforeEnemyAttack -= OnBeforeEnemyAttack;
        }
        _bound = null;

        // 停止并清理动画
        _playerAttackTween?.Kill();
        _enemyAttackTween?.Kill();
        _playerAttackTween = null;
        _enemyAttackTween = null;
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

    private void OnBeforePlayerAttack(FightContext ctx, AttackInfo info)
    {
        PlayAttackAnimation(playerRect, enemyDamageAnchor, _playerInitialPos, isPlayer: true);
    }

    private void OnBeforeEnemyAttack(FightContext ctx, AttackInfo info)
    {
        PlayAttackAnimation(enemyRect, playerDamageAnchor, _enemyInitialPos, isPlayer: false);
    }

    private void PlayAttackAnimation(RectTransform attackerRect, RectTransform targetAnchor, Vector2 initialPos, bool isPlayer)
    {
        if (attackerRect == null || targetAnchor == null) return;

        // 停止之前的动画
        if (isPlayer)
        {
            _playerAttackTween?.Kill();
        }
        else
        {
            _enemyAttackTween?.Kill();
        }

        // 计算目标位置：从目标锚点向攻击者方向偏移 AttackHitDistance
        // 使用世界坐标计算方向
        Vector3 targetWorldPos = targetAnchor.position;
        Vector3 attackerWorldPos = attackerRect.position;
        Vector3 direction = (targetWorldPos - attackerWorldPos).normalized;

        // 将世界坐标转换为本地坐标（相对于父节点）
        RectTransform parent = attackerRect.parent as RectTransform;
        if (parent == null) return;

        Vector3 targetLocalPos3D = parent.InverseTransformPoint(targetWorldPos);
        Vector2 targetLocalPos = new Vector2(targetLocalPos3D.x, targetLocalPos3D.y);

        // 将方向向量转换为本地坐标空间
        Vector3 localDirection3D = parent.InverseTransformDirection(direction);
        Vector2 localDirection = new Vector2(localDirection3D.x, localDirection3D.y).normalized;

        // 计算命中位置：从目标位置向攻击者方向偏移
        Vector2 hitLocalPos = targetLocalPos - localDirection * GameSetting.AttackHitDistance;

        // 动画：去程（50% 时间）+ 回程（50% 时间）
        float halfDuration = GameSetting.AttackTweenTotalSeconds * 0.5f;

        var sequence = DOTween.Sequence()
            .SetUpdate(true) // 即使 TimeScale=0 也能播 UI
            .Append(attackerRect.DOAnchorPos(hitLocalPos, halfDuration).SetEase(Ease.OutQuad))
            .Append(attackerRect.DOAnchorPos(initialPos, halfDuration).SetEase(Ease.InQuad));

        // 根据攻击者类型设置对应的 tween 引用
        if (isPlayer)
        {
            _playerAttackTween = sequence;
            sequence.OnComplete(() => _playerAttackTween = null);
        }
        else
        {
            _enemyAttackTween = sequence;
            sequence.OnComplete(() => _enemyAttackTween = null);
        }
    }
}


