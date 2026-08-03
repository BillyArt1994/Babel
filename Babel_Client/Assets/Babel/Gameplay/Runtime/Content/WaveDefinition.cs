using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public enum WaveSpawnMode
    {
        Burst = 0,
        Maintain = 1,
        Timed = 2
    }

    public readonly struct PoolEntry
    {
        public PoolEntry(string humanId, float weight)
        {
            HumanId = ContentValidation.RequireId(humanId, nameof(humanId));
            Weight = ContentValidation.RequirePositive(weight, nameof(weight));
        }

        public string HumanId { get; }
        public float Weight { get; }
    }

    public sealed class WaveDefinition
    {
        private readonly ReadOnlyCollection<PoolEntry> _pool;

        public WaveDefinition(
            string id,
            float startSeconds,
            float endSeconds,
            WaveSpawnMode mode,
            IEnumerable<PoolEntry> pool,
            int countMin,
            int countMax,
            float intervalSeconds,
            string spawnPointId)
        {
            Id = ContentValidation.RequireId(id, nameof(id));
            StartSeconds = ContentValidation.RequireNonNegative(startSeconds, nameof(startSeconds));
            EndSeconds = ContentValidation.RequireNonNegative(endSeconds, nameof(endSeconds));
            if (!Enum.IsDefined(typeof(WaveSpawnMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (endSeconds != 0f && endSeconds < startSeconds)
                throw new ArgumentOutOfRangeException(nameof(endSeconds), "End time cannot precede start time.");
            if (mode == WaveSpawnMode.Timed && endSeconds <= startSeconds)
                throw new ArgumentOutOfRangeException(nameof(endSeconds), "Timed waves require an end time after their start time.");
            if (countMin <= 0) throw new ArgumentOutOfRangeException(nameof(countMin));
            if (countMax < countMin) throw new ArgumentOutOfRangeException(nameof(countMax));

            IntervalSeconds = ContentValidation.RequireNonNegative(intervalSeconds, nameof(intervalSeconds));
            if (mode != WaveSpawnMode.Burst && intervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Non-burst waves require a positive interval.");
            SpawnPointId = ContentValidation.RequireId(spawnPointId, nameof(spawnPointId));
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            var entries = new List<PoolEntry>();
            foreach (PoolEntry entry in pool)
            {
                ContentValidation.RequireId(entry.HumanId, nameof(pool));
                ContentValidation.RequirePositive(entry.Weight, nameof(pool));
                entries.Add(entry);
            }

            if (entries.Count == 0) throw new ArgumentException("A wave must contain at least one pool entry.", nameof(pool));

            Mode = mode;
            CountMin = countMin;
            CountMax = countMax;
            _pool = entries.AsReadOnly();
        }

        public string Id { get; }
        public float StartSeconds { get; }
        public float EndSeconds { get; }
        public WaveSpawnMode Mode { get; }
        public IReadOnlyList<PoolEntry> Pool => _pool;
        public int CountMin { get; }
        public int CountMax { get; }
        public float IntervalSeconds { get; }
        public string SpawnPointId { get; }
    }
}
