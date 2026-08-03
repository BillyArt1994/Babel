using System;

namespace Babel.Gameplay.Content
{
    public sealed class EffectDefinition
    {
        public EffectDefinition(
            string effectType,
            float damage = 0f,
            float damageRatio = 0f,
            float radius = 0f,
            float damagePerSecond = 0f,
            float durationSeconds = 0f,
            string statName = "",
            float statValue = 0f)
        {
            EffectType = ContentValidation.RequireId(effectType, nameof(effectType));
            Damage = ContentValidation.RequireNonNegative(damage, nameof(damage));
            DamageRatio = ContentValidation.RequireNonNegative(damageRatio, nameof(damageRatio));
            Radius = ContentValidation.RequireNonNegative(radius, nameof(radius));
            DamagePerSecond = ContentValidation.RequireNonNegative(damagePerSecond, nameof(damagePerSecond));
            ContentValidation.RequireFinite(durationSeconds, nameof(durationSeconds));
            if (durationSeconds < 0f && durationSeconds != -1f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be non-negative or -1 for permanent effects.");

            DurationSeconds = durationSeconds;
            StatName = ContentValidation.OptionalId(statName, nameof(statName));
            StatValue = ContentValidation.RequireFinite(statValue, nameof(statValue));
        }

        public string EffectType { get; }
        public float Damage { get; }
        public float DamageRatio { get; }
        public float Radius { get; }
        public float DamagePerSecond { get; }
        public float DurationSeconds { get; }
        public string StatName { get; }
        public float StatValue { get; }
    }
}
