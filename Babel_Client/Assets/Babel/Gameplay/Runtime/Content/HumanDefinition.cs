using System;

namespace Babel.Gameplay.Content
{
    public sealed class HumanDefinition
    {
        public HumanDefinition(
            string id,
            string displayName,
            float maxHealth,
            float moveSpeed,
            int buildContribution,
            int buildCharges,
            int experienceReward,
            string abilityType = "",
            float abilityRadius = 0f,
            float abilityValue = 0f,
            float abilityCooldownSeconds = 0f,
            float buildTimeSeconds = 0f,
            string moveMode = "",
            float senseRadius = 0f)
        {
            Id = ContentValidation.RequireId(id, nameof(id));
            DisplayName = ContentValidation.RequireText(displayName, nameof(displayName));
            MaxHealth = ContentValidation.RequirePositive(maxHealth, nameof(maxHealth));
            MoveSpeed = ContentValidation.RequireNonNegative(moveSpeed, nameof(moveSpeed));
            if (buildContribution < 0) throw new ArgumentOutOfRangeException(nameof(buildContribution));
            if (buildCharges < 0) throw new ArgumentOutOfRangeException(nameof(buildCharges));
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));

            BuildContribution = buildContribution;
            BuildCharges = buildCharges;
            ExperienceReward = experienceReward;
            AbilityType = ContentValidation.OptionalId(abilityType, nameof(abilityType));
            AbilityRadius = ContentValidation.RequireNonNegative(abilityRadius, nameof(abilityRadius));
            AbilityValue = ContentValidation.RequireFinite(abilityValue, nameof(abilityValue));
            AbilityCooldownSeconds = ContentValidation.RequireNonNegative(abilityCooldownSeconds, nameof(abilityCooldownSeconds));
            BuildTimeSeconds = ContentValidation.RequireNonNegative(buildTimeSeconds, nameof(buildTimeSeconds));
            MoveMode = ContentValidation.OptionalId(moveMode, nameof(moveMode));
            SenseRadius = ContentValidation.RequireNonNegative(senseRadius, nameof(senseRadius));
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public int BuildContribution { get; }
        public int BuildCharges { get; }
        public int ExperienceReward { get; }
        public string AbilityType { get; }
        public float AbilityRadius { get; }
        public float AbilityValue { get; }
        public float AbilityCooldownSeconds { get; }
        public float BuildTimeSeconds { get; }
        public string MoveMode { get; }
        public float SenseRadius { get; }
    }
}
