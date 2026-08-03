using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Unity.Infrastructure.Pooling;
using UnityEngine;

namespace Babel.Unity.Presentation
{
    /// <summary>
    /// Maintains the presentation binding for each live entity slot. The registry owns
    /// checked-out views, but not the pools that created them.
    /// </summary>
    public sealed class EntityViewRegistry : IDisposable
    {
        private sealed class Binding
        {
            public Binding(EntityHandle entity, GameObject view, GameObjectPool pool)
            {
                Entity = entity;
                View = view;
                Pool = pool;
            }

            public EntityHandle Entity { get; }
            public GameObject View { get; }
            public GameObjectPool Pool { get; }
        }

        private readonly Dictionary<int, Binding> _bindings = new Dictionary<int, Binding>();
        private bool _disposed;

        public int Count => _bindings.Count;
        public bool IsDisposed => _disposed;

        public GameObject Bind(
            EntityHandle entity,
            GameObjectPool pool,
            Transform parent = null,
            bool worldPositionStays = false)
        {
            ThrowIfDisposed();
            ValidateEntity(entity);
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            if (_bindings.TryGetValue(entity.Index, out Binding existing))
            {
                if (existing.Entity == entity)
                    throw new InvalidOperationException($"{entity} already has a bound view.");

                throw new InvalidOperationException(
                    $"Entity index {entity.Index} is still bound to generation " +
                    $"{existing.Entity.Generation}; unbind that exact handle before binding generation {entity.Generation}.");
            }

            GameObject view = pool.Get(parent, worldPositionStays);
            try
            {
                _bindings.Add(entity.Index, new Binding(entity, view, pool));
                return view;
            }
            catch
            {
                pool.Return(view);
                throw;
            }
        }

        public bool TryGet(EntityHandle entity, out GameObject view)
        {
            ThrowIfDisposed();
            if (!entity.IsValid ||
                !_bindings.TryGetValue(entity.Index, out Binding binding) ||
                binding.Entity != entity ||
                binding.View == null)
            {
                view = null;
                return false;
            }

            view = binding.View;
            return true;
        }

        public bool TryGetComponent<TView>(EntityHandle entity, out TView view)
            where TView : Component
        {
            if (TryGet(entity, out GameObject gameObject) &&
                gameObject.TryGetComponent(out view))
            {
                return true;
            }

            view = null;
            return false;
        }

        public bool Unbind(EntityHandle entity)
        {
            ThrowIfDisposed();
            if (!entity.IsValid ||
                !_bindings.TryGetValue(entity.Index, out Binding binding) ||
                binding.Entity != entity)
            {
                return false;
            }

            _bindings.Remove(entity.Index);
            if (binding.View != null)
                binding.Pool.Return(binding.View);
            return true;
        }

        public void Clear()
        {
            ThrowIfDisposed();
            ReturnAllBindings();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                ReturnAllBindings();
            }
            finally
            {
                _disposed = true;
            }
        }

        private void ReturnAllBindings()
        {
            if (_bindings.Count == 0) return;

            var snapshot = new Binding[_bindings.Count];
            _bindings.Values.CopyTo(snapshot, 0);
            _bindings.Clear();

            List<Exception> errors = null;
            for (int i = 0; i < snapshot.Length; i++)
            {
                Binding binding = snapshot[i];
                if (binding.View == null) continue;

                try
                {
                    binding.Pool.Return(binding.View);
                }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
            }

            if (errors != null)
                throw new AggregateException("One or more entity views could not be returned to their pools.", errors);
        }

        private static void ValidateEntity(EntityHandle entity)
        {
            if (!entity.IsValid)
                throw new ArgumentException("A valid entity handle is required.", nameof(entity));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EntityViewRegistry));
        }
    }
}
