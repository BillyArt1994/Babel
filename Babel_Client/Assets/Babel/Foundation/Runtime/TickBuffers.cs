using System;
using System.Collections.Generic;

namespace Babel.Foundation
{
    public sealed class TickCommandBuffer<T>
    {
        private List<T> _pending;
        private List<T> _current;
        private bool _isTickOpen;

        public TickCommandBuffer(int initialCapacity = 16)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _pending = new List<T>(initialCapacity);
            _current = new List<T>(initialCapacity);
        }

        public IReadOnlyList<T> Current => _current;
        public int PendingCount => _pending.Count;
        public bool IsTickOpen => _isTickOpen;

        public void Enqueue(T command) => _pending.Add(command);

        public void BeginTick()
        {
            if (_isTickOpen) throw new InvalidOperationException("The previous tick is still open.");
            List<T> swap = _current;
            _current = _pending;
            _pending = swap;
            _pending.Clear();
            _isTickOpen = true;
        }

        public void EndTick()
        {
            if (!_isTickOpen) throw new InvalidOperationException("No tick is open.");
            _current.Clear();
            _isTickOpen = false;
        }

        public void ClearPending() => _pending.Clear();

        public void AbortTick()
        {
            _pending.Clear();
            _current.Clear();
            _isTickOpen = false;
        }

        public void Clear() => AbortTick();
    }

    /// <summary>
    /// Domain events written during tick N become read-only input during tick N+1.
    /// This keeps gameplay semantics independent from how many ticks a render frame executes.
    /// </summary>
    public sealed class TickEventBuffer<T>
    {
        private List<T> _writing;
        private List<T> _current;
        private bool _isTickOpen;

        public TickEventBuffer(int initialCapacity = 32)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _writing = new List<T>(initialCapacity);
            _current = new List<T>(initialCapacity);
        }

        public IReadOnlyList<T> Current => _current;
        public int PendingCount => _writing.Count;
        public bool IsTickOpen => _isTickOpen;

        public void BeginTick()
        {
            if (_isTickOpen) throw new InvalidOperationException("The previous tick is still open.");
            List<T> swap = _current;
            _current = _writing;
            _writing = swap;
            _writing.Clear();
            _isTickOpen = true;
        }

        public void Add(T value)
        {
            if (!_isTickOpen) throw new InvalidOperationException("BeginTick must be called before adding domain events.");
            _writing.Add(value);
        }

        public void EndTick()
        {
            if (!_isTickOpen) throw new InvalidOperationException("No tick is open.");
            _current.Clear();
            _isTickOpen = false;
        }

        public void AbortTick()
        {
            _writing.Clear();
            _current.Clear();
            _isTickOpen = false;
        }

        public void Clear() => AbortTick();
    }

    /// <summary>
    /// Presentation events are accumulated across all simulation ticks in one render frame,
    /// then published atomically after the frame succeeds.
    /// </summary>
    public sealed class FrameEventBuffer<T>
    {
        private List<T> _writing;
        private List<T> _published;
        private bool _isFrameOpen;

        public FrameEventBuffer(int initialCapacity = 32)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _writing = new List<T>(initialCapacity);
            _published = new List<T>(initialCapacity);
        }

        public IReadOnlyList<T> Published => _published;
        public bool IsFrameOpen => _isFrameOpen;

        public void BeginFrame()
        {
            if (_isFrameOpen) throw new InvalidOperationException("The previous frame is still open.");
            _writing.Clear();
            _isFrameOpen = true;
        }

        public void Add(T value)
        {
            if (!_isFrameOpen) throw new InvalidOperationException("BeginFrame must be called before adding presentation events.");
            _writing.Add(value);
        }

        public void PublishFrame()
        {
            if (!_isFrameOpen) throw new InvalidOperationException("No frame is open.");
            List<T> swap = _published;
            _published = _writing;
            _writing = swap;
            _writing.Clear();
            _isFrameOpen = false;
        }

        public void AbortFrame()
        {
            _writing.Clear();
            _published.Clear();
            _isFrameOpen = false;
        }

        public void Clear() => AbortFrame();
    }
}
