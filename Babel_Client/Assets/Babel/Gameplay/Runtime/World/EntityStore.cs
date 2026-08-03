using System;
using System.Collections;
using System.Collections.Generic;
using Babel.Foundation;

namespace Babel.Gameplay.World
{
    internal interface IEntityStoreObserver
    {
        void OnEntityDestroyed(int index);
        void OnEntitiesCleared();
        void OnEntityStoreDisposed();
    }

    /// <summary>
    /// Owns entity identity and lifetime for one run. Slots are recycled, while generations
    /// invalidate handles that refer to an earlier occupant of the same slot.
    /// </summary>
    public sealed class EntityStore : IDisposable, IEnumerable<EntityHandle>
    {
        private readonly List<uint> _generations = new List<uint>();
        private readonly List<bool> _alive = new List<bool>();
        private readonly Stack<int> _freeIndices = new Stack<int>();
        private readonly List<IEntityStoreObserver> _observers = new List<IEntityStoreObserver>();
        private int _aliveCount;
        private int _version;
        private bool _isDisposed;

        public int AliveCount
        {
            get
            {
                EnsureNotDisposed();
                return _aliveCount;
            }
        }

        public int Capacity
        {
            get
            {
                EnsureNotDisposed();
                return _generations.Count;
            }
        }

        public bool IsDisposed => _isDisposed;

        public EntityHandle Create()
        {
            EnsureNotDisposed();

            int index;
            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Pop();
            }
            else
            {
                index = _generations.Count;
                _generations.Add(1u);
                _alive.Add(false);
            }

            _alive[index] = true;
            _aliveCount++;
            _version++;
            return new EntityHandle(index, _generations[index]);
        }

        public bool Destroy(EntityHandle entity)
        {
            EnsureNotDisposed();
            if (!IsAliveCore(entity)) return false;

            int index = entity.Index;
            _alive[index] = false;
            _generations[index] = NextGeneration(_generations[index]);
            _freeIndices.Push(index);
            _aliveCount--;
            _version++;
            NotifyEntityDestroyed(index);
            return true;
        }

        public bool IsAlive(EntityHandle entity)
        {
            EnsureNotDisposed();
            return IsAliveCore(entity);
        }

        /// <summary>
        /// Replaces the destination contents with all live handles in ascending slot order.
        /// </summary>
        public int CopyAliveTo(List<EntityHandle> destination)
        {
            EnsureNotDisposed();
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (destination.Capacity < _aliveCount) destination.Capacity = _aliveCount;
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i]) destination.Add(new EntityHandle(i, _generations[i]));
            }

            return destination.Count;
        }

        /// <summary>
        /// Invalidates every live handle, clears registered component stores and keeps slots
        /// available for deterministic low-index reuse.
        /// </summary>
        public void Clear()
        {
            EnsureNotDisposed();

            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i]) _generations[i] = NextGeneration(_generations[i]);
                _alive[i] = false;
            }

            _freeIndices.Clear();
            for (int i = _alive.Count - 1; i >= 0; i--) _freeIndices.Push(i);
            _aliveCount = 0;
            _version++;
            NotifyEntitiesCleared();
        }

        public Enumerator GetEnumerator()
        {
            EnsureNotDisposed();
            return new Enumerator(this);
        }

        IEnumerator<EntityHandle> IEnumerable<EntityHandle>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _aliveCount = 0;
            _version++;
            _freeIndices.Clear();
            _alive.Clear();
            _generations.Clear();

            for (int i = 0; i < _observers.Count; i++)
                _observers[i].OnEntityStoreDisposed();
            _observers.Clear();
        }

        internal void RegisterObserver(IEntityStoreObserver observer)
        {
            EnsureNotDisposed();
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }

        internal void UnregisterObserver(IEntityStoreObserver observer)
        {
            if (_isDisposed || observer == null) return;
            _observers.Remove(observer);
        }

        private bool IsAliveCore(EntityHandle entity)
        {
            return entity.IsValid &&
                   entity.Index >= 0 &&
                   entity.Index < _alive.Count &&
                   _alive[entity.Index] &&
                   _generations[entity.Index] == entity.Generation;
        }

        private void NotifyEntityDestroyed(int index)
        {
            for (int i = 0; i < _observers.Count; i++)
                _observers[i].OnEntityDestroyed(index);
        }

        private void NotifyEntitiesCleared()
        {
            for (int i = 0; i < _observers.Count; i++)
                _observers[i].OnEntitiesCleared();
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EntityStore));
        }

        private static uint NextGeneration(uint generation)
        {
            unchecked
            {
                uint next = generation + 1u;
                return next == 0u ? 1u : next;
            }
        }

        public struct Enumerator : IEnumerator<EntityHandle>
        {
            private readonly EntityStore _store;
            private readonly int _version;
            private int _index;
            private EntityHandle _current;

            internal Enumerator(EntityStore store)
            {
                _store = store;
                _version = store._version;
                _index = -1;
                _current = EntityHandle.Invalid;
            }

            public EntityHandle Current
            {
                get
                {
                    EnsureValid();
                    if (!_current.IsValid) throw new InvalidOperationException("The enumerator is not positioned on an entity.");
                    return _current;
                }
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                EnsureValid();
                while (++_index < _store._alive.Count)
                {
                    if (!_store._alive[_index]) continue;
                    _current = new EntityHandle(_index, _store._generations[_index]);
                    return true;
                }

                _current = EntityHandle.Invalid;
                return false;
            }

            public void Reset()
            {
                EnsureValid();
                _index = -1;
                _current = EntityHandle.Invalid;
            }

            public void Dispose() { }

            private void EnsureValid()
            {
                if (_store == null) throw new InvalidOperationException("The enumerator is uninitialized.");
                _store.EnsureNotDisposed();
                if (_version != _store._version)
                    throw new InvalidOperationException("The entity store changed during enumeration.");
            }
        }
    }
}
