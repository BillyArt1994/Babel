using System;
using System.Collections.Generic;
using Babel.Foundation;
using UnityEngine;

namespace Babel.Unity.Presentation.UI
{
    /// <summary>
    /// Lightweight screen lifecycle. A screen owns only its visible-lifetime subscriptions;
    /// its GameObject and long-lived dependencies remain owned by the scene/composition root.
    /// </summary>
    public class Screen : MonoBehaviour, IDisposable
    {
        private SubscriptionBag _visibilitySubscriptions;
        private ScreenRouter _owner;
        private bool _isVisible;
        private bool _isDisposed;

        public bool IsVisible => _isVisible;
        public bool IsDisposed => _isDisposed;

        /// <summary>Available from OnScreenShown until the screen is hidden or disabled.</summary>
        protected SubscriptionBag VisibilitySubscriptions
        {
            get
            {
                if (!_isVisible || _visibilitySubscriptions == null)
                    throw new InvalidOperationException("Visibility subscriptions are available only while the screen is visible.");
                return _visibilitySubscriptions;
            }
        }

        protected virtual void OnScreenShown() { }
        protected virtual void OnScreenHidden() { }

        internal void AttachToRouter(ScreenRouter owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            ThrowIfDisposed();
            if (_owner != null && _owner != owner)
                throw new InvalidOperationException("Screen is already registered with another router.");
            if (_owner == owner)
                throw new InvalidOperationException("Screen is already registered with this router.");

            _owner = owner;
            ForceHiddenForRegistration();
        }

        internal void DetachFromRouter(ScreenRouter owner)
        {
            if (_owner != owner) return;
            _owner = null;
            HideFromRouter();
        }

        internal bool ShowFromRouter()
        {
            ThrowIfDisposed();
            if (_isVisible) return false;

            _isVisible = true;
            _visibilitySubscriptions = new SubscriptionBag();
            try
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                OnScreenShown();
                return true;
            }
            catch
            {
                HideCore(true, false);
                throw;
            }
        }

        internal bool HideFromRouter()
        {
            return HideCore(true, true);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            List<Exception> errors = null;
            try
            {
                HideCore(true, true);
            }
            catch (Exception exception)
            {
                errors = new List<Exception> { exception };
            }

            _isDisposed = true;
            ScreenRouter owner = _owner;
            _owner = null;
            if (owner != null) owner.NotifyScreenDisposed(this);

            if (errors != null) throw new AggregateException("Screen disposal failed.", errors);
        }

        private void ForceHiddenForRegistration()
        {
            _isVisible = false;
            DisposeVisibilitySubscriptions();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private bool HideCore(bool deactivateGameObject, bool invokeCallback)
        {
            bool wasVisible = _isVisible;
            _isVisible = false;
            List<Exception> errors = null;

            if (wasVisible && invokeCallback)
            {
                try
                {
                    OnScreenHidden();
                }
                catch (Exception exception)
                {
                    errors = new List<Exception> { exception };
                }
            }

            try
            {
                DisposeVisibilitySubscriptions();
            }
            catch (Exception exception)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(exception);
            }

            if (deactivateGameObject && gameObject.activeSelf)
                gameObject.SetActive(false);

            if (errors != null)
                throw new AggregateException("Screen hide lifecycle failed.", errors);
            return wasVisible;
        }

        private void DisposeVisibilitySubscriptions()
        {
            SubscriptionBag subscriptions = _visibilitySubscriptions;
            _visibilitySubscriptions = null;
            if (subscriptions != null) subscriptions.Dispose();
        }

        private void OnDisable()
        {
            if (!_isVisible)
            {
                try
                {
                    DisposeVisibilitySubscriptions();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
                return;
            }

            try
            {
                HideCore(false, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
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

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(GetType().Name);
        }
    }
}
