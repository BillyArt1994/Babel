using UnityEngine;

namespace Babel
{
    public class OnClickTrigger : TriggerBase
    {
        private readonly float _cooldown;
        private readonly float _chargeTime;
        private readonly System.Func<float> _cooldownMultProvider;

        private float _cooldownTimer;
        private float _holdDuration;
        private bool _isCharging;
        private bool _enabled;
        private Vector2 _lastWorldPos;

        public OnClickTrigger(float cooldown, float chargeTime, System.Func<float> cooldownMultProvider = null)
        {
            _cooldown = cooldown;
            _chargeTime = chargeTime;
            _cooldownMultProvider = cooldownMultProvider;
        }

        private float EffectiveCooldown
        {
            get
            {
                float mult = _cooldownMultProvider != null ? _cooldownMultProvider() : 1.0f;
                return Mathf.Max(0f, _cooldown * mult);
            }
        }

        public float CooldownProgress
        {
            get
            {
                float effective = EffectiveCooldown;
                if (effective <= 0f) return 0f;
                return Mathf.Clamp01(_cooldownTimer / effective);
            }
        }

        /// <summary>
        /// 清空当前点击技能冷却。
        /// </summary>
        public void ResetCooldown()
        {
            _cooldownTimer = 0f;
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
            if (_cooldownTimer > 0f)
            {
                _isCharging = false;
                _holdDuration = 0f;
                return;
            }

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

            _cooldownTimer = EffectiveCooldown;
        }

        private void HandlePointerCancel(PointerInputContext ctx)
        {
            _isCharging = false;
            _holdDuration = 0f;
        }
    }
}
