using System;
using UnityEngine;

namespace Babel
{
    public static class EnemyEvents
    {
        public static event Action<Vector2> OnEnemyDied;

        public static void RaiseEnemyDied(Vector2 deathPos)
        {
            OnEnemyDied?.Invoke(deathPos);
        }
    }
}
