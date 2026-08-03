using System;
using System.Collections.Generic;

namespace Babel.Gameplay.RunFlow
{
    public sealed class RunSystemExecutionException : Exception
    {
        public RunSystemExecutionException(long tick, string systemName, Exception innerException)
            : base("Run system failed at tick " + tick + ": " + systemName, innerException)
        {
            Tick = tick;
            SystemName = systemName;
        }

        public long Tick { get; }
        public string SystemName { get; }
    }

    public sealed class RunSimulation
    {
        private readonly RunContext _context;
        private readonly IRunSystem[] _systems;

        public RunSimulation(RunContext context, IEnumerable<IRunSystem> systems)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var list = systems == null ? new List<IRunSystem>() : new List<IRunSystem>(systems);
            var orderKeys = new Dictionary<string, Type>(StringComparer.Ordinal);

            for (int i = 0; i < list.Count; i++)
            {
                IRunSystem system = list[i];
                if (system == null) throw new ArgumentException("System collection contains null.", nameof(systems));
                if (!Enum.IsDefined(typeof(RunSystemStage), system.Stage))
                    throw new ArgumentOutOfRangeException(nameof(systems), "System has an invalid stage: " + system.GetType().FullName);
                if (system.TickInterval <= 0)
                    throw new ArgumentOutOfRangeException(nameof(systems), "System tick interval must be positive: " + system.GetType().FullName);

                string key = ((int)system.Stage).ToString() + ":" + system.Order.ToString();
                if (orderKeys.TryGetValue(key, out Type existing))
                    throw new ArgumentException(
                        "Duplicate run-system order " + system.Stage + "/" + system.Order + " for " + existing.FullName + " and " + system.GetType().FullName + ".",
                        nameof(systems));
                orderKeys.Add(key, system.GetType());
            }

            list.Sort(CompareSystems);
            _systems = list.ToArray();
        }

        public int SystemCount => _systems.Length;

        public bool Step()
        {
            if (_context.Phase != RunPhase.Playing) return false;

            _context.Clock.AdvanceTick();
            _context.GameplayCommands.BeginTick();
            _context.DomainEvents.BeginTick();
            bool rulesEvaluated = false;
            string activeSystem = "GameWorld.BeginTick";

            try
            {
                _context.BeginWorldTick();
                long tick = _context.Clock.Tick;
                for (int i = 0; i < _systems.Length; i++)
                {
                    IRunSystem system = _systems[i];
                    if (!rulesEvaluated && system.Stage > RunSystemStage.RunRules)
                    {
                        EvaluateRunRules();
                        rulesEvaluated = true;
                    }

                    if (tick % system.TickInterval != 0) continue;
                    activeSystem = system.GetType().FullName;
                    system.Step(_context, _context.Settings.FixedDeltaSeconds);
                }

                if (!rulesEvaluated) EvaluateRunRules();

                if (_context.Clock.Tick % _context.Settings.ReadModelIntervalTicks == 0)
                    _context.MarkReadModelSampleDue();

                activeSystem = "GameWorld.EndTick";
                _context.EndWorldTick();
                _context.DomainEvents.EndTick();
                _context.GameplayCommands.EndTick();
                return true;
            }
            catch (Exception exception)
            {
                _context.AbortWorldTick();
                _context.DomainEvents.AbortTick();
                _context.GameplayCommands.AbortTick();
                throw new RunSystemExecutionException(_context.Clock.Tick, activeSystem, exception);
            }
        }

        private void EvaluateRunRules()
        {
            if (_context.BabelCompleted)
                _context.TryEndRun(RunPhase.Lost);
            else if (_context.Clock.IsExpired)
                _context.TryEndRun(RunPhase.Won);
        }

        private static int CompareSystems(IRunSystem left, IRunSystem right)
        {
            int stage = left.Stage.CompareTo(right.Stage);
            return stage != 0 ? stage : left.Order.CompareTo(right.Order);
        }
    }
}
