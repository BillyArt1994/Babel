using System;
using System.Collections;
using System.Collections.Generic;
using Babel.Unity.Infrastructure.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Babel.Tests
{
    public sealed class GameObjectPoolPlayModeTests
    {
        private readonly List<GameObjectPool> _pools = new List<GameObjectPool>();
        private readonly List<GameObject> _objects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _pools.Count; i++)
                _pools[i]?.Dispose();
            _pools.Clear();

            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.Destroy(_objects[i]);
            }
            _objects.Clear();

            yield return null;
        }

        [Test]
        public void Constructor_PrewarmCreatesConfiguredInactiveInstances()
        {
            GameObject prefab = CreatePrefab();
            GameObjectPool pool = CreatePool(prefab, prewarm: 3, capacity: 3, allowExpansion: false);

            Assert.That(pool.ViewId, Is.EqualTo("test-view"));
            Assert.That(pool.Prefab, Is.SameAs(prefab));
            Assert.That(pool.CountAll, Is.EqualTo(3));
            Assert.That(pool.CountAvailable, Is.EqualTo(3));
            Assert.That(pool.CountInUse, Is.Zero);
        }

        [Test]
        public void GetReturn_ReusesInstanceAndInvokesLifecycleResets()
        {
            GameObjectPool pool = CreatePool(CreatePrefab(), prewarm: 1, capacity: 1, allowExpansion: false);

            GameObject first = pool.Get();
            PoolProbe probe = first.GetComponent<PoolProbe>();

            Assert.That(first.activeSelf, Is.True);
            Assert.That(probe.SpawnResetCount, Is.EqualTo(1));
            Assert.That(probe.DespawnResetCount, Is.Zero);
            Assert.That(pool.CountInUse, Is.EqualTo(1));

            pool.Return(first);

            Assert.That(first.activeSelf, Is.False);
            Assert.That(probe.DespawnResetCount, Is.EqualTo(1));
            Assert.That(pool.CountAvailable, Is.EqualTo(1));

            GameObject second = pool.Get();

            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CountAll, Is.EqualTo(1), "Reuse must not instantiate an additional object.");
            Assert.That(probe.SpawnResetCount, Is.EqualTo(2));
        }

        [Test]
        public void Get_WhenFixedCapacityIsExhausted_ThrowsWithoutInstantiating()
        {
            GameObjectPool pool = CreatePool(CreatePrefab(), prewarm: 0, capacity: 1, allowExpansion: false);
            GameObject first = pool.Get();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => pool.Get());

            Assert.That(error.Message, Does.Contain("test-view"));
            Assert.That(pool.CountAll, Is.EqualTo(1));
            Assert.That(pool.CountInUse, Is.EqualTo(1));
            pool.Return(first);
        }

        [Test]
        public void Get_WhenExpansionIsAllowed_GrowsBeyondConfiguredCapacity()
        {
            GameObjectPool pool = CreatePool(CreatePrefab(), prewarm: 0, capacity: 1, allowExpansion: true);

            GameObject first = pool.Get();
            GameObject second = pool.Get();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(pool.Capacity, Is.EqualTo(1));
            Assert.That(pool.CountAll, Is.EqualTo(2));
            Assert.That(pool.CountInUse, Is.EqualTo(2));
        }

        [Test]
        public void Return_WhenCalledTwice_RejectsDuplicateWithoutCorruptingCounts()
        {
            GameObjectPool pool = CreatePool(CreatePrefab(), prewarm: 1, capacity: 1, allowExpansion: false);
            GameObject instance = pool.Get();
            pool.Return(instance);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => pool.Return(instance));

            Assert.That(error.Message, Does.Contain("already been returned"));
            Assert.That(pool.CountAll, Is.EqualTo(1));
            Assert.That(pool.CountAvailable, Is.EqualTo(1));
            Assert.That(pool.CountInUse, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Dispose_DestroysAvailableAndCheckedOutInstancesAndRejectsFurtherUse()
        {
            GameObject externalParent = Track(new GameObject("External Parent"));
            GameObjectPool pool = CreatePool(CreatePrefab(), prewarm: 2, capacity: 2, allowExpansion: false);
            GameObject checkedOut = pool.Get(externalParent.transform);
            GameObject available = pool.Get();
            pool.Return(available);

            pool.Dispose();

            Assert.That(pool.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => pool.Get());
            Assert.Throws<ObjectDisposedException>(() => pool.Return(checkedOut));

            yield return null;

            Assert.That(checkedOut == null, Is.True, "The checked-out instance must be destroyed.");
            Assert.That(available == null, Is.True, "The available instance must be destroyed.");
        }

        private GameObject CreatePrefab()
        {
            GameObject prefab = Track(new GameObject("Pool Probe Prefab"));
            prefab.AddComponent<PoolProbe>();
            prefab.SetActive(false);
            return prefab;
        }

        private GameObjectPool CreatePool(GameObject prefab, int prewarm, int capacity, bool allowExpansion)
        {
            var pool = new GameObjectPool("test-view", prefab, prewarm, capacity, allowExpansion);
            _pools.Add(pool);
            return pool;
        }

        private GameObject Track(GameObject instance)
        {
            _objects.Add(instance);
            return instance;
        }

        private sealed class PoolProbe : MonoBehaviour, IPooledView
        {
            public int SpawnResetCount { get; private set; }
            public int DespawnResetCount { get; private set; }

            public void ResetForSpawn()
            {
                SpawnResetCount++;
            }

            public void ResetForDespawn()
            {
                DespawnResetCount++;
            }
        }
    }
}
