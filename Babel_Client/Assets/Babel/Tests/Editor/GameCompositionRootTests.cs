using Babel.Bootstrap;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.World;
using Babel.Unity.Infrastructure.Time;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class GameCompositionRootTests
    {
        [SetUp]
        public void SetUp()
        {
            PresentationTimeScaleAdapter.ResetLegacy();
        }

        [TearDown]
        public void TearDown()
        {
            PresentationTimeScaleAdapter.ResetLegacy();
        }

        [Test]
        public void ExplicitRuntimeContent_ComposesWorldAndRunsGameplaySystems()
        {
            GameRuntimeContent content = CreateRuntimeContent();
            var settings = new RunSettings(5d);

            using (var composition = new GameCompositionRoot(
                settings,
                seed: 17,
                gameRuntimeContent: content))
            {
                Assert.That(composition.World, Is.Not.Null);
                Assert.That(composition.Simulation.SystemCount, Is.EqualTo(10));

                Start(composition);
                composition.Loop.AdvanceFrame(settings.FixedDeltaSeconds);

                Assert.That(composition.Context.Clock.Tick, Is.EqualTo(1));
                Assert.That(composition.World.Entities.AliveCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ExistingConstructorWithoutContent_RemainsRunFlowOnly()
        {
            var settings = new RunSettings(5d);
            using (var composition = new GameCompositionRoot(settings, seed: 23))
            {
                Assert.That(composition.World, Is.Null);
                Assert.That(composition.Simulation.SystemCount, Is.Zero);

                Start(composition);
                composition.Loop.AdvanceFrame(settings.FixedDeltaSeconds);

                Assert.That(composition.Context.Phase, Is.EqualTo(RunPhase.Playing));
                Assert.That(composition.Context.Clock.Tick, Is.EqualTo(1));
            }
        }

        [Test]
        public void Dispose_WithWorld_IsIdempotentAndContextOwnsWorldLifetime()
        {
            var composition = new GameCompositionRoot(
                new RunSettings(5d),
                seed: 31,
                gameRuntimeContent: CreateRuntimeContent());
            RunContext context = composition.Context;
            GameWorld world = composition.World;

            Assert.DoesNotThrow(composition.Dispose);
            Assert.That(context.IsDisposed, Is.True);
            Assert.That(world.Entities.IsDisposed, Is.True);
            Assert.DoesNotThrow(composition.Dispose);
        }

        private static void Start(GameCompositionRoot composition)
        {
            composition.Context.EnqueueControlCommand(RunControlCommand.StartRun());
            composition.Loop.AdvanceFrame(0d);
        }

        private static GameRuntimeContent CreateRuntimeContent()
        {
            var human = new HumanDefinition(
                "worker",
                "Worker",
                10f,
                1f,
                buildContribution: 1,
                buildCharges: 1,
                experienceReward: 1,
                buildTimeSeconds: 0f,
                moveMode: "builder");
            var humans = new HumanCatalog(new[] { human });
            var waves = new WaveCatalog(new[]
            {
                new WaveDefinition(
                    "opening",
                    0f,
                    0f,
                    WaveSpawnMode.Burst,
                    new[] { new PoolEntry(human.Id, 1f) },
                    1,
                    1,
                    0f,
                    "left")
            });

            return new GameRuntimeContent(
                humans,
                waves,
                new ExperienceTable(10f),
                new BabelDefinition(new[] { 1 }, new[] { 0 }));
        }
    }
}
