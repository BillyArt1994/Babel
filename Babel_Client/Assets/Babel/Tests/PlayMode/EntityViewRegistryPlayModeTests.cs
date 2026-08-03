using System;
using System.Collections;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Unity.Infrastructure.Pooling;
using Babel.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Babel.Tests
{
    public sealed class EntityViewRegistryPlayModeTests
    {
        private readonly List<EntityViewRegistry> _registries = new List<EntityViewRegistry>();
        private readonly List<GameObjectPool> _pools = new List<GameObjectPool>();
        private readonly List<GameObject> _objects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _registries.Count; i++)
                _registries[i]?.Dispose();
            _registries.Clear();

            for (int i = 0; i < _pools.Count; i++)
                _pools[i]?.Dispose();
            _pools.Clear();

            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null) Object.Destroy(_objects[i]);
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void Bind_ChecksOutViewAndQueriesOnlyExactGeneration()
        {
            EntityViewRegistry registry = CreateRegistry();
            GameObjectPool pool = CreatePool("worker", capacity: 1);
            var current = new EntityHandle(4, 7);
            var stale = new EntityHandle(4, 6);

            GameObject bound = registry.Bind(current, pool);

            Assert.That(bound.activeSelf, Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(pool.CountInUse, Is.EqualTo(1));
            Assert.That(registry.TryGet(current, out GameObject exact), Is.True);
            Assert.That(exact, Is.SameAs(bound));
            Assert.That(registry.TryGet(stale, out _), Is.False);
            Assert.That(registry.TryGetComponent(current, out RegistryProbe probe), Is.True);
            Assert.That(probe.SpawnResetCount, Is.EqualTo(1));
        }

        [Test]
        public void Bind_RejectsDuplicateAndDifferentGenerationForOccupiedIndex()
        {
            EntityViewRegistry registry = CreateRegistry();
            GameObjectPool pool = CreatePool("worker", capacity: 2);
            var current = new EntityHandle(2, 1);
            var replacement = new EntityHandle(2, 2);
            registry.Bind(current, pool);

            Assert.Throws<InvalidOperationException>(() => registry.Bind(current, pool));
            Assert.Throws<InvalidOperationException>(() => registry.Bind(replacement, pool));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(pool.CountAll, Is.EqualTo(1));
            Assert.That(pool.CountInUse, Is.EqualTo(1));
        }

        [Test]
        public void Unbind_StaleHandleCannotReleaseCurrent_ExactHandleReturnsAndReusesView()
        {
            EntityViewRegistry registry = CreateRegistry();
            GameObjectPool pool = CreatePool("worker", capacity: 1);
            var firstGeneration = new EntityHandle(9, 3);
            var secondGeneration = new EntityHandle(9, 4);
            GameObject first = registry.Bind(firstGeneration, pool);

            Assert.That(registry.Unbind(secondGeneration), Is.False);
            Assert.That(registry.TryGet(firstGeneration, out _), Is.True);
            Assert.That(pool.CountInUse, Is.EqualTo(1));

            Assert.That(registry.Unbind(firstGeneration), Is.True);
            Assert.That(first.activeSelf, Is.False);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(pool.CountAvailable, Is.EqualTo(1));

            GameObject reused = registry.Bind(secondGeneration, pool);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(registry.TryGet(firstGeneration, out _), Is.False);
            Assert.That(registry.TryGet(secondGeneration, out _), Is.True);
        }

        [Test]
        public void Clear_ReturnsAllViewsAcrossPoolsAndCanBeCalledAgain()
        {
            EntityViewRegistry registry = CreateRegistry();
            GameObjectPool workerPool = CreatePool("worker", capacity: 1);
            GameObjectPool scoutPool = CreatePool("scout", capacity: 1);
            GameObject worker = registry.Bind(new EntityHandle(1, 1), workerPool);
            GameObject scout = registry.Bind(new EntityHandle(2, 1), scoutPool);

            registry.Clear();

            Assert.That(registry.Count, Is.Zero);
            Assert.That(worker.activeSelf, Is.False);
            Assert.That(scout.activeSelf, Is.False);
            Assert.That(workerPool.CountAvailable, Is.EqualTo(1));
            Assert.That(scoutPool.CountAvailable, Is.EqualTo(1));
            Assert.That(worker.GetComponent<RegistryProbe>().DespawnResetCount, Is.EqualTo(1));
            Assert.That(scout.GetComponent<RegistryProbe>().DespawnResetCount, Is.EqualTo(1));
            Assert.DoesNotThrow(registry.Clear);
        }

        [Test]
        public void InvalidAndDisposedRegistry_RejectUnsafeOperations()
        {
            EntityViewRegistry registry = CreateRegistry();
            GameObjectPool pool = CreatePool("worker", capacity: 1);

            Assert.Throws<ArgumentException>(() => registry.Bind(EntityHandle.Invalid, pool));
            Assert.That(registry.TryGet(EntityHandle.Invalid, out _), Is.False);
            Assert.That(registry.Unbind(EntityHandle.Invalid), Is.False);

            GameObject view = registry.Bind(new EntityHandle(0, 1), pool);
            registry.Dispose();

            Assert.That(registry.IsDisposed, Is.True);
            Assert.That(view.activeSelf, Is.False);
            Assert.That(pool.CountAvailable, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => registry.Bind(new EntityHandle(1, 1), pool));
            Assert.Throws<ObjectDisposedException>(() => registry.TryGet(new EntityHandle(0, 1), out _));
        }

        private EntityViewRegistry CreateRegistry()
        {
            var registry = new EntityViewRegistry();
            _registries.Add(registry);
            return registry;
        }

        private GameObjectPool CreatePool(string viewId, int capacity)
        {
            GameObject prefab = new GameObject($"{viewId} Registry Prefab");
            prefab.AddComponent<RegistryProbe>();
            prefab.SetActive(false);
            _objects.Add(prefab);

            var pool = new GameObjectPool(viewId, prefab, 0, capacity, false);
            _pools.Add(pool);
            return pool;
        }

        private sealed class RegistryProbe : MonoBehaviour, IPooledView
        {
            public int SpawnResetCount { get; private set; }
            public int DespawnResetCount { get; private set; }

            public void ResetForSpawn() => SpawnResetCount++;
            public void ResetForDespawn() => DespawnResetCount++;
        }
    }
}
