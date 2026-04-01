using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cycles game speed through x1 → x2 → x4 → x8 on each click.
/// Attach to a Button GameObject; updates its own Text child to show current speed.
/// Resets to x1 on game start and game over.
/// </summary>
[RequireComponent(typeof(Button))]
public class GameSpeedButton : MonoBehaviour
{
    private static readonly float[] Speeds = { 1f, 2f, 4f, 8f };

    private int _currentIndex;
    private Text _label;

    private void Awake()
    {
        _label = GetComponentInChildren<Text>();
        GetComponent<Button>().onClick.AddListener(CycleSpeed);
    }

    private void OnEnable()
    {
        GameEvents.OnGameStart += ResetSpeed;
        GameEvents.OnVictory   += ResetSpeed;
        GameEvents.OnDefeat    += ResetSpeed;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart -= ResetSpeed;
        GameEvents.OnVictory   -= ResetSpeed;
        GameEvents.OnDefeat    -= ResetSpeed;
    }

    private void Start()
    {
        _currentIndex = 0;
        UpdateLabel();
    }

    private void CycleSpeed()
    {
        if (GameLoopManager.Instance == null) return;
        if (GameLoopManager.Instance.CurrentState != GameState.Playing) return;

        _currentIndex = (_currentIndex + 1) % Speeds.Length;
        GameLoopManager.Instance.SetGameSpeed(Speeds[_currentIndex]);
        UpdateLabel();
    }

    private void ResetSpeed()
    {
        _currentIndex = 0;
        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.SetGameSpeed(1f);
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_label != null)
            _label.text = string.Concat("x", Speeds[_currentIndex].ToString("0"));
    }
}
