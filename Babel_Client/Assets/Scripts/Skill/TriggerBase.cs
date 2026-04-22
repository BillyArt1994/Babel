using System;

namespace Babel
{
    public abstract class TriggerBase
    {
        private Action<TriggerContext> _callback;

        public void Bind(Action<TriggerContext> callback) => _callback = callback;

        public virtual void Enable() { }
        public virtual void Disable() { }
        public virtual void Tick(float deltaTime) { }

        protected void Fire(TriggerContext ctx)
        {
            _callback?.Invoke(ctx);
        }
    }
}
