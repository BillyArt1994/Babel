using System;
using Babel.Foundation;
using Babel.Gameplay.World;

namespace Babel.Gameplay.RunFlow
{
    public enum RunEventKind
    {
        PhaseChanged = 0,
        SpeedChanged = 1,
        UpgradeChoiceOpened = 2,
        UpgradeSelected = 3,
        RunWon = 4,
        RunLost = 5,
        RestartRequested = 6,
        ReturnToMenuRequested = 7,
        RunFaulted = 8,
        EntitySpawned = 9,
        EntityDespawned = 10
    }

    public readonly struct RunEvent
    {
        public RunEvent(RunEventKind kind, long tick, int intValue = 0, int secondaryIntValue = 0)
        {
            Kind = kind;
            Tick = tick;
            IntValue = intValue;
            SecondaryIntValue = secondaryIntValue;
            Entity = EntityHandle.Invalid;
            HumanId = string.Empty;
            SpawnPointId = string.Empty;
            DespawnReason = default;
        }

        private RunEvent(
            RunEventKind kind,
            long tick,
            EntityHandle entity,
            string humanId,
            string spawnPointId,
            DespawnReason despawnReason)
        {
            Kind = kind;
            Tick = tick;
            IntValue = 0;
            SecondaryIntValue = 0;
            Entity = entity;
            HumanId = humanId;
            SpawnPointId = spawnPointId;
            DespawnReason = despawnReason;
        }

        public static RunEvent EntitySpawned(
            long tick,
            EntityHandle entity,
            string humanId,
            string spawnPointId)
        {
            if (!entity.IsValid) throw new ArgumentException("A valid entity is required.", nameof(entity));
            if (string.IsNullOrWhiteSpace(humanId))
                throw new ArgumentException("A human ID is required.", nameof(humanId));
            return new RunEvent(
                RunEventKind.EntitySpawned,
                tick,
                entity,
                humanId,
                spawnPointId ?? string.Empty,
                default);
        }

        public static RunEvent EntityDespawned(
            long tick,
            EntityHandle entity,
            DespawnReason despawnReason)
        {
            if (!entity.IsValid) throw new ArgumentException("A valid entity is required.", nameof(entity));
            if (!Enum.IsDefined(typeof(DespawnReason), despawnReason))
                throw new ArgumentOutOfRangeException(nameof(despawnReason));
            return new RunEvent(
                RunEventKind.EntityDespawned,
                tick,
                entity,
                string.Empty,
                string.Empty,
                despawnReason);
        }

        public RunEventKind Kind { get; }
        public long Tick { get; }
        public int IntValue { get; }
        public int SecondaryIntValue { get; }
        public EntityHandle Entity { get; }
        public string HumanId { get; }
        public string SpawnPointId { get; }
        public DespawnReason DespawnReason { get; }
    }

    public enum RunDomainEventKind
    {
        DamageResolved = 0,
        EntityDied = 1,
        BuildContributionCommitted = 2,
        ExperienceGranted = 3
    }

    public readonly struct RunDomainEvent
    {
        public RunDomainEvent(RunDomainEventKind kind, long tick, int sourceId = 0, int targetId = 0, float value = 0f)
        {
            Kind = kind;
            Tick = tick;
            SourceId = sourceId;
            TargetId = targetId;
            Value = value;
        }

        public RunDomainEventKind Kind { get; }
        public long Tick { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public float Value { get; }
    }

    public sealed class RunFaultInfo
    {
        public RunFaultInfo(long tick, string systemName, Exception exception)
        {
            Tick = tick;
            SystemName = string.IsNullOrWhiteSpace(systemName) ? "RunLoop" : systemName;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public long Tick { get; }
        public string SystemName { get; }
        public Exception Exception { get; }
        public string Message => Exception.Message;
    }
}
