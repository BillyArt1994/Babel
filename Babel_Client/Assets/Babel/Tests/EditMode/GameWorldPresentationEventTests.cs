using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.Systems;
using Babel.Gameplay.World;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class GameWorldPresentationEventTests
    {
        [Test]
        public void EncounterSpawn_EventCarriesExactEntityAndContent_BeforeHumanActsNextTick()
        {
            HumanDefinition worker = Human("worker", 10, 2);
            WaveDefinition opening = BurstWave(worker.Id, "left-gate");
            GameRuntimeContent content = Content(worker, new[] { opening });

            using (Harness harness = Harness.Create(content, new RunSettings(5d)))
            {
                harness.Start();
                harness.Step();

                RunEvent spawned = SingleEvent(harness.Context, RunEventKind.EntitySpawned);
                Assert.That(spawned.Tick, Is.EqualTo(1));
                Assert.That(spawned.Entity.IsValid, Is.True);
                Assert.That(spawned.Entity.Generation, Is.GreaterThan(0u));
                Assert.That(spawned.HumanId, Is.EqualTo(worker.Id));
                Assert.That(spawned.SpawnPointId, Is.EqualTo("left-gate"));
                Assert.That(harness.World.Entities.IsAlive(spawned.Entity), Is.True);
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.Zero);

                harness.Step();

                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.EqualTo(10));
                Assert.That(CountEvents(harness.Context, RunEventKind.EntitySpawned), Is.Zero);
            }
        }

        [Test]
        public void DeathDespawn_EventKeepsDestroyedGeneration_AndReusedSlotGetsNewGeneration()
        {
            HumanDefinition worker = Human("worker", 0, 0);
            GameRuntimeContent content = Content(worker, Array.Empty<WaveDefinition>());

            using (Harness harness = Harness.Create(
                content,
                new RunSettings(5d),
                world => new QueueSpawnOnTicksSystem(world, worker.Id, "scripted-gate", 1, 3),
                world => new DamageAllOnTickTwoSystem(world)))
            {
                harness.Start();

                harness.Step();
                RunEvent firstSpawn = SingleEvent(harness.Context, RunEventKind.EntitySpawned);

                harness.Step();
                RunEvent despawn = SingleEvent(harness.Context, RunEventKind.EntityDespawned);
                Assert.That(despawn.Tick, Is.EqualTo(2));
                Assert.That(despawn.Entity, Is.EqualTo(firstSpawn.Entity));
                Assert.That(despawn.Entity.Generation, Is.EqualTo(firstSpawn.Entity.Generation));
                Assert.That(despawn.DespawnReason, Is.EqualTo(DespawnReason.Death));
                Assert.That(harness.World.Entities.IsAlive(firstSpawn.Entity), Is.False);

                harness.Step();
                RunEvent secondSpawn = SingleEvent(harness.Context, RunEventKind.EntitySpawned);
                Assert.That(secondSpawn.Entity.Index, Is.EqualTo(firstSpawn.Entity.Index));
                Assert.That(secondSpawn.Entity.Generation, Is.GreaterThan(firstSpawn.Entity.Generation));
                Assert.That(secondSpawn.Entity, Is.Not.EqualTo(firstSpawn.Entity));
                Assert.That(harness.World.Entities.IsAlive(secondSpawn.Entity), Is.True);
            }
        }

        [Test]
        public void BuildChargesDespawn_EventCarriesExactHandleAndReason()
        {
            HumanDefinition worker = Human("worker", 50, 1);
            GameRuntimeContent content = Content(worker, Array.Empty<WaveDefinition>());

            using (Harness harness = Harness.Create(content, new RunSettings(5d)))
            {
                harness.Start();
                EntityHandle entity = harness.World.SpawnHuman(worker.Id, 0);

                harness.Step();

                RunEvent despawn = SingleEvent(harness.Context, RunEventKind.EntityDespawned);
                Assert.That(despawn.Tick, Is.EqualTo(1));
                Assert.That(despawn.Entity, Is.EqualTo(entity));
                Assert.That(despawn.Entity.Generation, Is.EqualTo(entity.Generation));
                Assert.That(despawn.DespawnReason, Is.EqualTo(DespawnReason.BuildChargesExhausted));
                Assert.That(harness.World.Entities.IsAlive(entity), Is.False);
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
            }
        }

        [Test]
        public void MultipleTicksInOneFrame_AggregateAllEntityEventsInTickOrder()
        {
            HumanDefinition worker = Human("worker", 0, 0);
            GameRuntimeContent content = Content(worker, Array.Empty<WaveDefinition>());
            var settings = new RunSettings(5d);

            using (Harness harness = Harness.Create(
                content,
                settings,
                world => new QueueSpawnOnTicksSystem(world, worker.Id, "scripted-gate", 1, 2, 3)))
            {
                harness.Start();
                harness.Loop.AdvanceFrame(3d * settings.FixedDeltaSeconds);

                List<RunEvent> events = Events(harness.Context, RunEventKind.EntitySpawned);
                Assert.That(events.Count, Is.EqualTo(3));
                Assert.That(events[0].Tick, Is.EqualTo(1));
                Assert.That(events[1].Tick, Is.EqualTo(2));
                Assert.That(events[2].Tick, Is.EqualTo(3));
                for (int i = 0; i < events.Count; i++)
                {
                    Assert.That(events[i].Entity.IsValid, Is.True);
                    Assert.That(events[i].HumanId, Is.EqualTo(worker.Id));
                    Assert.That(events[i].SpawnPointId, Is.EqualTo("scripted-gate"));
                }

                Assert.That(harness.World.Entities.AliveCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void FaultOnLaterTick_AtomicallyDropsEarlierEntityEventsFromSameFrame()
        {
            HumanDefinition worker = Human("worker", 0, 0);
            GameRuntimeContent content = Content(worker, Array.Empty<WaveDefinition>());
            var settings = new RunSettings(5d);

            using (Harness harness = Harness.Create(
                content,
                settings,
                world => new QueueSpawnOnTicksSystem(world, worker.Id, "scripted-gate", 1),
                world => new ThrowOnTickTwoSystem()))
            {
                harness.Start();
                RunFrameResult result = harness.Loop.AdvanceFrame(2d * settings.FixedDeltaSeconds);

                Assert.That(result.FaultedThisFrame, Is.True);
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Faulted));
                Assert.That(harness.World.Entities.AliveCount, Is.EqualTo(1));
                Assert.That(CountEvents(harness.Context, RunEventKind.EntitySpawned), Is.Zero);
                Assert.That(CountEvents(harness.Context, RunEventKind.EntityDespawned), Is.Zero);
                Assert.That(harness.Context.PublishedPresentationEvents.Count, Is.EqualTo(1));
                Assert.That(harness.Context.PublishedPresentationEvents[0].Kind, Is.EqualTo(RunEventKind.RunFaulted));
            }
        }

        private static HumanDefinition Human(string id, int buildContribution, int buildCharges)
        {
            return new HumanDefinition(
                id,
                id,
                10f,
                1f,
                buildContribution,
                buildCharges,
                experienceReward: 1,
                buildTimeSeconds: 0f,
                moveMode: "builder");
        }

        private static WaveDefinition BurstWave(string humanId, string spawnPointId)
        {
            return new WaveDefinition(
                "opening",
                0f,
                0f,
                WaveSpawnMode.Burst,
                new[] { new PoolEntry(humanId, 1f) },
                1,
                1,
                0f,
                spawnPointId);
        }

        private static GameRuntimeContent Content(
            HumanDefinition human,
            IEnumerable<WaveDefinition> waves)
        {
            return new GameRuntimeContent(
                new HumanCatalog(new[] { human }),
                new WaveCatalog(waves),
                new ExperienceTable(100f),
                new BabelDefinition(new[] { 1 }, new[] { 0 }));
        }

        private static RunEvent SingleEvent(RunContext context, RunEventKind kind)
        {
            List<RunEvent> events = Events(context, kind);
            Assert.That(events.Count, Is.EqualTo(1), "Expected exactly one " + kind + " event.");
            return events[0];
        }

        private static int CountEvents(RunContext context, RunEventKind kind)
        {
            return Events(context, kind).Count;
        }

        private static List<RunEvent> Events(RunContext context, RunEventKind kind)
        {
            var result = new List<RunEvent>();
            for (int i = 0; i < context.PublishedPresentationEvents.Count; i++)
            {
                RunEvent current = context.PublishedPresentationEvents[i];
                if (current.Kind == kind) result.Add(current);
            }

            return result;
        }

        private sealed class QueueSpawnOnTicksSystem : IRunSystem
        {
            private readonly GameWorld _world;
            private readonly string _humanId;
            private readonly string _spawnPointId;
            private readonly long[] _ticks;

            public QueueSpawnOnTicksSystem(
                GameWorld world,
                string humanId,
                string spawnPointId,
                params long[] ticks)
            {
                _world = world;
                _humanId = humanId;
                _spawnPointId = spawnPointId;
                _ticks = ticks;
            }

            public RunSystemStage Stage => RunSystemStage.Encounter;
            public int Order => 50;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                for (int i = 0; i < _ticks.Length; i++)
                {
                    if (_ticks[i] != context.Clock.Tick) continue;
                    _world.Spawn.Add(new SpawnWork(_humanId, "scripted", _spawnPointId));
                    return;
                }
            }
        }

        private sealed class DamageAllOnTickTwoSystem : IRunSystem
        {
            private readonly GameWorld _world;

            public DamageAllOnTickTwoSystem(GameWorld world)
            {
                _world = world;
            }

            public RunSystemStage Stage => RunSystemStage.Abilities;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                if (context.Clock.Tick != 2) return;
                foreach (EntityHandle entity in _world.Entities)
                    _world.Damage.Add(new DamageWork(EntityHandle.Invalid, entity, 100f));
            }
        }

        private sealed class ThrowOnTickTwoSystem : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 50;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                if (context.Clock.Tick == 2)
                    throw new InvalidOperationException("entity-event-fault");
            }
        }

        private sealed class Harness : IDisposable
        {
            private Harness(RunContext context, GameWorld world, RunLoop loop)
            {
                Context = context;
                World = world;
                Loop = loop;
            }

            public RunContext Context { get; }
            public GameWorld World { get; }
            public RunLoop Loop { get; }

            public static Harness Create(
                GameRuntimeContent content,
                RunSettings settings,
                params Func<GameWorld, IRunSystem>[] extraSystemFactories)
            {
                var context = new RunContext(settings, new SeededRandomSource(19));
                GameSystemSet game = GameSystemFactory.Create(context, content);
                var systems = new List<IRunSystem>(game.Systems);
                for (int i = 0; i < extraSystemFactories.Length; i++)
                    systems.Add(extraSystemFactories[i](game.World));

                var simulation = new RunSimulation(context, systems);
                return new Harness(
                    context,
                    game.World,
                    new RunLoop(context, new RunController(context), simulation));
            }

            public void Start()
            {
                Context.EnqueueControlCommand(RunControlCommand.StartRun());
                Loop.AdvanceFrame(0d);
            }

            public void Step()
            {
                Loop.AdvanceFrame(Context.Settings.FixedDeltaSeconds);
            }

            public void Dispose() => Loop.Dispose();
        }
    }
}
