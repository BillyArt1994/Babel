using System;
using System.Collections.Generic;

namespace Babel.Foundation
{
    public sealed class SubscriptionBag : IDisposable
    {
        private readonly List<IDisposable> _subscriptions;
        private bool _isDisposed;

        public SubscriptionBag(int initialCapacity = 4)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _subscriptions = new List<IDisposable>(initialCapacity);
        }

        public int Count => _subscriptions.Count;
        public bool IsDisposed => _isDisposed;

        public T Add<T>(T subscription) where T : IDisposable
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription));
            if (_isDisposed)
            {
                subscription.Dispose();
                return subscription;
            }

            _subscriptions.Add(subscription);
            return subscription;
        }

        public IDisposable Add(Action unsubscribe)
        {
            if (unsubscribe == null) throw new ArgumentNullException(nameof(unsubscribe));
            return Add(new ActionSubscription(unsubscribe));
        }

        public void Clear()
        {
            List<Exception> errors = null;
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _subscriptions[i].Dispose();
                }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
            }

            _subscriptions.Clear();
            if (errors != null) throw new AggregateException("One or more subscriptions failed to dispose.", errors);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Clear();
        }

        private sealed class ActionSubscription : IDisposable
        {
            private Action _unsubscribe;

            public ActionSubscription(Action unsubscribe) { _unsubscribe = unsubscribe; }

            public void Dispose()
            {
                Action unsubscribe = _unsubscribe;
                _unsubscribe = null;
                if (unsubscribe != null) unsubscribe();
            }
        }
    }
}
