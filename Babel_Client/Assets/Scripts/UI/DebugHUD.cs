using UnityEngine;
using UnityEngine.UI;

public class DebugHUD : MonoBehaviour
{
    private const int TOTAL_LAYERS = 10;

    [SerializeField] private Text _timerText;
    [SerializeField] private Text _towerProgressText;
    [SerializeField] private Text _currentLayerText;
    [SerializeField] private Text _gameStateText;
    [SerializeField] private Text _killCountText;
    [SerializeField] private TowerConstructionSystem _towerSystem;

    private int _killCount;

    private void OnEnable()
    {
        EnemyEvents.OnEnemyDied += OnEnemyDied;
        GameEvents.OnGameStart += OnGameStart;
    }

    private void OnDisable()
    {
        EnemyEvents.OnEnemyDied -= OnEnemyDied;
        GameEvents.OnGameStart -= OnGameStart;
    }

    private void Update()
    {
        RefreshTimerText();
        RefreshTowerProgressText();
        RefreshCurrentLayerText();
        RefreshGameStateText();
        RefreshKillCountText();
    }

    private void OnEnemyDied(EnemyData data, Vector2 position)
    {
        _killCount++;
    }

    private void OnGameStart()
    {
        _killCount = 0;
    }

    private void RefreshTimerText()
    {
        if (_timerText == null)
        {
            return;
        }

        if (GameLoopManager.Instance == null)
        {
            _timerText.text = "--:--";
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(GameLoopManager.Instance.GetRemainingTime()));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void RefreshTowerProgressText()
    {
        if (_towerProgressText == null)
        {
            return;
        }

        if (_towerSystem == null)
        {
            _towerProgressText.text = "0.0% (Layer 1/10)";
            return;
        }

        int layerNumber = Mathf.Clamp(_towerSystem.GetCurrentLayer() + 1, 1, TOTAL_LAYERS);
        _towerProgressText.text = $"{_towerSystem.GetTotalCompletionPercent():F1}% (Layer {layerNumber}/{TOTAL_LAYERS})";
    }

    private void RefreshCurrentLayerText()
    {
        if (_currentLayerText == null)
        {
            return;
        }

        if (_towerSystem == null)
        {
            _currentLayerText.text = "Layer 1/10";
            return;
        }

        int layerNumber = Mathf.Clamp(_towerSystem.GetCurrentLayer() + 1, 1, TOTAL_LAYERS);
        _currentLayerText.text = $"Layer {layerNumber}/{TOTAL_LAYERS}";
    }

    private void RefreshGameStateText()
    {
        if (_gameStateText == null)
        {
            return;
        }

        _gameStateText.text = GameLoopManager.Instance == null
            ? GameState.NotStarted.ToString()
            : GameLoopManager.Instance.GetCurrentState().ToString();
    }

    private void RefreshKillCountText()
    {
        if (_killCountText == null)
        {
            return;
        }

        _killCountText.text = _killCount.ToString();
    }
}
