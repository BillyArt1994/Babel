using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.World;

namespace Babel.Gameplay.Systems
{
    public sealed class GameSystemSet
    {
        internal GameSystemSet(GameWorld world, IRunSystem[] systems)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Systems = Array.AsReadOnly(systems ?? throw new ArgumentNullException(nameof(systems)));
        }

        public GameWorld World { get; }
        public ReadOnlyCollection<IRunSystem> Systems { get; }
    }

    /// <summary>Single composition seam for injecting the gameplay vertical slice.</summary>
    public static class GameSystemFactory
    {
        public static GameSystemSet Create(RunContext context, GameRuntimeContent content)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (content == null) throw new ArgumentNullException(nameof(content));

            var world = new GameWorld(content);
            var systems = new IRunSystem[]
            {
                new HumanBrainSystem(world),
                new HumanBuildIntentSystem(world),
                new DamageResolutionSystem(world),
                new DeathResolutionSystem(world),
                new DespawnResolutionSystem(world, RunSystemStage.Death, 100),
                new BabelWorkResolutionSystem(world),
                new DespawnResolutionSystem(world, RunSystemStage.BabelWork, 100),
                new ProgressionResolutionSystem(world),
                new EncounterSystem(world),
                new SpawnResolutionSystem(world)
            };

            try
            {
                context.AttachWorld(world);
                return new GameSystemSet(world, systems);
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }
    }
}
