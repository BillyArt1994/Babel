using System;
using Babel.Gameplay.Content;

namespace Babel.Gameplay.World
{
    public enum HumanWorkMode
    {
        Builder = 0,
        Scout = 1
    }

    public readonly struct HumanState
    {
        public HumanState(string definitionId, long spawnTick, string waveId, string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("A human definition ID is required.", nameof(definitionId));
            if (spawnTick < 0) throw new ArgumentOutOfRangeException(nameof(spawnTick));
            DefinitionId = definitionId;
            SpawnTick = spawnTick;
            WaveId = waveId ?? string.Empty;
            SpawnPointId = spawnPointId ?? string.Empty;
        }

        public string DefinitionId { get; }
        public long SpawnTick { get; }
        public string WaveId { get; }
        public string SpawnPointId { get; }
    }

    public readonly struct HealthState
    {
        public HealthState(float currentHealth)
        {
            if (float.IsNaN(currentHealth) || float.IsInfinity(currentHealth) || currentHealth < 0f)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            CurrentHealth = currentHealth;
        }

        public float CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0f;

        internal HealthState ApplyDamage(float amount)
        {
            return new HealthState(Math.Max(0f, CurrentHealth - amount));
        }
    }

    public struct BuilderState
    {
        public BuilderState(HumanWorkMode workMode, int remainingCharges)
        {
            if (!Enum.IsDefined(typeof(HumanWorkMode), workMode)) throw new ArgumentOutOfRangeException(nameof(workMode));
            if (remainingCharges < 0) throw new ArgumentOutOfRangeException(nameof(remainingCharges));
            WorkMode = workMode;
            RemainingCharges = remainingCharges;
            LayerIndex = 0;
            TargetPointIndex = -1;
            RemainingBuildSeconds = 0d;
            HasPendingContribution = false;
        }

        public HumanWorkMode WorkMode { get; }
        public int RemainingCharges { get; internal set; }
        public int LayerIndex { get; internal set; }
        public int TargetPointIndex { get; internal set; }
        public double RemainingBuildSeconds { get; internal set; }
        public bool HasPendingContribution { get; internal set; }
        public bool HasTarget => TargetPointIndex >= 0;

        internal void AssignTarget(int layerIndex, int pointIndex, double buildTimeSeconds)
        {
            if (layerIndex < 0) throw new ArgumentOutOfRangeException(nameof(layerIndex));
            if (pointIndex < 0) throw new ArgumentOutOfRangeException(nameof(pointIndex));
            if (double.IsNaN(buildTimeSeconds) || double.IsInfinity(buildTimeSeconds) || buildTimeSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(buildTimeSeconds));
            LayerIndex = layerIndex;
            TargetPointIndex = pointIndex;
            RemainingBuildSeconds = buildTimeSeconds;
            HasPendingContribution = false;
        }

        internal void ClearTarget()
        {
            TargetPointIndex = -1;
            RemainingBuildSeconds = 0d;
            HasPendingContribution = false;
        }
    }

    public sealed class ProgressionState
    {
        public int TotalExperience { get; private set; }
        public int Level { get; private set; } = 1;
        public float ProgressToNextLevel { get; private set; }

        internal void Reset()
        {
            TotalExperience = 0;
            Level = 1;
            ProgressToNextLevel = 0f;
        }

        internal void GrantExperience(int amount, ExperienceTable table)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (table == null) throw new ArgumentNullException(nameof(table));
            checked { TotalExperience += amount; }

            Level = table.ResolveLevel(TotalExperience);
            if (Level >= table.MaxLevel)
            {
                ProgressToNextLevel = 1f;
                return;
            }

            float levelStart = table.GetCumulativeXpToReachLevel(Level);
            float required = table.GetRequiredXpForNextLevel(Level);
            ProgressToNextLevel = Math.Max(0f, Math.Min(1f, (TotalExperience - levelStart) / required));
        }
    }

    internal sealed class WaveRuntimeState
    {
        public bool Started;
        public double NextSpawnSeconds;
    }

    internal sealed class EncounterRuntimeState
    {
        private readonly WaveRuntimeState[] _waves;

        public EncounterRuntimeState(int waveCount)
        {
            if (waveCount < 0) throw new ArgumentOutOfRangeException(nameof(waveCount));
            _waves = new WaveRuntimeState[waveCount];
            for (int i = 0; i < _waves.Length; i++) _waves[i] = new WaveRuntimeState();
        }

        public WaveRuntimeState Get(int index) => _waves[index];

        public void Reset()
        {
            for (int i = 0; i < _waves.Length; i++)
            {
                _waves[i].Started = false;
                _waves[i].NextSpawnSeconds = 0d;
            }
        }
    }
}
