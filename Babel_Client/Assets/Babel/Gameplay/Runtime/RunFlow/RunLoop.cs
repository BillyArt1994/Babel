using System;

namespace Babel.Gameplay.RunFlow
{
    public readonly struct RunFrameResult
    {
        public RunFrameResult(
            int steps,
            int droppedTicks,
            int controlCommands,
            RunPhase phase,
            long readModelVersion,
            RunExitRequest exitRequest,
            bool faultedThisFrame)
        {
            Steps = steps;
            DroppedTicks = droppedTicks;
            ControlCommands = controlCommands;
            Phase = phase;
            ReadModelVersion = readModelVersion;
            ExitRequest = exitRequest;
            FaultedThisFrame = faultedThisFrame;
        }

        public int Steps { get; }
        public int DroppedTicks { get; }
        public int ControlCommands { get; }
        public RunPhase Phase { get; }
        public long ReadModelVersion { get; }
        public RunExitRequest ExitRequest { get; }
        public bool FaultedThisFrame { get; }
    }

    public sealed class RunLoop : IDisposable
    {
        private readonly RunContext _context;
        private readonly RunController _controller;
        private readonly RunSimulation _simulation;
        private readonly FixedStepAccumulator _accumulator;
        private bool _isDisposed;
        private bool _isFaulted;

        public RunLoop(RunContext context, RunController controller, RunSimulation simulation)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _accumulator = new FixedStepAccumulator(context.Settings.FixedDeltaSeconds, context.Settings.MaxStepsPerFrame);
        }

        public RunFrameResult AdvanceFrame(double unscaledDeltaSeconds)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(RunLoop));
            if (_isFaulted)
            {
                _context.PresentationEvents.BeginFrame();
                int frozenControls = _controller.ProcessControlCommands();
                PublishReadModelIfRequested();
                _context.PresentationEvents.PublishFrame();
                return CreateResult(0, 0, frozenControls, false);
            }

            _context.PresentationEvents.BeginFrame();
            int executedSteps = 0;
            int processedControls = 0;
            FixedStepBatch batch = default;

            try
            {
                processedControls = _controller.ProcessControlCommands();
                bool simulationRunning = _context.Phase == RunPhase.Playing &&
                                         _context.ExitRequest == RunExitRequest.None;
                batch = _accumulator.Consume(
                    unscaledDeltaSeconds,
                    _context.Clock.Speed,
                    simulationRunning);

                for (int i = 0; i < batch.Steps; i++)
                {
                    if (!_simulation.Step()) break;
                    executedSteps++;
                    if (_context.Phase != RunPhase.Playing ||
                        _context.ExitRequest != RunExitRequest.None) break;
                }

                PublishReadModelIfRequested();
                _context.PresentationEvents.PublishFrame();
                return CreateResult(executedSteps, batch.DroppedTicks, processedControls, false);
            }
            catch (Exception exception)
            {
                if (_context.PresentationEvents.IsFrameOpen)
                    _context.PresentationEvents.AbortFrame();

                _accumulator.Reset();
                var systemFault = exception as RunSystemExecutionException;
                string systemName = systemFault == null ? "RunLoop" : systemFault.SystemName;
                Exception root = systemFault == null ? exception : systemFault.InnerException ?? systemFault;
                _context.EnterFault(new RunFaultInfo(_context.Clock.Tick, systemName, root));

                _context.PresentationEvents.BeginFrame();
                _context.EmitFaultPresentationEvent();
                PublishReadModelIfRequested();
                _context.PresentationEvents.PublishFrame();
                _isFaulted = true;
                return CreateResult(executedSteps, 0, processedControls, true);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _accumulator.Reset();
            _context.Dispose();
            _isDisposed = true;
        }

        private void PublishReadModelIfRequested()
        {
            if (_context.ConsumeReadModelPublishRequest())
                _context.ReadModel.Publish(_context);
        }

        private RunFrameResult CreateResult(int steps, int droppedTicks, int controls, bool faultedThisFrame)
        {
            return new RunFrameResult(
                steps,
                droppedTicks,
                controls,
                _context.Phase,
                _context.ReadModel.Current.Version,
                _context.ExitRequest,
                faultedThisFrame);
        }
    }
}
