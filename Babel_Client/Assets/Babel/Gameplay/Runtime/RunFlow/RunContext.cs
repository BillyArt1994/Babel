using System;
using System.Collections.Generic;
using Babel.Foundation;

namespace Babel.Gameplay.RunFlow
{
    public sealed class RunContext : IDisposable
    {
        private bool _isDisposed;
        private bool _urgentReadModelDirty;
        private bool _readModelSampleDue;
        private readonly FrameEventBuffer<RunEvent> _presentationEvents;
        private IRunWorldLifecycle _worldLifecycle;

        public RunContext(RunSettings settings, IRandomSource random)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Clock = new RunClock(settings);
            ControlCommands = new RunControlCommandQueue();
            GameplayCommands = new TickCommandBuffer<GameplayCommand>();
            DomainEvents = new TickEventBuffer<RunDomainEvent>();
            _presentationEvents = new FrameEventBuffer<RunEvent>();
            ReadModel = new RunReadModelStore();
            Phase = RunPhase.Booting;
            Level = 1;
            _urgentReadModelDirty = true;
            ReadModel.Publish(this);
            ClearReadModelDirty();
        }

        public RunSettings Settings { get; }
        internal IRandomSource Random { get; }
        public RunClock Clock { get; }
        internal RunControlCommandQueue ControlCommands { get; }
        internal TickCommandBuffer<GameplayCommand> GameplayCommands { get; }
        internal TickEventBuffer<RunDomainEvent> DomainEvents { get; }
        internal FrameEventBuffer<RunEvent> PresentationEvents => _presentationEvents;
        public IReadOnlyList<RunEvent> PublishedPresentationEvents => _presentationEvents.Published;
        public RunReadModelStore ReadModel { get; }
        public RunPhase Phase { get; private set; }
        public int KillCount { get; private set; }
        public int Level { get; private set; }
        public float XpProgress { get; private set; }
        public float BabelProgress { get; private set; }
        public bool BabelCompleted { get; private set; }
        public RunExitRequest ExitRequest { get; private set; }
        public RunFaultInfo Fault { get; private set; }
        public bool IsDisposed => _isDisposed;
        public bool IsTerminal => Phase == RunPhase.Won || Phase == RunPhase.Lost;
        public bool IsStopped => IsTerminal || Phase == RunPhase.Transitioning || Phase == RunPhase.Faulted || Phase == RunPhase.Disposed;

        public void EnqueueControlCommand(RunControlCommand command)
        {
            EnsureNotDisposed();
            ControlCommands.Enqueue(command);
        }

        public bool EnqueueGameplayCommand(GameplayCommand command)
        {
            EnsureNotDisposed();
            if (Phase != RunPhase.Playing || ExitRequest != RunExitRequest.None || Fault != null) return false;
            GameplayCommands.Enqueue(command);
            return true;
        }

        internal void EmitDomainEvent(RunDomainEvent domainEvent)
        {
            EnsureNotDisposed();
            DomainEvents.Add(domainEvent);
        }

        internal void RecordKill()
        {
            EnsureNotDisposed();
            KillCount++;
        }

        internal void SetProgression(int level, float xpProgress)
        {
            EnsureNotDisposed();
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (!IsUnitInterval(xpProgress)) throw new ArgumentOutOfRangeException(nameof(xpProgress));
            Level = level;
            XpProgress = xpProgress;
        }

        internal void SetBabelProgress(float progress)
        {
            EnsureNotDisposed();
            if (!IsUnitInterval(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
            BabelProgress = progress;
        }

        internal void MarkBabelCompleted()
        {
            EnsureNotDisposed();
            BabelCompleted = true;
            BabelProgress = 1f;
        }

        internal void ResetForStart()
        {
            Clock.Reset();
            GameplayCommands.Clear();
            DomainEvents.Clear();
            _worldLifecycle?.Reset();
            KillCount = 0;
            Level = 1;
            XpProgress = 0f;
            BabelProgress = 0f;
            BabelCompleted = false;
            ExitRequest = RunExitRequest.None;
            Fault = null;
            MarkUrgentReadModelDirty();
        }

        internal void SetPhase(RunPhase phase)
        {
            if (Phase == phase) return;
            RunPhase previous = Phase;
            Phase = phase;
            if (phase != RunPhase.Playing) GameplayCommands.ClearPending();
            _presentationEvents.Add(new RunEvent(RunEventKind.PhaseChanged, Clock.Tick, (int)previous, (int)phase));
            MarkUrgentReadModelDirty();
        }

        internal void SetSpeed(RunSpeed speed)
        {
            if (Clock.Speed == speed) return;
            Clock.SetSpeed(speed);
            _presentationEvents.Add(new RunEvent(RunEventKind.SpeedChanged, Clock.Tick, (int)speed));
            MarkUrgentReadModelDirty();
        }

        internal bool TryRequestExit(RunExitRequest request)
        {
            if (request == RunExitRequest.None) throw new ArgumentOutOfRangeException(nameof(request));
            if (ExitRequest != RunExitRequest.None || Phase == RunPhase.Disposed || Phase == RunPhase.Transitioning) return false;

            ExitRequest = request;
            SetPhase(RunPhase.Transitioning);
            _presentationEvents.Add(new RunEvent(
                request == RunExitRequest.Restart ? RunEventKind.RestartRequested : RunEventKind.ReturnToMenuRequested,
                Clock.Tick));
            return true;
        }

        internal bool TryEndRun(RunPhase outcome)
        {
            if (IsStopped) return false;
            if (outcome != RunPhase.Won && outcome != RunPhase.Lost)
                throw new ArgumentOutOfRangeException(nameof(outcome));

            SetPhase(outcome);
            _presentationEvents.Add(new RunEvent(outcome == RunPhase.Won ? RunEventKind.RunWon : RunEventKind.RunLost, Clock.Tick));
            return true;
        }

        internal void EnterFault(RunFaultInfo fault)
        {
            if (Fault != null || Phase == RunPhase.Disposed) return;
            Fault = fault ?? throw new ArgumentNullException(nameof(fault));
            GameplayCommands.AbortTick();
            DomainEvents.AbortTick();
            _worldLifecycle?.AbortTick();
            ControlCommands.Clear();
            ExitRequest = RunExitRequest.None;
            Phase = RunPhase.Faulted;
            MarkUrgentReadModelDirty();
        }

        internal void EmitFaultPresentationEvent()
        {
            _presentationEvents.Add(new RunEvent(RunEventKind.RunFaulted, Clock.Tick));
        }

        internal void MarkUrgentReadModelDirty() => _urgentReadModelDirty = true;

        internal void MarkReadModelSampleDue()
        {
            _readModelSampleDue = true;
        }

        internal bool ConsumeReadModelPublishRequest()
        {
            if (!_urgentReadModelDirty && !_readModelSampleDue) return false;
            ClearReadModelDirty();
            return true;
        }

        internal void AttachWorld(IRunWorldLifecycle worldLifecycle)
        {
            EnsureNotDisposed();
            if (worldLifecycle == null) throw new ArgumentNullException(nameof(worldLifecycle));
            if (_worldLifecycle != null)
                throw new InvalidOperationException("A run world is already attached to this context.");
            if (Phase != RunPhase.Booting)
                throw new InvalidOperationException("A run world must be attached before the run starts.");

            _worldLifecycle = worldLifecycle;
        }

        internal void BeginWorldTick() => _worldLifecycle?.BeginTick();
        internal void EndWorldTick() => _worldLifecycle?.EndTick();
        internal void AbortWorldTick() => _worldLifecycle?.AbortTick();

        public void Dispose()
        {
            if (_isDisposed) return;
            ControlCommands.Clear();
            GameplayCommands.Clear();
            DomainEvents.Clear();
            _presentationEvents.Clear();
            _worldLifecycle?.Dispose();
            _worldLifecycle = null;
            Phase = RunPhase.Disposed;
            _isDisposed = true;
        }

        private void ClearReadModelDirty()
        {
            _urgentReadModelDirty = false;
            _readModelSampleDue = false;
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(RunContext));
        }

        private static bool IsUnitInterval(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }
    }
}
