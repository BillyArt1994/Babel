using System;

namespace Babel.Gameplay.RunFlow
{
    public sealed class RunSettings
    {
        public const int DefaultSimulationHz = 60;
        public const int DefaultBrainHz = 10;
        public const int DefaultReadModelHz = 10;
        public const int DefaultMaxStepsPerFrame = 12;

        public RunSettings(
            double durationSeconds,
            int simulationHz = DefaultSimulationHz,
            int brainHz = DefaultBrainHz,
            int readModelHz = DefaultReadModelHz,
            int maxStepsPerFrame = DefaultMaxStepsPerFrame)
        {
            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (simulationHz <= 0) throw new ArgumentOutOfRangeException(nameof(simulationHz));
            if (brainHz <= 0 || simulationHz % brainHz != 0) throw new ArgumentOutOfRangeException(nameof(brainHz));
            if (readModelHz <= 0 || simulationHz % readModelHz != 0) throw new ArgumentOutOfRangeException(nameof(readModelHz));
            if (maxStepsPerFrame <= 0) throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame));

            DurationSeconds = durationSeconds;
            SimulationHz = simulationHz;
            BrainHz = brainHz;
            ReadModelHz = readModelHz;
            MaxStepsPerFrame = maxStepsPerFrame;
        }

        public double DurationSeconds { get; }
        public int SimulationHz { get; }
        public int BrainHz { get; }
        public int ReadModelHz { get; }
        public int MaxStepsPerFrame { get; }
        public double FixedDeltaSeconds => 1d / SimulationHz;
        public int BrainIntervalTicks => SimulationHz / BrainHz;
        public int ReadModelIntervalTicks => SimulationHz / ReadModelHz;
    }
}
