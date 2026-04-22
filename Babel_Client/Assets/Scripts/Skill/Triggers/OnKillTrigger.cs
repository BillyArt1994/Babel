using UnityEngine;

namespace Babel
{
    public class OnKillTrigger : TriggerBase
    {
        private readonly float _chance;
        private bool _enabled;

        public OnKillTrigger(float chance)
        {
            _chance = chance;
        }

        public override void Enable()
        {
            _enabled = true;
            EnemyEvents.OnEnemyDied += HandleEnemyDied;
        }

        public override void Disable()
        {
            _enabled = false;
            EnemyEvents.OnEnemyDied -= HandleEnemyDied;
        }

        private void HandleEnemyDied(Vector2 deathPos)
        {
            if (!_enabled) return;
            if (Random.value > _chance) return;

            Fire(new TriggerContext
            {
                WorldPos = deathPos,
                ChargeRatio = 1.0f,
                Target = null,
                IsPassive = true
            });
        }
    }
}
