using System;

/// <summary>
/// Placeholder tower events used by the game loop manager, as defined in
/// design/gdd/游戏循环管理器.md.
/// </summary>
public static class TowerEvents
{
    public static event Action OnTowerCompleted;

    public static void RaiseTowerCompleted()
    {
        OnTowerCompleted?.Invoke();
    }
}
