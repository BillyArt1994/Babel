using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.World;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class WorldStoreTests
    {
        [Test]
        public void DestroyThenCreate_ReusesSlotWithNewGenerationAndRejectsStaleHandle()
        {
            using (var entities = new EntityStore())
            {
                EntityHandle original = entities.Create();

                Assert.That(entities.Destroy(original), Is.True);
                Assert.That(entities.Destroy(original), Is.False);
                Assert.That(entities.IsAlive(original), Is.False);

                EntityHandle replacement = entities.Create();
                Assert.That(replacement.Index, Is.EqualTo(original.Index));
                Assert.That(replacement.Generation, Is.Not.EqualTo(original.Generation));
                Assert.That(entities.IsAlive(replacement), Is.True);
                Assert.That(entities.IsAlive(original), Is.False);
            }
        }

        [Test]
        public void EnumerationAndCopyAliveTo_AreStableInAscendingSlotOrder()
        {
            using (var entities = new EntityStore())
            {
                EntityHandle first = entities.Create();
                EntityHandle removed = entities.Create();
                EntityHandle third = entities.Create();
                entities.Destroy(removed);
                EntityHandle replacement = entities.Create();

                var enumerated = new List<EntityHandle>();
                foreach (EntityHandle entity in entities) enumerated.Add(entity);

                Assert.That(enumerated, Is.EqualTo(new[] { first, replacement, third }));

                var copied = new List<EntityHandle> { new EntityHandle(99, 1) };
                Assert.That(entities.CopyAliveTo(copied), Is.EqualTo(3));
                Assert.That(copied, Is.EqualTo(enumerated));
            }
        }

        [Test]
        public void Enumerator_WhenStoreChanges_ThrowsInsteadOfReturningUnstableResults()
        {
            using (var entities = new EntityStore())
            {
                entities.Create();
                EntityStore.Enumerator enumerator = entities.GetEnumerator();
                Assert.That(enumerator.MoveNext(), Is.True);

                entities.Create();

                Assert.That(() => enumerator.MoveNext(), Throws.InvalidOperationException);
            }
        }

        [Test]
        public void ComponentStore_AddSetTryGetRemove_UsesLiveEntityGeneration()
        {
            using (var entities = new EntityStore())
            using (var components = new ComponentStore<TestComponent>(entities))
            {
                EntityHandle entity = entities.Create();
                var initial = new TestComponent(10);
                var replacement = new TestComponent(20);

                Assert.That(components.Add(entity, initial), Is.True);
                Assert.That(components.Add(entity, replacement), Is.False);
                Assert.That(components.TryGet(entity, out TestComponent found), Is.True);
                Assert.That(found.Value, Is.EqualTo(10));

                components.Set(entity, replacement);
                Assert.That(components.TryGet(entity, out found), Is.True);
                Assert.That(found.Value, Is.EqualTo(20));
                Assert.That(components.Count, Is.EqualTo(1));

                Assert.That(components.Remove(entity), Is.True);
                Assert.That(components.Remove(entity), Is.False);
                Assert.That(components.TryGet(entity, out _), Is.False);
                Assert.That(components.Count, Is.Zero);
            }
        }

        [Test]
        public void Destroy_ClearsComponentsAndStaleHandleCannotAccessReplacementSlot()
        {
            using (var entities = new EntityStore())
            using (var components = new ComponentStore<TestComponent>(entities))
            {
                EntityHandle stale = entities.Create();
                components.Add(stale, new TestComponent(7));

                entities.Destroy(stale);

                Assert.That(components.Count, Is.Zero);
                Assert.That(components.TryGet(stale, out _), Is.False);
                Assert.That(components.Remove(stale), Is.False);
                Assert.That(() => components.Add(stale, new TestComponent(8)), Throws.ArgumentException);
                Assert.That(() => components.Set(stale, new TestComponent(8)), Throws.ArgumentException);

                EntityHandle replacement = entities.Create();
                Assert.That(replacement.Index, Is.EqualTo(stale.Index));
                Assert.That(components.TryGet(replacement, out _), Is.False);
            }
        }

        [Test]
        public void Clear_InvalidatesHandlesClearsComponentsAndReusesLowestSlotFirst()
        {
            using (var entities = new EntityStore())
            using (var components = new ComponentStore<TestComponent>(entities))
            {
                EntityHandle first = entities.Create();
                EntityHandle second = entities.Create();
                components.Add(first, new TestComponent(1));
                components.Add(second, new TestComponent(2));

                entities.Clear();

                Assert.That(entities.AliveCount, Is.Zero);
                Assert.That(entities.IsAlive(first), Is.False);
                Assert.That(entities.IsAlive(second), Is.False);
                Assert.That(components.Count, Is.Zero);

                EntityHandle reused = entities.Create();
                Assert.That(reused.Index, Is.Zero);
                Assert.That(reused.Generation, Is.Not.EqualTo(first.Generation));
                Assert.That(components.TryGet(reused, out _), Is.False);
            }
        }

        [Test]
        public void Dispose_IsIdempotentAndInvalidatesOwnedComponentStores()
        {
            var entities = new EntityStore();
            var components = new ComponentStore<TestComponent>(entities);
            EntityHandle entity = entities.Create();
            components.Add(entity, new TestComponent(1));

            entities.Dispose();
            entities.Dispose();
            components.Dispose();

            Assert.That(entities.IsDisposed, Is.True);
            Assert.That(components.IsDisposed, Is.True);
            Assert.That(() => entities.Create(), Throws.TypeOf<ObjectDisposedException>());
            Assert.That(() => entities.IsAlive(entity), Throws.TypeOf<ObjectDisposedException>());
            Assert.That(() => components.TryGet(entity, out _), Throws.TypeOf<ObjectDisposedException>());
        }

        private sealed class TestComponent
        {
            public TestComponent(int value) => Value = value;
            public int Value { get; }
        }
    }
}
