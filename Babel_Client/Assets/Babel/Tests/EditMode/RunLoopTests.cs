using Babel.Foundation;
using Babel.Gameplay.RunFlow;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class RunLoopTests
    {
        [Test]
        public void ControlCommands_AreProcessedWhileSimulationIsFrozen()
        {
            using (RunHarness harness = RunHarness.Create())
            {
                harness.Context.ControlCommands.Enqueue(RunControlCommand.StartRun());
                harness.Loop.AdvanceFrame(0d);
                harness.Context.ControlCommands.Enqueue(RunControlCommand.Pause());
                harness.Loop.AdvanceFrame(1d);

                long pausedTick = harness.Context.Clock.Tick;
                long version = harness.Context.ReadModel.Current.Version;
                harness.Context.ControlCommands.Enqueue(RunControlCommand.SetSpeed(RunSpeed.Four));
                RunFrameResult result = harness.Loop.AdvanceFrame(10d);

                Assert.That(result.Steps, Is.Zero);
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(pausedTick));
                Assert.That(harness.Context.Clock.Speed, Is.EqualTo(RunSpeed.Four));
                Assert.That(harness.Context.ReadModel.Current.Version, Is.GreaterThan(version));
            }
        }

        [Test]
        public void UpgradeSelection_IsHandledWithoutAdvancingTick()
        {
            var handler = new AcceptUpgradeHandler();
            using (RunHarness harness = RunHarness.Create(handler))
            {
                harness.Context.ControlCommands.Enqueue(RunControlCommand.StartRun());
                harness.Loop.AdvanceFrame(0d);
                harness.Context.ControlCommands.Enqueue(RunControlCommand.BeginUpgradeChoice());
                harness.Loop.AdvanceFrame(0d);
                long frozenTick = harness.Context.Clock.Tick;

                harness.Context.ControlCommands.Enqueue(RunControlCommand.SelectUpgrade(2));
                harness.Loop.AdvanceFrame(0d);

                Assert.That(handler.SelectedIndex, Is.EqualTo(2));
                Assert.That(harness.Context.Clock.Tick, Is.EqualTo(frozenTick));
                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Playing));
            }
        }

        [Test]
        public void UpgradeChoiceWithoutHandler_IsIgnored()
        {
            using (RunHarness harness = RunHarness.Create())
            {
                harness.Context.ControlCommands.Enqueue(RunControlCommand.StartRun());
                harness.Loop.AdvanceFrame(0d);
                harness.Context.ControlCommands.Enqueue(RunControlCommand.BeginUpgradeChoice());
                harness.Loop.AdvanceFrame(0d);
                harness.Context.ControlCommands.Enqueue(RunControlCommand.SelectUpgrade(0));
                harness.Loop.AdvanceFrame(0d);

                Assert.That(harness.Context.Phase, Is.EqualTo(RunPhase.Playing));
                Assert.That(harness.Context.Clock.Tick, Is.Zero);
            }
        }

        private sealed class AcceptUpgradeHandler : IUpgradeSelectionHandler
        {
            public int SelectedIndex { get; private set; } = -1;

            public bool TrySelectUpgrade(int optionIndex, RunContext context)
            {
                SelectedIndex = optionIndex;
                return true;
            }
        }

        private sealed class RunHarness : System.IDisposable
        {
            private RunHarness(RunContext context, RunLoop loop)
            {
                Context = context;
                Loop = loop;
            }

            public RunContext Context { get; }
            public RunLoop Loop { get; }

            public static RunHarness Create(IUpgradeSelectionHandler handler = null)
            {
                var settings = new RunSettings(10d);
                var context = new RunContext(settings, new SeededRandomSource(1));
                var controller = new RunController(context, handler);
                var simulation = new RunSimulation(context, null);
                return new RunHarness(context, new RunLoop(context, controller, simulation));
            }

            public void Dispose() => Loop.Dispose();
        }
    }
}
