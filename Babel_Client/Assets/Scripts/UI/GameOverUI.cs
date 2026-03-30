using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Implements the end-game screen from design/gdd/结算UI.md.
/// Shows victory/defeat text, kill count, and restart/menu buttons.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _subtitleText;
    [SerializeField] private Text _killCountText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _menuButton;

    private const string VICTORY_TITLE    = "天神的震怒平息，巴别塔化为尘埃";
    private const string VICTORY_SUBTITLE = "人类的僭越之心，终被镇压于历史长河";
    private const string DEFEAT_TITLE     = "通天塔已建成——人类抵达了天庭";
    private const string DEFEAT_SUBTITLE  = "审判日已降临，神的宝座第一次被动摇";

    private bool _buttonUsed;
    private int _killCount;

    private void OnEnable()
    {
        GameEvents.OnVictory += OnVictory;
        GameEvents.OnDefeat  += OnDefeat;
        EnemyEvents.OnEnemyDied += TrackKill;
        GameEvents.OnGameStart  += OnGameStart;
    }

    private void OnDisable()
    {
        GameEvents.OnVictory -= OnVictory;
        GameEvents.OnDefeat  -= OnDefeat;
        EnemyEvents.OnEnemyDied -= TrackKill;
        GameEvents.OnGameStart  -= OnGameStart;
    }

    private void Start()
    {
        Hide();

        if (_restartButton != null)
            _restartButton.onClick.AddListener(OnRestartClicked);

        if (_menuButton != null)
            _menuButton.onClick.AddListener(OnMenuClicked);
    }

    private void OnGameStart()
    {
        _killCount = 0;
        _buttonUsed = false;
        Hide();
    }

    private void TrackKill(EnemyData data, Vector2 pos) => _killCount++;

    private void OnVictory() => ShowScreen(VICTORY_TITLE, VICTORY_SUBTITLE);
    private void OnDefeat()  => ShowScreen(DEFEAT_TITLE,  DEFEAT_SUBTITLE);

    private void ShowScreen(string title, string subtitle)
    {
        if (_titleText != null)    _titleText.text    = title;
        if (_subtitleText != null) _subtitleText.text = subtitle;
        if (_killCountText != null)
            _killCountText.text = $"消灭了 {_killCount} 名人类";

        _root.SetActive(true);
    }

    private void Hide() => _root.SetActive(false);

    private void OnRestartClicked()
    {
        if (_buttonUsed) return;
        _buttonUsed = true;
        Hide();
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.RestartGame();
    }

    private void OnMenuClicked()
    {
        if (_buttonUsed) return;
        _buttonUsed = true;
        Hide();
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.ReturnToMainMenu();
    }
}
