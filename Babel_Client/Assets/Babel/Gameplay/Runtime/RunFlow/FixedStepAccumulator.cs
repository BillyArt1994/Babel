using System;

namespace Babel.Gameplay.RunFlow
{
    public readonly struct FixedStepBatch
    {
        public FixedStepBatch(int steps, int droppedTicks, double remainderSeconds)
        {
            Steps = steps;
            DroppedTicks = droppedTicks;
            RemainderSeconds = remainderSeconds;
        }

        public int Steps { get; }
        public int DroppedTicks { get; }
        public double RemainderSeconds { get; }
    }

    public sealed class FixedStepAccumulator
    {
        private readonly double _fixedDeltaSeconds;
        private readonly int _maxStepsPerFrame;
        private double _accumulatorSeconds;

        public FixedStepAccumulator(double fixedDeltaSeconds, int maxStepsPerFrame)
        {
            if (double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds) || fixedDeltaSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
            if (maxStepsPerFrame <= 0) throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame));

            _fixedDeltaSeconds = fixedDeltaSeconds;
            _maxStepsPerFrame = maxStepsPerFrame;
        }

        public double RemainderSeconds => _accumulatorSeconds;
        public long DroppedTicksTotal { get; private set; }

        public FixedStepBatch Consume(double unscaledDeltaSeconds, RunSpeed speed, bool simulationRunning)
        {
            if (double.IsNaN(unscaledDeltaSeconds) || double.IsInfinity(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));

            int multiplier = (int)speed;
            if (multiplier != 1 && multiplier != 2 && multiplier != 4)
                throw new ArgumentOutOfRangeException(nameof(speed));

            if (!simulationRunning || unscaledDeltaSeconds == 0d)
                return new FixedStepBatch(0, 0, _accumulatorSeconds);

            _accumulatorSeconds += unscaledDeltaSeconds * multiplier;
            int requestedSteps = (int)Math.Floor((_accumulatorSeconds + 1e-12d) / _fixedDeltaSeconds);
            if (requestedSteps <= 0)
                return new FixedStepBatch(0, 0, _accumulatorSeconds);

            _accumulatorSeconds -= requestedSteps * _fixedDeltaSeconds;
            if (_accumulatorSeconds < 0d && _accumulatorSeconds > -1e-10d)
                _accumulatorSeconds = 0d;

            int steps = Math.Min(requestedSteps, _maxStepsPerFrame);
            int dropped = requestedSteps - steps;
            DroppedTicksTotal += dropped;
            return new FixedStepBatch(steps, dropped, _accumulatorSeconds);
        }

        public void Reset()
        {
            _accumulatorSeconds = 0d;
            DroppedTicksTotal = 0;
        }
    }
}
