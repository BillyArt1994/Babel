using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.Systems;
using Babel.Gameplay.World;
using Babel.Unity.Infrastructure.Time;

namespace Babel.Bootstrap
{
    public sealed class GameCompositionRoot : IDisposable
    {
        private readonly List<IDisposable> _ownedRuntimeObjects = new List<IDisposable>();
        private bool _isDisposed;

        public GameCompositionRoot(
            RunSettings settings,
            int seed,
            IEnumerable<IRunSystem> systems = null,
            IUpgradeSelectionHandler upgradeSelectionHandler = null,
            IEnumerable<IDisposable> additionalOwnedObjects = null,
            GameRuntimeContent gameRuntimeContent = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Seed = seed;

            var systemList = systems == null ? new List<IRunSystem>() : new List<IRunSystem>(systems);
            Context = new RunContext(settings, new SeededRandomSource(seed));
            if (gameRuntimeContent != null)
            {
                GameSystemSet gameplay = GameSystemFactory.Create(Context, gameRuntimeContent);
                World = gameplay.World;
                systemList.AddRange(gameplay.Systems);
            }

            Controller = new RunController(Context, upgradeSelectionHandler);
            Simulation = new RunSimulation(Context, systemList);
            Loop = new RunLoop(Context, Controller, Simulation);
            PresentationTime = new PresentationTimeScaleAdapter();

            AddOwnedRange(additionalOwnedObjects);
            for (int i = 0; i < systemList.Count; i++) AddOwned(systemList[i] as IDisposable);
            AddOwned(upgradeSelectionHandler as IDisposable);
        }

        public RunSettings Settings { get; }
        public int Seed { get; }
        public RunContext Context { get; }
        public GameWorld World { get; }
        public RunController Controller { get; }
        public RunSimulation Simulation { get; }
        public RunLoop Loop { get; }
        public PresentationTimeScaleAdapter PresentationTime { get; }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> errors = null;

            for (int i = _ownedRuntimeObjects.Count - 1; i >= 0; i--)
            {
                try { _ownedRuntimeObjects[i].Dispose(); }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
            }
            _ownedRuntimeObjects.Clear();

            try { Loop.Dispose(); }
            catch (Exception exception)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(exception);
            }

            try { PresentationTime.Dispose(); }
            catch (Exception exception)
            {
                if (errors == null) errors = new List<Exception>();
                errors.Add(exception);
            }

            if (errors != null) throw new AggregateException("One or more run objects failed to dispose.", errors);
        }

        private void AddOwnedRange(IEnumerable<IDisposable> objects)
        {
            if (objects == null) return;
            foreach (IDisposable value in objects) AddOwned(value);
        }

        private void AddOwned(IDisposable value)
        {
            if (value == null) return;
            for (int i = 0; i < _ownedRuntimeObjects.Count; i++)
                if (ReferenceEquals(_ownedRuntimeObjects[i], value)) return;
            _ownedRuntimeObjects.Add(value);
        }
    }
}
