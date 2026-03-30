using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player-facing HUD.
/// Layout:
///   Top-center  — countdown timer
///   Top-left    — pause button (separate GameObject, just needs _root control)
///   Right side  — acquired skill list (dynamic, top-to-bottom)
/// </summary>
public class GameHUD : MonoBehaviour
{
    private const float WARNING_THRESHOLD = 60f;
    private static readonly Color NormalTimeColor  = Color.white;
    private static readonly Color WarningTimeColor = new Color(1f, 0.25f, 0.25f);

    [Header("Root (hidden during pause/gameover)")]
    [SerializeField] private GameObject _root;

    [Header("Top-center: Timer")]
    [SerializeField] private Text _timerText;

    [Header("Bottom-center: Faith bar")]
    [SerializeField] private Image _faithBarFill;   // fillAmount 0→1
    [SerializeField] private Text  _faithLevelText; // "Lv N"

    [Header("Right side: Skill list")]
    [SerializeField] private Transform _skillListContainer;  // VerticalLayoutGroup parent
    [SerializeField] private GameObject _skillEntryPrefab;   // Text prefab for each skill entry

    private readonly List<GameObject> _skillEntries = new List<GameObject>();

    private void OnEnable()
    {
        GameEvents.OnGameStart   += OnGameStart;
        GameEvents.OnGamePaused  += OnGamePaused;
        GameEvents.OnGameResumed += OnGameResumed;
        GameEvents.OnVictory     += OnGameEnded;
        GameEvents.OnDefeat      += OnGameEnded;
        SkillEvents.OnSkillAdded += OnSkillAdded;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart   -= OnGameStart;
        GameEvents.OnGamePaused  -= OnGamePaused;
        GameEvents.OnGameResumed -= OnGameResumed;
        GameEvents.OnVictory     -= OnGameEnded;
        GameEvents.OnDefeat      -= OnGameEnded;
        SkillEvents.OnSkillAdded -= OnSkillAdded;
    }

    private void Start()
    {
        // Always visible — game auto-starts on scene load
    }

    private void Update()
    {
        RefreshTimer();
        RefreshFaith();
    }

    // ── Game events ───────────────────────────────────────────────────────────

    private void OnGameStart()
    {
        ClearSkillList();
    }

    private void OnGamePaused()  { }
    private void OnGameResumed() { }
    private void OnGameEnded()   { }

    // ── Skill list ────────────────────────────────────────────────────────────

    private void OnSkillAdded(SkillData skill)
    {
        if (skill == null || _skillListContainer == null || _skillEntryPrefab == null)
            return;

        // Check if entry already exists (passives can stack — update stack count)
        foreach (GameObject entry in _skillEntries)
        {
            SkillEntryUI entryUI = entry.GetComponent<SkillEntryUI>();
            if (entryUI != null && entryUI.SkillData == skill)
            {
                entryUI.IncrementStack();
                return;
            }
        }

        // New entry
        GameObject go = Instantiate(_skillEntryPrefab, _skillListContainer);
        SkillEntryUI ui = go.GetComponent<SkillEntryUI>();
        if (ui != null)
            ui.SetSkill(skill);

        _skillEntries.Add(go);
    }

    private void ClearSkillList()
    {
        foreach (GameObject entry in _skillEntries)
            if (entry != null) Destroy(entry);
        _skillEntries.Clear();
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

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

    // ── Faith bar ─────────────────────────────────────────────────────────────

    private void RefreshFaith()
    {
        if (UpgradeSystem.Instance == null) return;

        if (_faithBarFill != null)
            _faithBarFill.fillAmount = UpgradeSystem.Instance.GetFaithProgress();

        if (_faithLevelText != null)
            _faithLevelText.text = $"Lv {UpgradeSystem.Instance.GetLevelCount()}";
    }
}
