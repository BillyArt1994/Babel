using System;

namespace Babel.Gameplay.RunFlow
{
    public sealed class RunClock
    {
        private readonly double _durationSeconds;
        private readonly double _fixedDeltaSeconds;

        public RunClock(RunSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _durationSeconds = settings.DurationSeconds;
            _fixedDeltaSeconds = settings.FixedDeltaSeconds;
            Reset();
        }

        public long Tick { get; private set; }
        public double RemainingSeconds { get; private set; }
        public RunSpeed Speed { get; private set; }
        public bool IsExpired => RemainingSeconds <= 0d;

        internal void AdvanceTick()
        {
            Tick++;
            RemainingSeconds = Math.Max(0d, RemainingSeconds - _fixedDeltaSeconds);
        }

        internal void SetSpeed(RunSpeed speed)
        {
            ValidateSpeed(speed);
            Speed = speed;
        }

        internal void Reset()
        {
            Tick = 0;
            RemainingSeconds = _durationSeconds;
            Speed = RunSpeed.One;
        }

        internal static void ValidateSpeed(RunSpeed speed)
        {
            int multiplier = (int)speed;
            if (multiplier != 1 && multiplier != 2 && multiplier != 4)
                throw new ArgumentOutOfRangeException(nameof(speed));
        }
    }
}
