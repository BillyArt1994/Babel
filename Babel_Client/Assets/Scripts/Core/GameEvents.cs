using System;

/// <summary>
/// Game-wide loop events defined by design/gdd/游戏循环管理器.md.
/// Static raise methods centralize invocation and keep publishers consistent.
/// </summary>
public static class GameEvents
{
    public static event Action OnGameStart;
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;
    public static event Action OnLevelUpStart;
    public static event Action OnLevelUpComplete;
    public static event Action OnVictory;
    public static event Action OnDefeat;

    public static void RaiseGameStart()
    {
        OnGameStart?.Invoke();
    }

    public static void RaiseGamePaused()
    {
        OnGamePaused?.Invoke();
    }

    public static void RaiseGameResumed()
    {
        OnGameResumed?.Invoke();
    }

    public static void RaiseLevelUpStart()
    {
        OnLevelUpStart?.Invoke();
    }

    public static void RaiseLevelUpComplete()
    {
        OnLevelUpComplete?.Invoke();
    }

    public static void RaiseVictory()
    {
        OnVictory?.Invoke();
    }

    public static void RaiseDefeat()
    {
        OnDefeat?.Invoke();
    }
}
