using UnityEngine;

namespace Babel
{
    public class OnClickTrigger : TriggerBase
    {
        private readonly float _cooldown;
        private readonly float _chargeTime;

        private float _cooldownTimer;
        private float _holdDuration;
        private bool _isCharging;
        private bool _enabled;
        private Vector2 _lastWorldPos;

        public OnClickTrigger(float cooldown, float chargeTime)
        {
            _cooldown = cooldown;
            _chargeTime = chargeTime;
        }

        public float CooldownProgress
        {
            get
            {
                if (_cooldown <= 0) return 0f;
                return Mathf.Clamp01(_cooldownTimer / _cooldown);
            }
        }

        public override void Enable()
        {
            _enabled = true;
            InputEvents.OnPointerDown += HandlePointerDown;
            InputEvents.OnPointerHold += HandlePointerHold;
            InputEvents.OnPointerUp += HandlePointerUp;
            InputEvents.OnPointerCancel += HandlePointerCancel;
        }

        public override void Disable()
        {
            _enabled = false;
            InputEvents.OnPointerDown -= HandlePointerDown;
            InputEvents.OnPointerHold -= HandlePointerHold;
            InputEvents.OnPointerUp -= HandlePointerUp;
            InputEvents.OnPointerCancel -= HandlePointerCancel;

            if (_isCharging)
            {
                _isCharging = false;
                _holdDuration = 0f;
            }
            _cooldownTimer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= deltaTime;
            }
        }

        private void HandlePointerDown(PointerInputContext ctx)
        {
            if (!_enabled) return;
            _isCharging = true;
            _holdDuration = 0f;
            _lastWorldPos = ctx.WorldPosition;
        }

        private void HandlePointerHold(PointerInputContext ctx)
        {
            if (!_enabled || !_isCharging) return;
            _holdDuration = ctx.HoldDuration;
            _lastWorldPos = ctx.WorldPosition;
        }

        private void HandlePointerUp(PointerInputContext ctx)
        {
            if (!_enabled || !_isCharging) return;

            _isCharging = false;
            _lastWorldPos = ctx.WorldPosition;

            if (_cooldownTimer > 0) return;

            float chargeRatio = _chargeTime > 0
                ? Mathf.Clamp01(_holdDuration / _chargeTime)
                : 1.0f;

            Fire(new TriggerContext
            {
                WorldPos = _lastWorldPos,
                ChargeRatio = chargeRatio,
                Target = null,
                IsPassive = false
            });

            _cooldownTimer = _cooldown;
        }

        private void HandlePointerCancel(PointerInputContext ctx)
        {
            _isCharging = false;
            _holdDuration = 0f;
        }
    }
}
