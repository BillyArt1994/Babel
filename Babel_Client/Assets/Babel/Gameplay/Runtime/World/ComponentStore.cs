using System;
using System.Collections.Generic;
using Babel.Foundation;

namespace Babel.Gameplay.World
{
    /// <summary>
    /// Generation-aware component storage owned by one EntityStore. Destroying or clearing
    /// entities releases their component values immediately.
    /// </summary>
    public sealed class ComponentStore<T> : IDisposable, IEntityStoreObserver
    {
        private readonly EntityStore _entities;
        private readonly List<T> _values = new List<T>();
        private readonly List<uint> _generations = new List<uint>();
        private readonly List<bool> _occupied = new List<bool>();
        private int _count;
        private bool _isDisposed;

        public ComponentStore(EntityStore entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _entities.RegisterObserver(this);
        }

        public int Count
        {
            get
            {
                EnsureNotDisposed();
                return _count;
            }
        }

        public bool IsDisposed => _isDisposed;

        /// <summary>Returns false when the live entity already has this component.</summary>
        public bool Add(EntityHandle entity, T component)
        {
            EnsureNotDisposed();
            EnsureAlive(entity);
            EnsureSlot(entity.Index);

            if (HasComponent(entity)) return false;
            ClearSlot(entity.Index);
            _values[entity.Index] = component;
            _generations[entity.Index] = entity.Generation;
            _occupied[entity.Index] = true;
            _count++;
            return true;
        }

        /// <summary>Adds or replaces a component on a live entity.</summary>
        public void Set(EntityHandle entity, T component)
        {
            EnsureNotDisposed();
            EnsureAlive(entity);
            EnsureSlot(entity.Index);

            if (!HasComponent(entity))
            {
                ClearSlot(entity.Index);
                _count++;
            }

            _values[entity.Index] = component;
            _generations[entity.Index] = entity.Generation;
            _occupied[entity.Index] = true;
        }

        public bool TryGet(EntityHandle entity, out T component)
        {
            EnsureNotDisposed();
            if (!_entities.IsAlive(entity) || !HasComponent(entity))
            {
                component = default;
                return false;
            }

            component = _values[entity.Index];
            return true;
        }

        public bool Remove(EntityHandle entity)
        {
            EnsureNotDisposed();
            if (!_entities.IsAlive(entity) || !HasComponent(entity)) return false;

            ClearSlot(entity.Index);
            _count--;
            return true;
        }

        public void Clear()
        {
            EnsureNotDisposed();
            ClearCore();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _entities.UnregisterObserver(this);
            ClearCore();
            _isDisposed = true;
        }

        void IEntityStoreObserver.OnEntityDestroyed(int index)
        {
            if (_isDisposed || index < 0 || index >= _occupied.Count || !_occupied[index]) return;
            ClearSlot(index);
            _count--;
        }

        void IEntityStoreObserver.OnEntitiesCleared()
        {
            if (!_isDisposed) ClearCore();
        }

        void IEntityStoreObserver.OnEntityStoreDisposed()
        {
            if (_isDisposed) return;
            ClearCore();
            _isDisposed = true;
        }

        private bool HasComponent(EntityHandle entity)
        {
            return entity.Index >= 0 &&
                   entity.Index < _occupied.Count &&
                   _occupied[entity.Index] &&
                   _generations[entity.Index] == entity.Generation;
        }

        private void EnsureAlive(EntityHandle entity)
        {
            if (!_entities.IsAlive(entity))
                throw new ArgumentException("Entity handle is invalid, dead, or stale.", nameof(entity));
        }

        private void EnsureSlot(int index)
        {
            while (_values.Count <= index)
            {
                _values.Add(default);
                _generations.Add(0u);
                _occupied.Add(false);
            }
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= _occupied.Count) return;
            _values[index] = default;
            _generations[index] = 0u;
            _occupied[index] = false;
        }

        private void ClearCore()
        {
            for (int i = 0; i < _values.Count; i++) _values[i] = default;
            _values.Clear();
            _generations.Clear();
            _occupied.Clear();
            _count = 0;
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(typeof(ComponentStore<T>).Name);
        }
    }
}
