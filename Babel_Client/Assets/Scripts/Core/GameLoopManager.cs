using UnityEngine;

/// <summary>
/// Implements the gameplay loop state machine from design/gdd/游戏循环管理器.md.
/// Responsibilities:
/// - Manage the core game session lifecycle
/// - Track the 15-minute countdown
/// - Handle pause / resume / level-up flow
/// - Resolve victory and defeat conditions
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameLoopManager : MonoBehaviour
{
    private const float TOTAL_DURATION = 900f;
    private const float PAUSE_COOLDOWN = 0.2f;

    public static GameLoopManager Instance { get; private set; }

    [SerializeField] private GameState _currentState = GameState.NotStarted;
    [SerializeField] private float _remainingTime = TOTAL_DURATION;
    [SerializeField] private float _pauseCooldown;

    public GameState CurrentState => _currentState;
    public float RemainingTime => _remainingTime;
    public float PauseCooldown => _pauseCooldown;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _currentState = GameState.NotStarted;
        _remainingTime = TOTAL_DURATION;
        _pauseCooldown = 0f;
        Time.timeScale = 1f;

        TowerEvents.OnTowerCompleted += OnTowerCompleted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        TowerEvents.OnTowerCompleted -= OnTowerCompleted;
    }

    private void Update()
    {
        if (_pauseCooldown > 0f)
        {
            _pauseCooldown -= Time.unscaledDeltaTime;
            if (_pauseCooldown < 0f)
            {
                _pauseCooldown = 0f;
            }
        }

        HandlePauseInput();

        if (_currentState != GameState.Playing)
        {
            return;
        }

        _remainingTime -= Time.deltaTime;
        if (_remainingTime < 0f)
        {
            _remainingTime = 0f;
        }

        if (_currentState == GameState.Playing && Mathf.Approximately(_remainingTime, 0f))
        {
            TransitionTo(GameState.Victory);
        }
    }

    public void StartGame()
    {
        if (_currentState != GameState.NotStarted)
        {
            return;
        }

        _remainingTime = TOTAL_DURATION;
        TransitionTo(GameState.Playing);
    }

    public void RestartGame()
    {
        if (_currentState != GameState.Victory && _currentState != GameState.Defeat)
        {
            return;
        }

        _remainingTime = TOTAL_DURATION;
        TransitionTo(GameState.Playing);
    }

    public void ReturnToMainMenu()
    {
        _remainingTime = TOTAL_DURATION;
        _pauseCooldown = 0f;
        TransitionTo(GameState.NotStarted);
    }

    public void RequestLevelUp()
    {
        if (_currentState != GameState.Playing)
        {
            return;
        }

        TransitionTo(GameState.LevelingUp);
    }

    public void RequestLevelUpComplete()
    {
        if (_currentState != GameState.LevelingUp)
        {
            return;
        }

        TransitionTo(GameState.Playing);
    }

    public float GetRemainingTime()
    {
        return _remainingTime;
    }

    public float GetElapsedTime()
    {
        return Mathf.Clamp(TOTAL_DURATION - _remainingTime, 0f, TOTAL_DURATION);
    }

    public float GetGameProgress()
    {
        return Mathf.Clamp01(GetElapsedTime() / TOTAL_DURATION);
    }

    public GameState GetCurrentState()
    {
        return _currentState;
    }

    public bool IsPlaying()
    {
        return _currentState == GameState.Playing;
    }

    public bool IsPaused()
    {
        return _currentState == GameState.Paused;
    }

    public bool IsGameOver()
    {
        return _currentState == GameState.Victory || _currentState == GameState.Defeat;
    }

    private void HandlePauseInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (IsGameOver() || _currentState == GameState.LevelingUp || _pauseCooldown > 0f)
        {
            return;
        }

        if (_currentState == GameState.Playing)
        {
            TransitionTo(GameState.Paused);
        }
        else if (_currentState == GameState.Paused)
        {
            TransitionTo(GameState.Playing);
        }
    }

    private void TransitionTo(GameState next)
    {
        GameState previous = _currentState;
        _currentState = next;

        switch (next)
        {
            case GameState.Playing:
                Time.timeScale = 1f;

                if (previous == GameState.Paused)
                {
                    _pauseCooldown = PAUSE_COOLDOWN;
                    GameEvents.RaiseGameResumed();
                }
                else if (previous == GameState.LevelingUp)
                {
                    GameEvents.RaiseLevelUpComplete();
                }
                else if (previous == GameState.NotStarted || previous == GameState.Victory || previous == GameState.Defeat)
                {
                    GameEvents.RaiseGameStart();
                }
                break;

            case GameState.Paused:
                Time.timeScale = 0f;
                _pauseCooldown = PAUSE_COOLDOWN;
                GameEvents.RaiseGamePaused();
                break;

            case GameState.LevelingUp:
                Time.timeScale = 0f;
                GameEvents.RaiseLevelUpStart();
                break;

            case GameState.Victory:
                Time.timeScale = 0f;
                GameEvents.RaiseVictory();
                break;

            case GameState.Defeat:
                Time.timeScale = 0f;
                GameEvents.RaiseDefeat();
                break;

            case GameState.NotStarted:
                Time.timeScale = 1f;
                break;
        }
    }

    private void OnTowerCompleted()
    {
        if (_currentState != GameState.Playing)
        {
            return;
        }

        TransitionTo(GameState.Defeat);
    }
}
