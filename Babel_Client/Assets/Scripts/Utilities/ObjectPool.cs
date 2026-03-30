using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the object pool system described in design/gdd/对象池系统.md.
/// Generic component pool using a Stack for reuse and a HashSet to guard double returns.
/// </summary>
public class ObjectPool<T> : MonoBehaviour where T : Component
{
    [SerializeField] private T _prefab;
    [SerializeField] private int _initialSize = 50;
    [SerializeField] private Transform _poolParent;

    private readonly Stack<T> _pool = new Stack<T>();
    private readonly HashSet<T> _activeObjects = new HashSet<T>();
    private int _totalInstantiated = 0;

    public int ActiveCount => _activeObjects.Count;
    public int PooledCount => _pool.Count;

    private void Awake()
    {
        if (_prefab == null)
        {
            Debug.LogError($"{nameof(ObjectPool<T>)} on {name} is missing a prefab reference.", this);
            return;
        }

        for (int i = 0; i < _initialSize; i++)
        {
            T instance = CreateInstance();

            if (instance is IPoolable poolable)
            {
                poolable.OnReturnToPool();
            }

            instance.gameObject.SetActive(false);
            _pool.Push(instance);
        }
    }

    public T Get(Vector2 position, Quaternion rotation)
    {
        if (_prefab == null)
        {
            Debug.LogError($"{nameof(ObjectPool<T>)} on {name} cannot Get because prefab is null.", this);
            return null;
        }

        T instance = _pool.Count > 0 ? _pool.Pop() : CreateInstance();

        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(null);
        instanceTransform.SetPositionAndRotation(position, rotation);

        instance.gameObject.SetActive(true);
        _activeObjects.Add(instance);

        if (instance is IPoolable poolable)
        {
            poolable.OnGetFromPool();
        }

        return instance;
    }

    public void Return(T obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"{nameof(ObjectPool<T>)} on {name} received a null object return.", this);
            return;
        }

        if (!_activeObjects.Contains(obj))
        {
            Debug.LogWarning($"{nameof(ObjectPool<T>)} on {name} detected a double return or foreign object: {obj.name}.", obj);
            return;
        }

        _activeObjects.Remove(obj);

        if (obj is IPoolable poolable)
        {
            poolable.OnReturnToPool();
        }

        obj.transform.SetParent(_poolParent);
        obj.gameObject.SetActive(false);
        _pool.Push(obj);
    }

    public void ReturnAll()
    {
        List<T> activeObjects = new List<T>(_activeObjects);
        for (int i = 0; i < activeObjects.Count; i++)
        {
            Return(activeObjects[i]);
        }
    }

    private T CreateInstance()
    {
        T instance = Instantiate(_prefab, _poolParent);
        _totalInstantiated++;
        return instance;
    }

    private void OnDestroy()
    {
        _pool.Clear();
        _activeObjects.Clear();
    }
}
