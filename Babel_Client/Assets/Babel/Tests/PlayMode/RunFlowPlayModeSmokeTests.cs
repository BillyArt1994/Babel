using System.Collections;
using System.Text.RegularExpressions;
using Babel.Bootstrap;
using Babel.Gameplay.RunFlow;
using Babel.Unity.Infrastructure.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Babel.Tests
{
    public sealed class RunFlowPlayModeSmokeTests
    {
        private GameObject _host;
        private RunDriver _driver;
        private GameCompositionRoot _composition;

        [SetUp]
        public void SetUp()
        {
            PresentationTimeScaleAdapter.ResetLegacy();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_driver != null)
                _driver.Detach();

            if (_composition != null)
            {
                _composition.Dispose();
                _composition = null;
            }

            if (_host != null)
                Object.Destroy(_host);

            _host = null;
            _driver = null;
            PresentationTimeScaleAdapter.ResetLegacy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RunRoot_StartPauseResumeSpeedOutcomeAndExit_Smoke()
        {
            _host = new GameObject("RunRoot PlayMode Smoke");
            _driver = _host.AddComponent<RunDriver>();
            RunRoot runRoot = _host.AddComponent<RunRoot>();

            RunContext context = runRoot.Context;
            Assert.That(context, Is.Not.Null);
            Assert.That(runRoot.Driver, Is.SameAs(_driver));
            Assert.That(_driver.IsInitialized, Is.True);

            yield return WaitForPhase(context, RunPhase.Playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));

            long beforePauseTick = context.Clock.Tick;
            _driver.Enqueue(RunControlCommand.Pause());
            yield return null;

            Assert.That(context.Phase, Is.EqualTo(RunPhase.Paused));
            Assert.That(context.Clock.Tick, Is.EqualTo(beforePauseTick));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));

            _driver.Enqueue(RunControlCommand.SetSpeed(RunSpeed.Four));
            yield return null;

            Assert.That(context.Phase, Is.EqualTo(RunPhase.Paused));
            Assert.That(context.Clock.Speed, Is.EqualTo(RunSpeed.Four));
            Assert.That(context.Clock.Tick, Is.EqualTo(beforePauseTick));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));

            _driver.Enqueue(RunControlCommand.Resume());
            yield return null;

            Assert.That(context.Phase, Is.EqualTo(RunPhase.Playing));
            Assert.That(context.Clock.Speed, Is.EqualTo(RunSpeed.Four));
            Assert.That(Time.timeScale, Is.EqualTo(4f).Within(0.001f));

            for (int i = 0; i < 30 && context.Clock.Tick == beforePauseTick; i++)
                yield return null;

            Assert.That(context.Clock.Tick, Is.GreaterThan(beforePauseTick));

            long beforeOutcomeTick = context.Clock.Tick;
            _driver.Enqueue(RunControlCommand.ResolveOutcome(RunOutcome.Victory));
            yield return null;

            Assert.That(context.Phase, Is.EqualTo(RunPhase.Won));
            Assert.That(context.Clock.Tick, Is.EqualTo(beforeOutcomeTick));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));
            Assert.That(ContainsEvent(context, RunEventKind.RunWon), Is.True);

            _driver.Enqueue(RunControlCommand.RequestReturnToMenu());
            yield return null;

            Assert.That(context.Phase, Is.EqualTo(RunPhase.Transitioning));
            Assert.That(context.ExitRequest, Is.EqualTo(RunExitRequest.ReturnToMenu));
            Assert.That(context.Clock.Tick, Is.EqualTo(beforeOutcomeTick));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));
            Assert.That(ContainsEvent(context, RunEventKind.ReturnToMenuRequested), Is.True);

            Object.Destroy(_host);
            _host = null;
            _driver = null;
            yield return null;

            Assert.That(context.IsDisposed, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator RunDriver_WhenSystemFaults_StopsTicksAndStillAcceptsExit()
        {
            var throwingSystem = new ThrowingSystem();
            var settings = new RunSettings(60d);

            _composition = new GameCompositionRoot(
                settings,
                seed: 37,
                systems: new IRunSystem[] { throwingSystem });

            _host = new GameObject("Fault PlayMode Smoke");
            _driver = _host.AddComponent<RunDriver>();
            _driver.Initialize(
                _composition.Loop,
                _composition.Context,
                _composition.PresentationTime);

            RunContext context = _composition.Context;
            bool sawFaultFrame = false;
            RunFrameResult faultFrame = default;

            _driver.FrameAdvanced += result =>
            {
                if (!result.FaultedThisFrame) return;
                sawFaultFrame = true;
                faultFrame = result;
            };

            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: fault-smoke"));

            _driver.Enqueue(RunControlCommand.StartRun());

            for (int i = 0; i < 60 && context.Phase != RunPhase.Faulted; i++)
                yield return null;

            Assert.That(sawFaultFrame, Is.True);
            Assert.That(faultFrame.FaultedThisFrame, Is.True);
            Assert.That(faultFrame.Steps, Is.Zero);
            Assert.That(faultFrame.Phase, Is.EqualTo(RunPhase.Faulted));
            Assert.That(context.Phase, Is.EqualTo(RunPhase.Faulted));
            Assert.That(context.Clock.Tick, Is.EqualTo(1));
            Assert.That(throwingSystem.Calls, Is.EqualTo(1));
            Assert.That(context.Fault, Is.Not.Null);
            Assert.That(context.Fault.SystemName, Does.Contain(nameof(ThrowingSystem)));
            Assert.That(context.Fault.Message, Is.EqualTo("fault-smoke"));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));
            Assert.That(ContainsEvent(context, RunEventKind.RunFaulted), Is.True);

            long faultTick = context.Clock.Tick;

            _driver.Enqueue(RunControlCommand.RequestRestart());
            yield return null;

            Assert.That(context.ExitRequest, Is.EqualTo(RunExitRequest.Restart));
            Assert.That(context.Phase, Is.EqualTo(RunPhase.Transitioning));
            Assert.That(context.Clock.Tick, Is.EqualTo(faultTick));
            Assert.That(throwingSystem.Calls, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero.Within(0.001f));
            Assert.That(ContainsEvent(context, RunEventKind.RestartRequested), Is.True);

            _driver.Detach();
            _composition.Dispose();
            _composition = null;

            Object.Destroy(_host);
            _host = null;
            _driver = null;
            yield return null;

            Assert.That(context.IsDisposed, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
        }

        private static IEnumerator WaitForPhase(
            RunContext context,
            RunPhase expected,
            int maximumFrames = 60)
        {
            for (int i = 0; i < maximumFrames && context.Phase != expected; i++)
                yield return null;

            Assert.That(
                context.Phase,
                Is.EqualTo(expected),
                "Run phase did not reach the expected state in time.");
        }

        private static bool ContainsEvent(RunContext context, RunEventKind expected)
        {
            for (int i = 0; i < context.PublishedPresentationEvents.Count; i++)
            {
                if (context.PublishedPresentationEvents[i].Kind == expected)
                    return true;
            }

            return false;
        }

        private sealed class ThrowingSystem : IRunSystem
        {
            public int Calls { get; private set; }
            public RunSystemStage Stage => RunSystemStage.Combat;
            public int Order => 0;
            public int TickInterval => 1;

            public void Step(RunContext context, double fixedDeltaSeconds)
            {
                Calls++;
                throw new System.InvalidOperationException("fault-smoke");
            }
        }
    }
}
