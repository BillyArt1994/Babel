using UnityEngine;

namespace Babel
{
    public class OnHitTrigger : TriggerBase
    {
        private readonly float _chance;
        private bool _enabled;

        public OnHitTrigger(float chance)
        {
            _chance = chance;
        }

        public override void Enable()
        {
            _enabled = true;
            ClickAttackSystem.OnAttackExecuted += HandleAttackExecuted;
        }

        public override void Disable()
        {
            _enabled = false;
            ClickAttackSystem.OnAttackExecuted -= HandleAttackExecuted;
        }

        private void HandleAttackExecuted(AttackResult result)
        {
            if (!_enabled) return;
            if (result.IsPassive) return;
            if (Random.value > _chance) return;

            Fire(new TriggerContext
            {
                WorldPos = result.WorldPos,
                ChargeRatio = 1.0f,
                Target = result.Target,
                IsPassive = true
            });
        }
    }
}
