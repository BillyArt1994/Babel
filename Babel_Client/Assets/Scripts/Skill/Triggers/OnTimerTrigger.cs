using UnityEngine;

namespace Babel
{
    public class OnTimerTrigger : TriggerBase
    {
        private readonly float _interval;
        private float _timer;
        private bool _enabled;

        public System.Func<Vector2> GetBasePosition;

        public OnTimerTrigger(float interval)
        {
            _interval = interval;
        }

        public override void Enable()
        {
            _enabled = true;
            _timer = _interval;
        }

        public override void Disable()
        {
            _enabled = false;
            _timer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (!_enabled) return;

            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _timer = _interval;

                Vector2 basePos = GetBasePosition != null
                    ? GetBasePosition()
                    : Vector2.zero;

                Fire(new TriggerContext
                {
                    WorldPos = basePos,
                    ChargeRatio = 1.0f,
                    Target = null,
                    IsPassive = true
                });
            }
        }
    }
}
