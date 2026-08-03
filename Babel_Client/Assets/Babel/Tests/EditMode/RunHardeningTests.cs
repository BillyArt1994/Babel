using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.RunFlow;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class RunHardeningTests
    {
        private const double FixedDelta = 1d / 60d;

        [Test]
        public void RunContext_WhenConstructed_PublishesTruthfulInitialReadModel()
        {
            using (var context = new RunContext(new RunSettings(90d), new SeededRandomSource(7)))
            {
                RunReadModel model = context.ReadModel.Current;
                Assert.That(model.Version, Is.EqualTo(1));
                Assert.That(model.Tick, Is.Zero);
                Assert.That(model.Phase, Is.EqualTo(RunPhase.Booting));
                Assert.That(model.Speed, Is.EqualTo(RunSpeed.One));
                Assert.That(model.RemainingSeconds, Is.EqualTo(90d));
                Assert.That(model.Level, Is.EqualTo(1));
                Assert.That(model.KillCount, Is.Zero);
                Assert.That(model.XpProgress, Is.Zero);
                Assert.That(model.BabelProgress, Is.Zero);
            }
        }

        [Test]
        public void DomainEvents_WhenFramePartitionChanges_AreConsumedExactlyOnNextSimulationTick()
        {
            var oneFrameObserver = new DomainObserverSystem();
            var splitFrameObserver = new DomainObserverSystem();

            using (Harness oneFrame = Harness.Create(new DomainEmitterSystem(), oneFrameObserver))
            using (Harness splitFrames = Harness.Create(new DomainEmitterSystem(), splitFrameObserver))
            {
                oneFrame.Start();
                splitFrames.Start();

                oneFrame.Loop.AdvanceFrame(3d * FixedDelta);
                for (int i = 0; i < 3; i++) splitFrames.Loop.AdvanceFrame(FixedDelta);

                Assert.That(oneFrame.Context.Clock.Tick, Is.EqualTo(3));
                Assert.That(splitFrames.Context.Clock.Tick, Is.EqualTo(3));
                Assert.That(oneFrameObserver.ConsumedTicks, Is.EqualTo(new[] { 2L }));
                Assert.That(splitFrameObserver.ConsumedTicks, Is.EqualTo(new[] { 2L }));
                Assert.That(oneFrameObserver.SourceIds, Is.EqualTo(new[] { 7 }));
                Assert.That(splitFrameObserver.SourceIds, Is.EqualTo(new[] { 7 }));
            }
        }

        [Test]
        public void PresentationStage_WhenRulesEndRun_ObservesTerminalPhaseInSameTick()
        {
            var probe = new PhaseProbeSystem();
            using (Harness harness = Harness.Create(new CompleteBabelSystem(), probe))
            {
                harness.Start();
                harness.Loop.AdvanceFrame(FixedDelta);

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Lost));
                Assert.That(probe.ObservedPhases, Is.EqualTo(new[] { RunPhase.Lost }));
                Assert.That(CountEvents(harness.Context.PublishedPresentationEvents, RunEventKind.RunLost), Is.EqualTo(1));
            }
        }

        [Test]
        public void Pause_WhenCommandsArePendingOrArriveWhileFrozen_DiscardsBeforeResume()
        {
            var observer = new GameplayCommandProbeSystem();
            using (Harness harness = Harness.Create(observer))
            {
                harness.Start();
                Assert.That(harness.Context.EnqueueGameplayCommand(GameplayCommand.CastAbility(101, Float2.Zero)), Is.True);
                harness.Context.ControlCommands.Enqueue(RunControlCommand.Pause());
                harness.Loop.AdvanceFrame(FixedDelta);

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Paused));
                Assert.That(harness.Context.GameplayCommands.PendingCount, Is.Zero);
                Assert.That(harness.Context.EnqueueGameplayCommand(GameplayCommand.CastAbility(202, Float2.Zero)), Is.False);

                harness.Context.ControlCommands.Enqueue(RunControlCommand.Resume());
                harness.Loop.AdvanceFrame(FixedDelta);
                Assert.That(observer.Counts, Is.EqualTo(new[] { 0 }));

                Assert.That(harness.Context.EnqueueGameplayCommand(GameplayCommand.CastAbility(303, Float2.Zero)), Is.True);
                harness.Loop.AdvanceFrame(FixedDelta);
                Assert.That(observer.Counts, Is.EqualTo(new[] { 0, 1 }));
                Assert.That(observer.AbilityIds, Is.EqualTo(new[] { 303 }));
            }
        }

        [Test]
        public void RunSimulationConstructor_WhenSystemsShareStageAndOrder_Throws()
        {
            using (var context = new RunContext(new RunSettings(10d), new SeededRandomSource(1)))
            {
                var exception = Assert.Throws<ArgumentException>(() =>
                    new RunSimulation(context, new IRunSystem[] { new DuplicateOrderA(), new DuplicateOrderB() }));

                Assert.That(exception.ParamName, Is.EqualTo("systems"));
                Assert.That(exception.Message, Does.Contain("Combat"));
                Assert.That(exception.Message, Does.Contain("10"));
                Assert.That(exception.Message, Does.Contain(typeof(DuplicateOrderA).FullName));
                Assert.That(exception.Message, Does.Contain(typeof(DuplicateOrderB).FullName));
            }
        }

        [TestCase(RunExitRequest.Restart, RunExitRequest.ReturnToMenu)]
        [TestCase(RunExitRequest.ReturnToMenu, RunExitRequest.Restart)]
        public void AdvanceFrame_WhenMultipleExitRequestsQueued_FirstWinsAndExecutesNoTick(
            RunExitRequest first,
            RunExitRequest second)
        {
            var probe = new CountingSystem();
            using (Harness harness = Harness.Create(probe))
            {
                harness.Start();
                long tickBefore = harness.Context.Clock.Tick;
                harness.Context.ControlCommands.Enqueue(ToCommand(first));
                harness.Context.ControlCommands.Enqueue(ToCommand(second));

                RunFrameResult result = harness.Loop.AdvanceFrame(1d);

                Assert.That(result.ExitRequest, Is.EqualTo(first));
                Assert.That(result.Phase, Is.EqualTo(RunPhase.Transitioning));
                Assert.That(result.Steps, Is.Zero);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(tickBefore));
                Assert.That(probe.Calls, Is.Zero);
                RunEventKind expected = first == RunExitRequest.Restart
                    ? RunEventKind.RestartRequested
                    : RunEventKind.ReturnToMenuRequested;
                Assert.That(CountEvents(harness.Context.PublishedPresentationEvents, expected), Is.EqualTo(1));

                harness.Loop.AdvanceFrame(5d);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(tickBefore));
                Assert.That(probe.Calls, Is.Zero);
                Assert.That(CountEvents(harness.Context.PublishedPresentationEvents, expected), Is.Zero);
            }
        }

        [Test]
        public void AdvanceFrame_WhenSystemThrows_AbortsPartialFrameAndFaultsWithoutRetryingSimulation()
        {
            var throwing = new ThrowingSystem();
            var never = new CountingSystem(RunSystemStage.Combat, 2);
            using (Harness harness = Harness.Create(new PartialWriteSystem(), throwing, never))
            {
                harness.Start();
                RunFrameResult result = harness.Loop.AdvanceFrame(FixedDelta);

                Assert.That(result.FaultedThisFrame, Is.True);
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Faulted));
                Assert.That(harness.Context.Fault, Is.Not.Null);
                Assert.That(harness.Context.Fault.Tick, Is.EqualTo(1));
                Assert.That(harness.Context.Fault.SystemName, Is.EqualTo(typeof(ThrowingSystem).FullName));
                Assert.That(harness.Context.Fault.Exception, Is.TypeOf<SentinelException>());
                Assert.That(throwing.Calls, Is.EqualTo(1));
                Assert.That(never.Calls, Is.Zero);
                Assert.That(harness.Context.PublishedPresentationEvents.Count, Is.EqualTo(1));
                Assert.That(harness.Context.PublishedPresentationEvents[0].Kind, Is.EqualTo(RunEventKind.RunFaulted));
                Assert.That(harness.Context.GameplayCommands.PendingCount, Is.Zero);
                Assert.That(harness.Context.DomainEvents.PendingCount, Is.Zero);
                Assert.That(harness.Context.GameplayCommands.IsTickOpen, Is.False);
                Assert.That(harness.Context.DomainEvents.IsTickOpen, Is.False);
                Assert.That(harness.Context.PresentationEvents.IsFrameOpen, Is.False);

                long failedTick = harness.Context.Clock.Tick;
                RunFrameResult frozen = harness.Loop.AdvanceFrame(5d);
                Assert.That(frozen.Steps, Is.Zero);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(failedTick));
                Assert.That(throwing.Calls, Is.EqualTo(1));

                harness.Context.ControlCommands.Enqueue(RunControlCommand.RequestReturnToMenu());
                RunFrameResult exit = harness.Loop.AdvanceFrame(0d);
                Assert.That(exit.ExitRequest, Is.EqualTo(RunExitRequest.ReturnToMenu));
                Assert.That(exit.Phase, Is.EqualTo(RunPhase.Transitioning));
                Assert.That(exit.Steps, Is.Zero);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(failedTick));
            }
        }

        [Test]
        public void ReadModel_WhenOnlyClockChanges_PublishesAtTenHertzAndAtMostOncePerRenderFrame()
        {
            using (Harness harness = Harness.Create())
            {
                harness.Start();
                long startedVersion = harness.Context.ReadModel.Current.Version;

                for (int i = 1; i <= 5; i++)
                {
                    harness.Loop.AdvanceFrame(FixedDelta);
                    Assert.That(harness.Context.ReadModel.Current.Version, Is.EqualTo(startedVersion));
                }

                harness.Loop.AdvanceFrame(FixedDelta);
                Assert.That(harness.Context.ReadModel.Current.Version, Is.EqualTo(startedVersion + 1));
                Assert.That(harness.Context.ReadModel.Current.Tick, Is.EqualTo(6));
                Assert.That(harness.Context.ReadModel.Current.RemainingSeconds, Is.EqualTo(10d - (6d * FixedDelta)).Within(1e-9d));

                for (int i = 7; i <= 11; i++)
                {
                    harness.Loop.AdvanceFrame(FixedDelta);
                    Assert.That(harness.Context.ReadModel.Current.Version, Is.EqualTo(startedVersion + 1));
                    Assert.That(harness.Context.ReadModel.Current.Tick, Is.EqualTo(6));
                }

                harness.Loop.AdvanceFrame(FixedDelta);
                Assert.That(harness.Context.ReadModel.Current.Version, Is.EqualTo(startedVersion + 2));
                Assert.That(harness.Context.ReadModel.Current.Tick, Is.EqualTo(12));
            }

            using (Harness catchUp = Harness.Create())
            {
                catchUp.Start();
                long startedVersion = catchUp.Context.ReadModel.Current.Version;
                catchUp.Loop.AdvanceFrame(12d * FixedDelta);
                Assert.That(catchUp.Context.Clock.Tick, Is.EqualTo(12));
                Assert.That(catchUp.Context.ReadModel.Current.Version, Is.EqualTo(startedVersion + 1));
                Assert.That(catchUp.Context.ReadModel.Current.Tick, Is.EqualTo(12));
            }
        }

        private static RunControlCommand ToCommand(RunExitRequest request)
        {
            return request == RunExitRequest.Restart
                ? RunControlCommand.RequestRestart()
                : RunControlCommand.RequestReturnToMenu();
        }

        private static int CountEvents(IReadOnlyList<RunEvent> events, RunEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++) if (events[i].Kind == kind) count++;
            return count;
        }

        private sealed class DomainEmitterSystem : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                if (context.Clock.Tick == 1)
                    context.EmitDomainEvent(new RunDomainEvent(RunDomainEventKind.EntityDied, 1, 7));
            }
        }

        private sealed class DomainObserverSystem : IRunSystem
        {
            public readonly List<long> ConsumedTicks = new List<long>();
            public readonly List<int> SourceIds = new List<int>();
            public RunSystemStage Stage => RunSystemStage.Death;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                for (int i = 0; i < context.DomainEvents.Current.Count; i++)
                {
                    ConsumedTicks.Add(context.Clock.Tick);
                    SourceIds.Add(context.DomainEvents.Current[i].SourceId);
                }
            }
        }

        private sealed class CompleteBabelSystem : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.BabelWork;
            public int Order => 0;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) => context.MarkBabelCompleted();
        }

        private sealed class PhaseProbeSystem : IRunSystem
        {
            public readonly List<RunPhase> ObservedPhases = new List<RunPhase>();
            public RunSystemStage Stage => RunSystemStage.Presentation;
            public int Order => 0;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) => ObservedPhases.Add(context.Phase);
        }

        private sealed class GameplayCommandProbeSystem : IRunSystem
        {
            public readonly List<int> Counts = new List<int>();
            public readonly List<int> AbilityIds = new List<int>();
            public RunSystemStage Stage => RunSystemStage.GameplayCommands;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                Counts.Add(context.GameplayCommands.Current.Count);
                for (int i = 0; i < context.GameplayCommands.Current.Count; i++)
                    AbilityIds.Add(context.GameplayCommands.Current[i].IntValue);
            }
        }

        private sealed class DuplicateOrderA : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 10;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) { }
        }

        private sealed class DuplicateOrderB : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 10;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) { }
        }

        private sealed class CountingSystem : IRunSystem
        {
            private readonly RunSystemStage _stage;
            private readonly int _order;

            public CountingSystem(RunSystemStage stage = RunSystemStage.Combat, int order = 0)
            {
                _stage = stage;
                _order = order;
            }

            public int Calls { get; private set; }
            public RunSystemStage Stage => _stage;
            public int Order => _order;
            public int TickInterval => 1;
            public void Step(RunContext context, double fixedDeltaSeconds) => Calls++;
        }

        private sealed class PartialWriteSystem : IRunSystem
        {
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                context.PresentationEvents.Add(new RunEvent(RunEventKind.SpeedChanged, context.Clock.Tick));
                context.EmitDomainEvent(new RunDomainEvent(RunDomainEventKind.DamageResolved, context.Clock.Tick, 1, 2, 3f));
                context.EnqueueGameplayCommand(GameplayCommand.CastAbility(9, Float2.Zero));
            }
        }

        private sealed class ThrowingSystem : IRunSystem
        {
            public int Calls { get; private set; }
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 1;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                Calls++;
                throw new SentinelException();
            }
        }

        private sealed class SentinelException : Exception { }

        private sealed class Harness : IDisposable
        {
            private Harness(RunContext context, RunLoop loop)
            {
                Context = context;
                Loop = loop;
            }

            public RunContext Context { get; }
            public RunLoop Loop { get; }

            public static Harness Create(params IRunSystem[] systems)
            {
                var context = new RunContext(new RunSettings(10d), new SeededRandomSource(1));
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
