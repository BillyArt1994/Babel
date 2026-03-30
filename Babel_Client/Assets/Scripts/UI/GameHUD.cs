using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player-facing HUD from design/gdd/游戏HUD.md.
/// Shows: countdown timer, kill count, active skill name, cooldown bar, faith progress bar.
/// Replaces DebugHUD for production use.
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const float WARNING_THRESHOLD = 60f;
    private static readonly Color NormalTimeColor  = Color.white;
    private static readonly Color WarningTimeColor = new Color(1f, 0.25f, 0.25f);

    [Header("Timer")]
    [SerializeField] private Text  _timerText;

    [Header("Kill Count")]
    [SerializeField] private Text  _killCountText;

    [Header("Skill")]
    [SerializeField] private Text  _skillNameText;
    [SerializeField] private Image _cooldownBar;      // fillAmount driven

    [Header("Faith")]
    [SerializeField] private Image _faithBar;         // fillAmount driven
    [SerializeField] private Text  _faithLevelText;   // "Lv N"

    [Header("Root (hidden during pause/gameover)")]
    [SerializeField] private GameObject _root;

    private int _killCount;

    private void OnEnable()
    {
        EnemyEvents.OnEnemyDied += OnEnemyDied;
        GameEvents.OnGameStart  += OnGameStart;
        GameEvents.OnGamePaused += OnGamePaused;
        GameEvents.OnGameResumed += OnGameResumed;
        GameEvents.OnVictory    += OnGameEnded;
        GameEvents.OnDefeat     += OnGameEnded;
    }

    private void OnDisable()
    {
        EnemyEvents.OnEnemyDied -= OnEnemyDied;
        GameEvents.OnGameStart  -= OnGameStart;
        GameEvents.OnGamePaused -= OnGamePaused;
        GameEvents.OnGameResumed -= OnGameResumed;
        GameEvents.OnVictory    -= OnGameEnded;
        GameEvents.OnDefeat     -= OnGameEnded;
    }

    private void Start()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Update()
    {
        RefreshTimer();
        RefreshKillCount();
        RefreshSkill();
        RefreshFaith();
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnGameStart()
    {
        _killCount = 0;
        if (_root != null) _root.SetActive(true);
    }

    private void OnGamePaused()  { if (_root != null) _root.SetActive(false); }
    private void OnGameResumed() { if (_root != null) _root.SetActive(true);  }
    private void OnGameEnded()   { if (_root != null) _root.SetActive(false); }

    private void OnEnemyDied(EnemyData data, Vector2 pos) => _killCount++;

    // ── Refresh helpers ───────────────────────────────────────────────────────

    private void RefreshTimer()
    {
        if (_timerText == null) return;

        float remaining = GameLoopManager.Instance != null
            ? Mathf.Max(0f, GameLoopManager.Instance.GetRemainingTime())
            : 0f;

        int total   = Mathf.CeilToInt(remaining);
        int minutes = total / 60;
        int seconds = total % 60;
        _timerText.text  = $"{minutes:00}:{seconds:00}";
        _timerText.color = remaining <= WARNING_THRESHOLD ? WarningTimeColor : NormalTimeColor;
    }

    private void RefreshKillCount()
    {
        if (_killCountText == null) return;
        _killCountText.text = _killCount > 99999 ? "99999+" : _killCount.ToString();
    }

    private void RefreshSkill()
    {
        if (SkillSystem.Instance == null) return;

        SkillData active = SkillSystem.Instance.GetActiveClickForm();

        if (_skillNameText != null)
            _skillNameText.text = active != null ? active.SkillName : "--";

        if (_cooldownBar != null)
            _cooldownBar.fillAmount = 1f - SkillSystem.Instance.GetCooldownProgress();
    }

    private void RefreshFaith()
    {
        if (UpgradeSystem.Instance == null) return;

        if (_faithBar != null)
            _faithBar.fillAmount = UpgradeSystem.Instance.GetFaithProgress();

        if (_faithLevelText != null)
            _faithLevelText.text = $"Lv {UpgradeSystem.Instance.GetLevelCount()}";
    }
}
