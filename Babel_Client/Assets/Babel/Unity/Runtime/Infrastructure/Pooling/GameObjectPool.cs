using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Babel.Unity.Infrastructure.Pooling
{
    /// <summary>
    /// Owns the runtime instances for one view id and one prefab.
    /// This class is intentionally independent from content loading and legacy gameplay types.
    /// </summary>
    public sealed class GameObjectPool : IDisposable
    {
        private sealed class Entry
        {
            public Entry(GameObject instance, IPooledView[] pooledViews)
            {
                Instance = instance;
                PooledViews = pooledViews;
            }

            public GameObject Instance { get; }
            public IPooledView[] PooledViews { get; }
        }

        private readonly GameObject _prefab;
        private readonly Transform _storageRoot;
        private readonly Dictionary<GameObject, Entry> _entries = new Dictionary<GameObject, Entry>();
        private readonly Stack<Entry> _available = new Stack<Entry>();
        private readonly HashSet<GameObject> _availableInstances = new HashSet<GameObject>();
        private bool _disposed;

        public GameObjectPool(
            string viewId,
            GameObject prefab,
            int prewarm,
            int capacity,
            bool allowExpansion,
            Transform poolParent = null)
        {
            if (string.IsNullOrWhiteSpace(viewId))
                throw new ArgumentException("A pool view id is required.", nameof(viewId));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (prewarm < 0)
                throw new ArgumentOutOfRangeException(nameof(prewarm), prewarm, "Prewarm cannot be negative.");
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least one.");
            if (prewarm > capacity)
                throw new ArgumentOutOfRangeException(nameof(prewarm), prewarm, "Prewarm cannot exceed capacity.");

            ViewId = viewId;
            Capacity = capacity;
            AllowExpansion = allowExpansion;
            _prefab = prefab;

            var root = new GameObject($"[Pool] {viewId}");
            _storageRoot = root.transform;
            _storageRoot.SetParent(poolParent, false);

            try
            {
                for (int i = 0; i < prewarm; i++)
                    StoreAvailable(CreateEntry());
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string ViewId { get; }
        public GameObject Prefab => _prefab;
        public int Capacity { get; }
        public bool AllowExpansion { get; }
        public bool IsDisposed => _disposed;
        public int CountAll => _entries.Count;
        public int CountAvailable => _available.Count;
        public int CountInUse => CountAll - CountAvailable;

        /// <summary>
        /// Gets an instance. When parent is omitted it stays below the pool's storage root.
        /// </summary>
        public GameObject Get(Transform parent = null, bool worldPositionStays = false)
        {
            ThrowIfDisposed();

            Entry entry;
            if (_available.Count > 0)
            {
                entry = _available.Pop();
                _availableInstances.Remove(entry.Instance);
            }
            else
            {
                if (!AllowExpansion && CountAll >= Capacity)
                {
                    throw new InvalidOperationException(
                        $"Pool '{ViewId}' is exhausted at its configured capacity of {Capacity}.");
                }

                entry = CreateEntry();
            }

            try
            {
                Transform targetParent = parent != null ? parent : _storageRoot;
                entry.Instance.transform.SetParent(targetParent, worldPositionStays);
                InvokeSpawn(entry);
                entry.Instance.SetActive(true);
                return entry.Instance;
            }
            catch
            {
                if (entry.Instance != null)
                {
                    entry.Instance.SetActive(false);
                    entry.Instance.transform.SetParent(_storageRoot, false);
                    StoreAvailable(entry);
                }

                throw;
            }
        }

        /// <summary>
        /// Returns an owned instance. Foreign and already-returned instances are rejected.
        /// </summary>
        public void Return(GameObject instance)
        {
            ThrowIfDisposed();

            if (ReferenceEquals(instance, null))
                throw new ArgumentNullException(nameof(instance));
            if (instance == null)
                throw new InvalidOperationException($"A destroyed instance cannot be returned to pool '{ViewId}'.");
            if (!_entries.TryGetValue(instance, out Entry entry))
                throw new InvalidOperationException($"GameObject '{instance.name}' is not owned by pool '{ViewId}'.");
            if (_availableInstances.Contains(instance))
                throw new InvalidOperationException($"GameObject '{instance.name}' has already been returned to pool '{ViewId}'.");

            try
            {
                InvokeDespawn(entry);
            }
            finally
            {
                instance.SetActive(false);
                instance.transform.SetParent(_storageRoot, false);
                StoreAvailable(entry);
            }
        }

        public bool Owns(GameObject instance)
        {
            return !_disposed && !ReferenceEquals(instance, null) && _entries.ContainsKey(instance);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GameObject storageObject = _storageRoot != null ? _storageRoot.gameObject : null;
            foreach (Entry entry in _entries.Values)
            {
                GameObject instance = entry.Instance;
                if (instance == null) continue;

                bool ownedByStorageRoot = _storageRoot != null && instance.transform.IsChildOf(_storageRoot);
                if (!ownedByStorageRoot)
                    DestroyObject(instance);
            }

            if (storageObject != null)
                DestroyObject(storageObject);

            _available.Clear();
            _availableInstances.Clear();
            _entries.Clear();
        }

        private Entry CreateEntry()
        {
            GameObject instance = Object.Instantiate(_prefab, _storageRoot, false);
            instance.name = $"{_prefab.name} [{ViewId}]";
            instance.SetActive(false);

            IPooledView[] pooledViews = FindPooledViews(instance);
            var entry = new Entry(instance, pooledViews);
            _entries.Add(instance, entry);
            return entry;
        }

        private void StoreAvailable(Entry entry)
        {
            if (!_availableInstances.Add(entry.Instance))
                throw new InvalidOperationException($"Pool '{ViewId}' attempted to store the same instance twice.");

            _available.Push(entry);
        }

        private static IPooledView[] FindPooledViews(GameObject instance)
        {
            MonoBehaviour[] components = instance.GetComponentsInChildren<MonoBehaviour>(true);
            var pooledViews = new List<IPooledView>(components.Length);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IPooledView pooledView)
                    pooledViews.Add(pooledView);
            }

            return pooledViews.ToArray();
        }

        private static void InvokeSpawn(Entry entry)
        {
            for (int i = 0; i < entry.PooledViews.Length; i++)
                entry.PooledViews[i].ResetForSpawn();
        }

        private static void InvokeDespawn(Entry entry)
        {
            for (int i = entry.PooledViews.Length - 1; i >= 0; i--)
                entry.PooledViews[i].ResetForDespawn();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameObjectPool), $"Pool '{ViewId}' has been disposed.");
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
