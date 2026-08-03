using System;
using Babel.Foundation;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;

namespace Babel.Gameplay.World
{
    /// <summary>Pure C# mutable state for one run. The attached RunContext owns its lifetime.</summary>
    public sealed class GameWorld : IRunWorldLifecycle
    {
        private bool _isDisposed;

        public GameWorld(GameRuntimeContent content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Entities = new EntityStore();
            Humans = new ComponentStore<HumanState>(Entities);
            Health = new ComponentStore<HealthState>(Entities);
            Builders = new ComponentStore<BuilderState>(Entities);
            Babel = new BabelState(content.Babel, content.BuildPointRequiredProgress);
            Progression = new ProgressionState();
            Encounter = new EncounterRuntimeState(content.Waves.Count);

            Damage = new TickWorkBuffer<DamageWork>();
            DeathRewards = new TickWorkBuffer<DeathRewardWork>();
            Build = new TickWorkBuffer<BuildWork>();
            Spawn = new TickWorkBuffer<SpawnWork>();
            Despawn = new TickWorkBuffer<DespawnWork>();
        }

        public GameRuntimeContent Content { get; }
        public EntityStore Entities { get; }
        public ComponentStore<HumanState> Humans { get; }
        public ComponentStore<HealthState> Health { get; }
        public ComponentStore<BuilderState> Builders { get; }
        public BabelState Babel { get; }
        public ProgressionState Progression { get; }

        public TickWorkBuffer<DamageWork> Damage { get; }
        public TickWorkBuffer<DeathRewardWork> DeathRewards { get; }
        public TickWorkBuffer<BuildWork> Build { get; }
        public TickWorkBuffer<SpawnWork> Spawn { get; }
        public TickWorkBuffer<DespawnWork> Despawn { get; }

        internal EncounterRuntimeState Encounter { get; }

        public EntityHandle SpawnHuman(string humanId, long spawnTick, string waveId = "", string spawnPointId = "")
        {
            EnsureNotDisposed();
            HumanDefinition definition = Content.Humans.GetRequired(humanId);
            EntityHandle entity = Entities.Create();

            try
            {
                Humans.Add(entity, new HumanState(definition.Id, spawnTick, waveId, spawnPointId));
                Health.Add(entity, new HealthState(definition.MaxHealth));
                if (definition.BuildContribution > 0 && definition.BuildCharges > 0)
                {
                    HumanWorkMode mode = string.Equals(definition.MoveMode, "scout", StringComparison.OrdinalIgnoreCase)
                        ? HumanWorkMode.Scout
                        : HumanWorkMode.Builder;
                    Builders.Add(entity, new BuilderState(mode, definition.BuildCharges));
                }

                return entity;
            }
            catch
            {
                Entities.Destroy(entity);
                throw;
            }
        }

        internal int CountLivingHumansFromWave(string waveId)
        {
            int count = 0;
            foreach (EntityHandle entity in Entities)
            {
                if (Humans.TryGet(entity, out HumanState human) &&
                    string.Equals(human.WaveId, waveId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public void Reset()
        {
            EnsureNotDisposed();
            AbortTick();
            Entities.Clear();
            Babel.Reset();
            Progression.Reset();
            Encounter.Reset();
        }

        void IRunWorldLifecycle.Reset() => Reset();
        void IRunWorldLifecycle.BeginTick() => BeginTick();
        void IRunWorldLifecycle.EndTick() => EndTick();
        void IRunWorldLifecycle.AbortTick() => AbortTick();

        public void Dispose()
        {
            if (_isDisposed) return;
            AbortTick();
            Builders.Dispose();
            Health.Dispose();
            Humans.Dispose();
            Entities.Dispose();
            _isDisposed = true;
        }

        private void BeginTick()
        {
            EnsureNotDisposed();
            try
            {
                Damage.BeginTick();
                DeathRewards.BeginTick();
                Build.BeginTick();
                Spawn.BeginTick();
                Despawn.BeginTick();
            }
            catch
            {
                AbortTick();
                throw;
            }
        }

        private void EndTick()
        {
            EnsureNotDisposed();
            Damage.EndTick();
            DeathRewards.EndTick();
            Build.EndTick();
            Spawn.EndTick();
            Despawn.EndTick();
        }

        private void AbortTick()
        {
            Damage.AbortTick();
            DeathRewards.AbortTick();
            Build.AbortTick();
            Spawn.AbortTick();
            Despawn.AbortTick();
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(GameWorld));
        }
    }
}
