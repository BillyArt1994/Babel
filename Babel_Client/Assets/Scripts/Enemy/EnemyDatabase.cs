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

    public EnemyData[] GetSpawnableAtTime(float gameTime)
    {
        if (_allEnemies == null || _allEnemies.Length == 0)
        {
            return System.Array.Empty<EnemyData>();
        }

        List<EnemyData> spawnableEnemies = new List<EnemyData>();

        for (int i = 0; i < _allEnemies.Length; i++)
        {
            EnemyData enemyData = _allEnemies[i];
            if (enemyData == null)
            {
                continue;
            }

            if (enemyData.SpawnStartTime <= gameTime && enemyData.SpawnWeight > 0f)
            {
                spawnableEnemies.Add(enemyData);
            }
        }

        return spawnableEnemies.ToArray();
    }
}
