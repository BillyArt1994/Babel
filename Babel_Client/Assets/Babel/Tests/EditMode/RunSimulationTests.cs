using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.RunFlow;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class RunSimulationTests
    {
        [Test]
        public void Systems_RunInStageThenOrderSequence()
        {
            var calls = new List<string>();
            using (Harness harness = Harness.Create(
                10d,
                new RecordingSystem("combat-2", RunSystemStage.Combat, 2, calls),
                new RecordingSystem("commands", RunSystemStage.GameplayCommands, 0, calls),
                new RecordingSystem("combat-1", RunSystemStage.Combat, 1, calls)))
            {
                harness.Start();
                harness.Loop.AdvanceFrame(1d / 60d);
            }

            Assert.That(calls, Is.EqualTo(new[] { "commands", "combat-1", "combat-2" }));
        }

        [Test]
        public void IntervalSystem_RunsAtTenHertzInsideSixtyHertzSimulation()
        {
            var calls = new List<string>();
            using (Harness harness = Harness.Create(
                10d,
                new RecordingSystem("brain", RunSystemStage.HumanBrain, 0, calls, 6)))
            {
                harness.Start();
                for (int i = 0; i < 12; i++) harness.Loop.AdvanceFrame(1d / 60d);
            }

            Assert.That(calls.Count, Is.EqualTo(2));
        }

        [Test]
        public void BabelCompletionBeatsTimerExpiryOnSameTick()
        {
            using (Harness harness = Harness.Create(
                1d / 60d,
                new CompleteBabelSystem()))
            {
                harness.Start();
                harness.Loop.AdvanceFrame(1d / 60d);

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
            }
        }

        [Test]
        public void TimerExpiryWithoutBabelCompletion_IsVictory()
        {
            using (Harness harness = Harness.Create(1d / 60d))
            {
                harness.Start();
                harness.Loop.AdvanceFrame(1d / 60d);

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Won));
                long tick = harness.Context.Clock.Tick;
                harness.Loop.AdvanceFrame(1d);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(tick));
            }
        }

        [Test]
        public void CommandEnqueuedDuringTick_IsVisibleOnNextTick()
        {
            var observer = new CommandObserverSystem();
            using (Harness harness = Harness.Create(10d, observer))
            {
                harness.Start();
                harness.Context.EnqueueGameplayCommand(GameplayCommand.CastAbility(1, Float2.Zero));
                harness.Loop.AdvanceFrame(1d / 60d);
                harness.Loop.AdvanceFrame(1d / 60d);
            }

            Assert.That(observer.CommandsPerTick, Is.EqualTo(new[] { 1, 1 }));
            Assert.That(observer.AbilityIds, Is.EqualTo(new[] { 1, 2 }));
        }

        private sealed class RecordingSystem : IRunSystem
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public RecordingSystem(string name, RunSystemStage stage, int order, List<string> calls, int interval = 1)
            {
                _name = name;
                Stage = stage;
                Order = order;
                _calls = calls;
                TickInterval = interval;
            }

            public RunSystemStage Stage { get; }
            public int Order { get; }
            public int TickInterval { get; }
            public void Step(RunContext context, double fixedDeltaSeconds) => _calls.Add(_name);
        }

        private sealed class CompleteBabelSystem : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.BabelWork;
            public int Order => 0;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) => context.MarkBabelCompleted();
        }

        private sealed class CommandObserverSystem : IRunSystem
        {
            public readonly List<int> CommandsPerTick = new List<int>();
            public readonly List<int> AbilityIds = new List<int>();

            public RunSystemStage Stage => RunSystemStage.GameplayCommands;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                CommandsPerTick.Add(context.GameplayCommands.Current.Count);
                for (int i = 0; i < context.GameplayCommands.Current.Count; i++)
                    AbilityIds.Add(context.GameplayCommands.Current[i].IntValue);

                if (context.Clock.Tick == 1)
                    context.EnqueueGameplayCommand(GameplayCommand.CastAbility(2, Float2.Zero));
            }
        }

        private sealed class Harness : System.IDisposable
        {
            private Harness(RunContext context, RunLoop loop)
            {
                Context = context;
                Loop = loop;
            }

            public RunContext Context { get; }
            public RunLoop Loop { get; }

            public static Harness Create(double duration, params IRunSystem[] systems)
            {
                var settings = new RunSettings(duration);
                var context = new RunContext(settings, new SeededRandomSource(1));
                var controller = new RunController(context);
                var simulation = new RunSimulation(context, systems);
                return new Harness(context, new RunLoop(context, controller, simulation));
            }

            public void Start()
            {
                Context.ControlCommands.Enqueue(RunControlCommand.StartRun());
                Loop.AdvanceFrame(0d);
            }

            public void Dispose() => Loop.Dispose();
        }
    }
}
