using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "Babel/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    [SerializeField] private EnemyData[] _allEnemies;

    public EnemyData[] AllEnemies => _allEnemies;

    public EnemyData GetByType(EnemyType type)
    {
        if (_allEnemies == null)
        {
            return null;
        }

        for (int i = 0; i < _allEnemies.Length; i++)
        {
            EnemyData enemyData = _allEnemies[i];
            if (enemyData != null && enemyData.EnemyType == type)
            {
                return enemyData;
            }
        }

        return null;
    }

    /// <summary>
    /// Fills <paramref name="results"/> with all spawnable enemies at the given game time.
    /// The caller owns the list and should Clear() it before passing.
    /// </summary>
    public void GetSpawnableAtTime(float gameTime, List<EnemyData> results)
    {
        if (_allEnemies == null)
            return;

        for (int i = 0; i < _allEnemies.Length; i++)
        {
            EnemyData enemyData = _allEnemies[i];
            if (enemyData == null)
                continue;

            if (enemyData.SpawnStartTime <= gameTime && enemyData.SpawnWeight > 0f)
                results.Add(enemyData);
        }
    }
}
