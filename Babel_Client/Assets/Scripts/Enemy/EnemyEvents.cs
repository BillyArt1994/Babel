using System;
using UnityEngine;

public static class EnemyEvents
{
    public static event Action<EnemyData, Vector2> OnEnemyDied;
    public static event Action<EnemyData> OnEnemyReachedTower;

    public static void RaiseEnemyDied(EnemyData data, Vector2 position) => OnEnemyDied?.Invoke(data, position);
    public static void RaiseEnemyReachedTower(EnemyData data) => OnEnemyReachedTower?.Invoke(data);
}
