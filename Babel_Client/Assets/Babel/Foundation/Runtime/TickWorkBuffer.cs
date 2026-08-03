using System;
using System.Collections.Generic;

namespace Babel.Foundation
{
    /// <summary>
    /// Same-tick work queue used by ordered resolution stages such as damage,
    /// death rewards and Babel construction.
    /// </summary>
    public sealed class TickWorkBuffer<T>
    {
        private readonly List<T> _items;
        private bool _isTickOpen;

        public TickWorkBuffer(int initialCapacity = 32)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _items = new List<T>(initialCapacity);
        }

        public IReadOnlyList<T> Items { get { return _items; } }
        public int Count { get { return _items.Count; } }
        public bool IsTickOpen { get { return _isTickOpen; } }

        public void BeginTick()
        {
            if (_isTickOpen) throw new InvalidOperationException("The previous tick is still open.");
            _items.Clear();
            _isTickOpen = true;
        }

        public void Add(T value)
        {
            if (!_isTickOpen) throw new InvalidOperationException("BeginTick must be called before adding work.");
            _items.Add(value);
        }

        public void ClearResolved()
        {
            if (!_isTickOpen) throw new InvalidOperationException("No tick is open.");
            _items.Clear();
        }

        public void EndTick()
        {
            if (!_isTickOpen) throw new InvalidOperationException("No tick is open.");
            _items.Clear();
            _isTickOpen = false;
        }

        public void AbortTick()
        {
            _items.Clear();
            _isTickOpen = false;
        }
    }
}
