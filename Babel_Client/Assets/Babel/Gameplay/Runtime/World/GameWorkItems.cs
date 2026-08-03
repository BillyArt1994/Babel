using System;
using Babel.Foundation;

namespace Babel.Gameplay.World
{
    public readonly struct DamageWork
    {
        public DamageWork(EntityHandle source, EntityHandle target, float amount)
        {
            if (!target.IsValid) throw new ArgumentException("A valid target is required.", nameof(target));
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Source = source;
            Target = target;
            Amount = amount;
        }

        public EntityHandle Source { get; }
        public EntityHandle Target { get; }
        public float Amount { get; }
    }

    public readonly struct DeathRewardWork
    {
        public DeathRewardWork(EntityHandle victim, string humanId, int experience)
        {
            if (!victim.IsValid) throw new ArgumentException("A valid victim is required.", nameof(victim));
            if (string.IsNullOrWhiteSpace(humanId)) throw new ArgumentException("A human ID is required.", nameof(humanId));
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            Victim = victim;
            HumanId = humanId;
            Experience = experience;
        }

        public EntityHandle Victim { get; }
        public string HumanId { get; }
        public int Experience { get; }
    }

    public readonly struct BuildWork
    {
        public BuildWork(EntityHandle source, int layerIndex, int pointIndex, int amount)
        {
            if (!source.IsValid) throw new ArgumentException("A valid source is required.", nameof(source));
            if (layerIndex < 0) throw new ArgumentOutOfRangeException(nameof(layerIndex));
            if (pointIndex < 0) throw new ArgumentOutOfRangeException(nameof(pointIndex));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Source = source;
            LayerIndex = layerIndex;
            PointIndex = pointIndex;
            Amount = amount;
        }

        public EntityHandle Source { get; }
        public int LayerIndex { get; }
        public int PointIndex { get; }
        public int Amount { get; }
    }

    public readonly struct SpawnWork
    {
        public SpawnWork(string humanId, string waveId, string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(humanId)) throw new ArgumentException("A human ID is required.", nameof(humanId));
            HumanId = humanId;
            WaveId = waveId ?? string.Empty;
            SpawnPointId = spawnPointId ?? string.Empty;
        }

        public string HumanId { get; }
        public string WaveId { get; }
        public string SpawnPointId { get; }
    }

    public enum DespawnReason
    {
        Death = 0,
        BuildChargesExhausted = 1,
        EncounterEnded = 2
    }

    public readonly struct DespawnWork
    {
        public DespawnWork(EntityHandle entity, DespawnReason reason)
        {
            if (!entity.IsValid) throw new ArgumentException("A valid entity is required.", nameof(entity));
            if (!Enum.IsDefined(typeof(DespawnReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
            Entity = entity;
            Reason = reason;
        }

        public EntityHandle Entity { get; }
        public DespawnReason Reason { get; }
    }
}
