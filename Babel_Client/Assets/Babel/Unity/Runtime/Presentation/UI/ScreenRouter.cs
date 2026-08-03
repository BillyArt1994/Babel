using System;
using System.Collections.Generic;
using UnityEngine;

namespace Babel.Unity.Presentation.UI
{
    /// <summary>Instance-owned screen registry and history stack with no loading or global state.</summary>
    [DisallowMultipleComponent]
    public sealed class ScreenRouter : MonoBehaviour, IDisposable
    {
        private readonly Dictionary<string, Screen> _screens =
            new Dictionary<string, Screen>(StringComparer.Ordinal);
        private readonly Dictionary<Screen, string> _idsByScreen =
            new Dictionary<Screen, string>();
        private readonly List<string> _history = new List<string>();

        private string _currentId;
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;
        public int RegistrationCount => _screens.Count;
        public int HistoryDepth => _history.Count;
        public string CurrentId => _currentId;

        public Screen CurrentScreen
        {
            get
            {
                if (_currentId == null) return null;
                return _screens.TryGetValue(_currentId, out Screen screen) ? screen : null;
            }
        }

        public void Register(string id, Screen screen)
        {
            ThrowIfDisposed();
            ValidateId(id);
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (screen.gameObject == gameObject)
                throw new ArgumentException("A screen cannot share the router GameObject.", nameof(screen));
            if (_screens.ContainsKey(id))
                throw new InvalidOperationException("A screen is already registered as '" + id + "'.");
            if (_idsByScreen.TryGetValue(screen, out string existingId))
                throw new InvalidOperationException("Screen is already registered as '" + existingId + "'.");

            screen.AttachToRouter(this);
            try
            {
                _screens.Add(id, screen);
                _idsByScreen.Add(screen, id);
            }
            catch
            {
                screen.DetachFromRouter(this);
                throw;
            }
        }

        public bool Unregister(string id)
        {
            ThrowIfDisposed();
            ValidateId(id);
            if (!_screens.TryGetValue(id, out Screen screen)) return false;

            if (string.Equals(_currentId, id, StringComparison.Ordinal))
                _currentId = null;
            RemoveFromHistory(id);
            _screens.Remove(id);
            _idsByScreen.Remove(screen);
            if (screen != null) screen.DetachFromRouter(this);
            return true;
        }

        public bool Show(string id)
        {
            ThrowIfUnavailable();
            ValidateId(id);
            return ShowInternal(id, true);
        }

        public bool Hide(string id)
        {
            ThrowIfDisposed();
            ValidateId(id);
            if (!_screens.TryGetValue(id, out Screen screen)) return false;

            bool changed = screen != null && screen.HideFromRouter();
            if (string.Equals(_currentId, id, StringComparison.Ordinal))
                _currentId = null;
            return changed;
        }

        public bool HideCurrent()
        {
            ThrowIfDisposed();
            return _currentId != null && Hide(_currentId);
        }

        public bool Back()
        {
            ThrowIfUnavailable();
            while (_history.Count > 0)
            {
                int lastIndex = _history.Count - 1;
                string previousId = _history[lastIndex];
                _history.RemoveAt(lastIndex);
                if (!_screens.ContainsKey(previousId)) continue;

                if (_currentId != null && _screens.TryGetValue(_currentId, out Screen current) && current != null)
                    current.HideFromRouter();
                _currentId = null;
                return ShowInternal(previousId, false);
            }

            return false;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            var errors = new List<Exception>();
            Screen[] screens = new Screen[_screens.Count];
            _screens.Values.CopyTo(screens, 0);
            _currentId = null;
            _history.Clear();
            _screens.Clear();
            _idsByScreen.Clear();

            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i] == null) continue;
                try
                {
                    screens[i].DetachFromRouter(this);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
                throw new AggregateException("One or more screens failed to detach.", errors);
        }

        internal void NotifyScreenDisposed(Screen screen)
        {
            if (_isDisposed || ReferenceEquals(screen, null)) return;
            if (!_idsByScreen.TryGetValue(screen, out string id)) return;

            _idsByScreen.Remove(screen);
            _screens.Remove(id);
            RemoveFromHistory(id);
            if (string.Equals(_currentId, id, StringComparison.Ordinal))
                _currentId = null;
        }

        private bool ShowInternal(string id, bool rememberCurrent)
        {
            if (!_screens.TryGetValue(id, out Screen target) || target == null)
                throw new KeyNotFoundException("No screen is registered as '" + id + "'.");

            if (string.Equals(_currentId, id, StringComparison.Ordinal))
                return target.ShowFromRouter();

            string previousId = _currentId;
            Screen previous = null;
            bool pushed = false;
            if (previousId != null && _screens.TryGetValue(previousId, out previous) && previous != null)
            {
                previous.HideFromRouter();
                if (rememberCurrent)
                {
                    _history.Add(previousId);
                    pushed = true;
                }
            }
            _currentId = null;

            try
            {
                target.ShowFromRouter();
                _currentId = id;
                return true;
            }
            catch
            {
                if (pushed) _history.RemoveAt(_history.Count - 1);
                if (previous != null)
                {
                    try
                    {
                        previous.ShowFromRouter();
                        _currentId = previousId;
                    }
                    catch (Exception restoreError)
                    {
                        Debug.LogException(restoreError, previous);
                    }
                }
                throw;
            }
        }

        private void OnDisable()
        {
            if (_isDisposed) return;
            _currentId = null;
            _history.Clear();

            Screen[] screens = new Screen[_screens.Count];
            _screens.Values.CopyTo(screens, 0);
            for (int i = 0; i < screens.Length; i++)
            {
                Screen screen = screens[i];
                if (screen == null) continue;
                try
                {
                    screen.HideFromRouter();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, screen);
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void RemoveFromHistory(string id)
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_history[i], id, StringComparison.Ordinal))
                    _history.RemoveAt(i);
            }
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, id.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Screen ID cannot be empty or contain leading/trailing whitespace.", nameof(id));
        }

        private void ThrowIfUnavailable()
        {
            ThrowIfDisposed();
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("ScreenRouter must be active and enabled to navigate.");
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ScreenRouter));
        }
    }
}
