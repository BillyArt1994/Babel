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
    [System.Obsolete("Use WaveSpawnSystem with level_waves.csv instead.")]
    public void GetSpawnableAtTime(float gameTime, List<EnemyData> results)
    {
        // Legacy method — kept for backward compatibility but no longer used.
        // EnemyData no longer has SpawnStartTime/SpawnWeight fields.
    }
}
