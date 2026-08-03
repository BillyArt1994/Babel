using System;
using Babel.Foundation;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.World;

namespace Babel.Gameplay.Systems
{
    internal sealed class EncounterSystem : IRunSystem
    {
        private const double TimeEpsilon = 0.0000001d;
        private readonly GameWorld _world;

        public EncounterSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.Encounter;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            double elapsedSeconds = context.Clock.Tick * context.Settings.FixedDeltaSeconds;
            for (int waveIndex = 0; waveIndex < _world.Content.Waves.Count; waveIndex++)
            {
                WaveDefinition wave = _world.Content.Waves.All[waveIndex];
                WaveRuntimeState state = _world.Encounter.Get(waveIndex);
                if (elapsedSeconds + TimeEpsilon < wave.StartSeconds) continue;

                if (wave.Mode == WaveSpawnMode.Burst)
                {
                    if (state.Started) continue;
                    state.Started = true;
                    Enqueue(context, wave, RollCount(context, wave));
                    continue;
                }

                if (!state.Started)
                {
                    state.Started = true;
                    state.NextSpawnSeconds = wave.StartSeconds;
                }

                int safety = 0;
                while (elapsedSeconds + TimeEpsilon >= state.NextSpawnSeconds &&
                       IsInsideWindow(wave, state.NextSpawnSeconds))
                {
                    if (wave.Mode == WaveSpawnMode.Timed)
                    {
                        Enqueue(context, wave, RollCount(context, wave));
                    }
                    else
                    {
                        int queued = CountQueued(wave.Id);
                        int living = _world.CountLivingHumansFromWave(wave.Id);
                        int missing = RollCount(context, wave) - living - queued;
                        if (missing > 0) Enqueue(context, wave, missing);
                    }

                    state.NextSpawnSeconds += wave.IntervalSeconds;
                    if (++safety > 10000)
                        throw new InvalidOperationException("Encounter wave produced too many intervals in one tick: " + wave.Id);
                }
            }
        }

        private static bool IsInsideWindow(WaveDefinition wave, double spawnSeconds)
        {
            return wave.EndSeconds <= 0f || spawnSeconds + TimeEpsilon < wave.EndSeconds;
        }

        private static int RollCount(RunContext context, WaveDefinition wave)
        {
            return wave.CountMin == wave.CountMax
                ? wave.CountMin
                : context.Random.NextInt(wave.CountMin, checked(wave.CountMax + 1));
        }

        private void Enqueue(RunContext context, WaveDefinition wave, int count)
        {
            for (int i = 0; i < count; i++)
                _world.Spawn.Add(new SpawnWork(SelectHuman(context, wave), wave.Id, wave.SpawnPointId));
        }

        private static string SelectHuman(RunContext context, WaveDefinition wave)
        {
            float total = 0f;
            for (int i = 0; i < wave.Pool.Count; i++) total += wave.Pool[i].Weight;

            float roll = context.Random.NextFloat() * total;
            for (int i = 0; i < wave.Pool.Count; i++)
            {
                PoolEntry entry = wave.Pool[i];
                if (roll < entry.Weight) return entry.HumanId;
                roll -= entry.Weight;
            }

            return wave.Pool[wave.Pool.Count - 1].HumanId;
        }

        private int CountQueued(string waveId)
        {
            int count = 0;
            for (int i = 0; i < _world.Spawn.Items.Count; i++)
            {
                if (string.Equals(_world.Spawn.Items[i].WaveId, waveId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }
    }

    internal sealed class SpawnResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public SpawnResolutionSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.Encounter;
        public int Order => 100;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            for (int i = 0; i < _world.Spawn.Items.Count; i++)
            {
                SpawnWork work = _world.Spawn.Items[i];
                EntityHandle entity = _world.SpawnHuman(
                    work.HumanId,
                    context.Clock.Tick,
                    work.WaveId,
                    work.SpawnPointId);
                context.PresentationEvents.Add(
                    RunEvent.EntitySpawned(context.Clock.Tick, entity, work.HumanId, work.SpawnPointId));
            }

            _world.Spawn.ClearResolved();
        }
    }
}
