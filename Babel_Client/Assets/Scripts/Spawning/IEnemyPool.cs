using UnityEngine;

namespace Babel
{
    public interface IEnemyPool
    {
        GameObject Get(string enemyId, Vector2 position);
        void Return(GameObject enemy);
        int ActiveCount { get; }
    }
}
