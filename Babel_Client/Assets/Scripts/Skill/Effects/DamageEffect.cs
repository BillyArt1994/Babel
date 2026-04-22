using UnityEngine;

namespace Babel
{
    public class DamageEffect : IEffect
    {
        private const float CHARGE_MULT = 1.5f;
        private const float CRIT_MULT = 2.0f;

        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;
        private readonly bool _isAoe;

        private static readonly Collider2D[] _hitBuffer = new Collider2D[64];

        public DamageEffect(EffectConfig config, EffectManager effectManager, bool isAoe)
        {
            _config = config;
            _effectManager = effectManager;
            _isAoe = isAoe;
        }

        public void Execute(TriggerContext context)
        {
            // 1. Query buff multipliers
            float damageMult = _effectManager.GetStatValue("damageMult");
            float critChance = _effectManager.GetStatValue("critChance") - 1.0f;
            bool isCrit = Random.value < critChance;

            // 2. Calculate final damage
            float baseDamage = _config.Damage;
            if (baseDamage <= 0 && _config.DamageRatio > 0)
            {
                baseDamage = _config.DamageRatio * 100f;
            }

            float finalDamage = baseDamage
                * Mathf.Lerp(1.0f, CHARGE_MULT, context.ChargeRatio)
                * damageMult
                * (isCrit ? CRIT_MULT : 1.0f);

            // 3. Physics detection
            int hitCount = 0;
            IDamageable firstTarget = null;

            if (_isAoe && _config.Radius > 0)
            {
                int count = Physics2D.OverlapCircleNonAlloc(context.WorldPos, _config.Radius, _hitBuffer);
                for (int i = 0; i < count; i++)
                {
                    if (_hitBuffer[i].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(finalDamage, isCrit);
                        hitCount++;
                        if (firstTarget == null) firstTarget = target;
                    }
                }
            }
            else
            {
                int count = Physics2D.OverlapPointNonAlloc(context.WorldPos, _hitBuffer);
                for (int i = 0; i < count; i++)
                {
                    if (_hitBuffer[i].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(finalDamage, isCrit);
                        hitCount++;
                        firstTarget = target;
                        break;
                    }
                }
            }

            // 4. Broadcast attack result
            if (hitCount > 0)
            {
                ClickAttackSystem.RaiseAttackExecuted(new AttackResult
                {
                    WorldPos = context.WorldPos,
                    Target = firstTarget,
                    IsPassive = context.IsPassive,
                    HitCount = hitCount
                });
            }
        }
    }
}
