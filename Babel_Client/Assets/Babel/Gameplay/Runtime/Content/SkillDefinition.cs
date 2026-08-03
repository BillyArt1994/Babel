using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public sealed class SkillDefinition
    {
        private readonly ReadOnlyCollection<EffectDefinition> _effects;

        public SkillDefinition(
            string id,
            string displayName,
            string description,
            string iconId,
            string triggerType,
            float cooldownSeconds,
            float chargeTimeSeconds,
            float intervalSeconds,
            float chance,
            IEnumerable<EffectDefinition> effects,
            int level = 1,
            int maxLevel = 1,
            float weight = 1f,
            bool isStarterSkill = false,
            string upgradesFrom = "")
        {
            Id = ContentValidation.RequireId(id, nameof(id));
            DisplayName = ContentValidation.RequireText(displayName, nameof(displayName));
            Description = description ?? string.Empty;
            IconId = ContentValidation.OptionalId(iconId, nameof(iconId));
            TriggerType = ContentValidation.RequireId(triggerType, nameof(triggerType));
            CooldownSeconds = ContentValidation.RequireNonNegative(cooldownSeconds, nameof(cooldownSeconds));
            ChargeTimeSeconds = ContentValidation.RequireNonNegative(chargeTimeSeconds, nameof(chargeTimeSeconds));
            IntervalSeconds = ContentValidation.RequireNonNegative(intervalSeconds, nameof(intervalSeconds));
            ContentValidation.RequireFinite(chance, nameof(chance));
            if (chance < 0f || chance > 1f) throw new ArgumentOutOfRangeException(nameof(chance));
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
            if (maxLevel < level) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            Weight = ContentValidation.RequireNonNegative(weight, nameof(weight));
            UpgradesFrom = ContentValidation.OptionalId(upgradesFrom, nameof(upgradesFrom));
            if (effects == null) throw new ArgumentNullException(nameof(effects));

            var effectList = new List<EffectDefinition>();
            foreach (EffectDefinition effect in effects)
            {
                if (effect == null) throw new ArgumentException("Skill effects cannot contain null entries.", nameof(effects));
                effectList.Add(effect);
            }

            if (effectList.Count == 0) throw new ArgumentException("A skill requires at least one effect.", nameof(effects));
            if (effectList.Count > 3) throw new ArgumentException("A skill supports at most three ordered effects.", nameof(effects));

            Chance = chance;
            Level = level;
            MaxLevel = maxLevel;
            IsStarterSkill = isStarterSkill;
            _effects = effectList.AsReadOnly();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string IconId { get; }
        public string TriggerType { get; }
        public float CooldownSeconds { get; }
        public float ChargeTimeSeconds { get; }
        public float IntervalSeconds { get; }
        public float Chance { get; }
        public IReadOnlyList<EffectDefinition> Effects => _effects;
        public int Level { get; }
        public int MaxLevel { get; }
        public float Weight { get; }
        public bool IsStarterSkill { get; }
        public string UpgradesFrom { get; }
    }
}
