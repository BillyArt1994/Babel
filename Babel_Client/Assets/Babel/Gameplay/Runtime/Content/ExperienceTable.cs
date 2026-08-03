using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public sealed class ExperienceTable
    {
        private readonly ReadOnlyCollection<float> _requiredXpByCurrentLevel;

        public ExperienceTable(params float[] requiredXpByCurrentLevel)
        {
            if (requiredXpByCurrentLevel == null) throw new ArgumentNullException(nameof(requiredXpByCurrentLevel));
            if (requiredXpByCurrentLevel.Length == 0)
                throw new ArgumentException("An experience table requires at least one threshold.", nameof(requiredXpByCurrentLevel));

            var copy = new List<float>(requiredXpByCurrentLevel.Length);
            float previous = 0f;
            for (int i = 0; i < requiredXpByCurrentLevel.Length; i++)
            {
                float threshold = ContentValidation.RequirePositive(requiredXpByCurrentLevel[i], nameof(requiredXpByCurrentLevel));
                if (i > 0 && threshold < previous)
                    throw new ArgumentException("Experience thresholds must be non-decreasing.", nameof(requiredXpByCurrentLevel));
                copy.Add(threshold);
                previous = threshold;
            }

            _requiredXpByCurrentLevel = copy.AsReadOnly();
        }

        public int MinLevel => 1;
        public int MaxLevel => _requiredXpByCurrentLevel.Count + 1;
        public IReadOnlyList<float> RequiredXpByCurrentLevel => _requiredXpByCurrentLevel;

        public float GetRequiredXpForNextLevel(int currentLevel)
        {
            if (currentLevel < MinLevel || currentLevel >= MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            return _requiredXpByCurrentLevel[currentLevel - 1];
        }

        public float GetCumulativeXpToReachLevel(int targetLevel)
        {
            if (targetLevel < MinLevel || targetLevel > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(targetLevel));

            float total = 0f;
            for (int i = 0; i < targetLevel - 1; i++) total += _requiredXpByCurrentLevel[i];
            return total;
        }

        public int ResolveLevel(float totalExperience)
        {
            ContentValidation.RequireNonNegative(totalExperience, nameof(totalExperience));
            float spent = 0f;
            for (int level = MinLevel; level < MaxLevel; level++)
            {
                spent += _requiredXpByCurrentLevel[level - 1];
                if (totalExperience < spent) return level;
            }

            return MaxLevel;
        }
    }
}
