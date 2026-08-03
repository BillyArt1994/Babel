namespace Babel.Gameplay.RunFlow
{
    public readonly struct RunReadModel
    {
        public RunReadModel(
            long version,
            long tick,
            RunPhase phase,
            RunSpeed speed,
            double remainingSeconds,
            int killCount,
            int level,
            float xpProgress,
            float babelProgress,
            RunExitRequest exitRequest,
            string faultMessage)
        {
            Version = version;
            Tick = tick;
            Phase = phase;
            Speed = speed;
            RemainingSeconds = remainingSeconds;
            KillCount = killCount;
            Level = level;
            XpProgress = xpProgress;
            BabelProgress = babelProgress;
            ExitRequest = exitRequest;
            FaultMessage = faultMessage;
        }

        public long Version { get; }
        public long Tick { get; }
        public RunPhase Phase { get; }
        public RunSpeed Speed { get; }
        public double RemainingSeconds { get; }
        public int KillCount { get; }
        public int Level { get; }
        public float XpProgress { get; }
        public float BabelProgress { get; }
        public RunExitRequest ExitRequest { get; }
        public string FaultMessage { get; }
    }

    public sealed class RunReadModelStore
    {
        public RunReadModel Current { get; private set; }

        internal void Publish(RunContext context)
        {
            long version = Current.Version + 1;
            Current = new RunReadModel(
                version,
                context.Clock.Tick,
                context.Phase,
                context.Clock.Speed,
                context.Clock.RemainingSeconds,
                context.KillCount,
                context.Level,
                context.XpProgress,
                context.BabelProgress,
                context.ExitRequest,
                context.Fault == null ? null : context.Fault.Message);
        }
    }
}
