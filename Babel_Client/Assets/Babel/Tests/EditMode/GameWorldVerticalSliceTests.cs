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
    public sealed class GameWorldVerticalSliceTests
    {
        [Test]
        public void EncounterSpawn_NewHumanStartsParticipatingOnFollowingTick()
        {
            HumanDefinition worker = Human(
                "worker",
                buildContribution: 10,
                buildCharges: 2,
                buildTimeSeconds: 0f);
            GameRuntimeContent content = Content(
                new[] { worker },
                new[]
                {
                    new WaveDefinition(
                        "opening",
                        0f,
                        0f,
                        WaveSpawnMode.Burst,
                        new[] { new PoolEntry(worker.Id, 1f) },
                        1,
                        1,
                        0f,
                        "left")
                },
                new BabelDefinition(new[] { 1 }, new[] { 0 }));

            using (Harness harness = Harness.Create(content, new RunSettings(5d), new SeededRandomSource(11)))
            {
                harness.Start();
                harness.Step();

                Assert.That(harness.World.Entities.AliveCount, Is.EqualTo(1));
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.Zero);
                EntityHandle spawned = FirstEntity(harness.World);
                Assert.That(harness.World.Humans.TryGet(spawned, out HumanState human), Is.True);
                Assert.That(human.SpawnTick, Is.EqualTo(1));
                Assert.That(harness.World.Builders.TryGet(spawned, out BuilderState newborn), Is.True);
                Assert.That(newborn.HasTarget, Is.False);

                harness.Step();

                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.EqualTo(10));
                Assert.That(harness.World.Builders.TryGet(spawned, out BuilderState active), Is.True);
                Assert.That(active.RemainingCharges, Is.EqualTo(1));
            }
        }

        [Test]
        public void HumanBrain_ScoutPrioritizesGateway_WhileBuilderUsesRandomCandidate()
        {
            HumanDefinition builder = Human("builder", 1, 2, 1f);
            HumanDefinition scout = Human("scout", 1, 2, 1f, moveMode: "scout");
            GameRuntimeContent content = Content(
                new[] { builder, scout },
                Array.Empty<WaveDefinition>(),
                new BabelDefinition(new[] { 3, 1 }, new[] { 1, 0 }));

            using (Harness harness = Harness.Create(content, new RunSettings(5d), new MaximumRandomSource()))
            {
                harness.Start();
                EntityHandle builderEntity = harness.World.SpawnHuman(builder.Id, 0);
                EntityHandle scoutEntity = harness.World.SpawnHuman(scout.Id, 0);

                harness.Step();

                Assert.That(harness.World.Builders.TryGet(builderEntity, out BuilderState builderState), Is.True);
                Assert.That(harness.World.Builders.TryGet(scoutEntity, out BuilderState scoutState), Is.True);
                Assert.That(builderState.TargetPointIndex, Is.EqualTo(2));
                Assert.That(scoutState.TargetPointIndex, Is.EqualTo(0));
                Assert.That(harness.World.Babel.IsGateway(0, scoutState.TargetPointIndex), Is.True);
            }
        }

        [Test]
        public void BuildContribution_CommitsOnlyAfterBuildTime_ThenConsumesCharge()
        {
            HumanDefinition worker = Human("worker", 50, 1, 0.2f);
            GameRuntimeContent content = Content(
                new[] { worker },
                Array.Empty<WaveDefinition>(),
                new BabelDefinition(new[] { 1 }, new[] { 0 }));

            var settings = new RunSettings(5d, simulationHz: 10, brainHz: 10, readModelHz: 10);
            using (Harness harness = Harness.Create(content, settings, new SeededRandomSource(3)))
            {
                harness.Start();
                harness.World.SpawnHuman(worker.Id, 0);

                harness.Step();
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.Zero);
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Playing));

                harness.Step();
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.EqualTo(50));
                Assert.That(harness.World.Babel.IsCompleted, Is.True);
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
            }
        }

        [Test]
        public void ContestedCompletedTarget_ReleasesLoserWithoutCharge_AndReselectsNextTick()
        {
            HumanDefinition finisher = Human("finisher", 50, 1, 0f);
            HumanDefinition follower = Human("follower", 50, 2, 0f);
            GameRuntimeContent content = Content(
                new[] { finisher, follower },
                Array.Empty<WaveDefinition>(),
                new BabelDefinition(new[] { 2 }, new[] { 0 }));

            using (Harness harness = Harness.Create(content, new RunSettings(5d), new SeededRandomSource(5)))
            {
                harness.Start();
                EntityHandle first = harness.World.SpawnHuman(finisher.Id, 0);
                EntityHandle second = harness.World.SpawnHuman(follower.Id, 0);
                ForceTarget(harness.World, first, 0, 0);
                ForceTarget(harness.World, second, 0, 0);

                harness.Step();

                Assert.That(harness.World.Entities.IsAlive(first), Is.False);
                Assert.That(harness.World.Babel.IsPointCompleted(0, 0), Is.True);
                Assert.That(harness.World.Babel.IsPointCompleted(0, 1), Is.False);
                Assert.That(harness.World.Builders.TryGet(second, out BuilderState afterContest), Is.True);
                Assert.That(afterContest.RemainingCharges, Is.EqualTo(2));
                Assert.That(afterContest.HasTarget, Is.False);

                harness.Step();

                Assert.That(harness.World.Babel.IsPointCompleted(0, 1), Is.True);
                Assert.That(harness.World.Builders.TryGet(second, out BuilderState afterReselect), Is.True);
                Assert.That(afterReselect.RemainingCharges, Is.EqualTo(1));
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
            }
        }

        [Test]
        public void LethalDamageResolvesBeforeBabelWork_StaleBuilderCannotBuild_RewardOccursOnce()
        {
            HumanDefinition worker = Human(
                "worker",
                buildContribution: 50,
                buildCharges: 1,
                buildTimeSeconds: 0f,
                experienceReward: 10);
            GameRuntimeContent content = Content(
                new[] { worker },
                Array.Empty<WaveDefinition>(),
                new BabelDefinition(new[] { 1 }, new[] { 0 }),
                new ExperienceTable(10f));

            DamageOnFirstTickSystem damageSystem = null;
            using (Harness harness = Harness.Create(
                content,
                new RunSettings(5d),
                new SeededRandomSource(7),
                world => damageSystem = new DamageOnFirstTickSystem(world)))
            {
                harness.Start();
                EntityHandle victim = harness.World.SpawnHuman(worker.Id, 0);
                damageSystem.Target = victim;

                harness.Step();

                Assert.That(harness.World.Entities.IsAlive(victim), Is.False);
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.Zero);
                Assert.That(harness.Context.KillCount, Is.EqualTo(1));
                Assert.That(harness.World.Progression.TotalExperience, Is.EqualTo(10));
                Assert.That(harness.Context.Level, Is.EqualTo(2));

                harness.Step();

                Assert.That(harness.Context.KillCount, Is.EqualTo(1));
                Assert.That(harness.World.Progression.TotalExperience, Is.EqualTo(10));
                Assert.That(harness.World.Babel.GetPoint(0, 0).Progress, Is.Zero);
            }
        }

        [Test]
        public void FixedSeed_EmptyRunBuildsAllSixLayersAndLoses()
        {
            HumanDefinition worker = Human("worker", 50, 36, 0f);
            GameRuntimeContent content = Content(
                new[] { worker },
                new[]
                {
                    new WaveDefinition(
                        "solo",
                        0f,
                        0f,
                        WaveSpawnMode.Burst,
                        new[] { new PoolEntry(worker.Id, 1f) },
                        1,
                        1,
                        0f,
                        "left")
                },
                new BabelDefinition());

            using (Harness harness = Harness.Create(content, new RunSettings(10d), new SeededRandomSource(12345)))
            {
                harness.Start();
                for (int i = 0; i < 100 && harness.Context.Phase == RunPhase.Playing; i++)
                    harness.Step();

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
                Assert.That(harness.Context.BabelCompleted, Is.True);
                Assert.That(harness.World.Babel.IsCompleted, Is.True);
                Assert.That(harness.World.Babel.LayerCount, Is.EqualTo(6));
                Assert.That(harness.World.Babel.CompletedPointCount, Is.EqualTo(36));
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(37));
            }
        }

        private static HumanDefinition Human(
            string id,
            int buildContribution,
            int buildCharges,
            float buildTimeSeconds,
            string moveMode = "",
            int experienceReward = 0)
        {
            return new HumanDefinition(
                id,
                id,
                10f,
                1f,
                buildContribution,
                buildCharges,
                experienceReward,
                buildTimeSeconds: buildTimeSeconds,
                moveMode: moveMode);
        }

        private static GameRuntimeContent Content(
            IEnumerable<HumanDefinition> humans,
            IEnumerable<WaveDefinition> waves,
            BabelDefinition babel,
            ExperienceTable experience = null)
        {
            return new GameRuntimeContent(
                new HumanCatalog(humans),
                new WaveCatalog(waves),
                experience ?? new ExperienceTable(100f),
                babel);
        }

        private static EntityHandle FirstEntity(GameWorld world)
        {
            foreach (EntityHandle entity in world.Entities) return entity;
            return EntityHandle.Invalid;
        }

        private static void ForceTarget(GameWorld world, EntityHandle entity, int layer, int point)
        {
            Assert.That(world.Builders.TryGet(entity, out BuilderState state), Is.True);
            state.AssignTarget(layer, point, 0d);
            world.Builders.Set(entity, state);
        }

        private sealed class MaximumRandomSource : IRandomSource
        {
            public uint NextUInt() => uint.MaxValue;
            public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;
            public float NextFloat() => 0.999999f;
            public bool NextBool() => true;
        }

        private sealed class DamageOnFirstTickSystem : IRunSystem
        {
            private readonly GameWorld _world;

            public DamageOnFirstTickSystem(GameWorld world)
            {
                _world = world;
            }

            public EntityHandle Target { get; set; }
            public RunSystemStage Stage => RunSystemStage.Abilities;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                if (context.Clock.Tick == 1 && Target.IsValid)
                    _world.Damage.Add(new DamageWork(EntityHandle.Invalid, Target, 100f));
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
                IRandomSource random,
                params Func<GameWorld, IRunSystem>[] extraSystemFactories)
            {
                var context = new RunContext(settings, random);
                GameSystemSet game = GameSystemFactory.Create(context, content);
                var systems = new List<IRunSystem>(game.Systems);
                for (int i = 0; i < extraSystemFactories.Length; i++)
                    systems.Add(extraSystemFactories[i](game.World));

                var simulation = new RunSimulation(context, systems);
                var controller = new RunController(context);
                return new Harness(context, game.World, new RunLoop(context, controller, simulation));
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
