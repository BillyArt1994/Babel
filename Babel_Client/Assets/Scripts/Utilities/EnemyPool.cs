using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements enemy pool lookup and routing described in design/gdd/对象池系统.md.
/// </summary>
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    private Dictionary<EnemyType, ObjectPool<EnemyController>> _pools;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _pools = new Dictionary<EnemyType, ObjectPool<EnemyController>>();
    }

    public void RegisterPool(EnemyType type, ObjectPool<EnemyController> pool)
    {
        if (pool == null)
        {
            Debug.LogWarning($"{nameof(EnemyPool)} received a null pool registration for type {type}.", this);
            return;
        }

        _pools[type] = pool;
    }

    public EnemyController Get(EnemyData data, Vector2 position)
    {
        if (data == null)
        {
            Debug.LogError($"{nameof(EnemyPool)} cannot Get because EnemyData is null.", this);
            return null;
        }

        if (!_pools.TryGetValue(data.EnemyType, out ObjectPool<EnemyController> pool) || pool == null)
        {
            Debug.LogError($"{nameof(EnemyPool)} has no registered pool for enemy type {data.EnemyType}.", this);
            return null;
        }

        EnemyController enemy = pool.Get(position, Quaternion.identity);
        if (enemy == null)
        {
            return null;
        }

        return enemy;
    }

    public void Return(EnemyController enemy)
    {
        if (enemy == null)
        {
            Debug.LogWarning($"{nameof(EnemyPool)} received a null enemy return.", this);
            return;
        }

        if (!_pools.TryGetValue(enemy.EnemyType, out ObjectPool<EnemyController> pool) || pool == null)
        {
            Debug.LogError($"{nameof(EnemyPool)} has no registered pool for enemy type {enemy.EnemyType}.", enemy);
            return;
        }

        pool.Return(enemy);
    }

    public void ReturnAll()
    {
        foreach (KeyValuePair<EnemyType, ObjectPool<EnemyController>> pair in _pools)
        {
            if (pair.Value != null)
            {
                pair.Value.ReturnAll();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
