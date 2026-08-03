using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.World;

namespace Babel.Gameplay.Systems
{
    internal sealed class DamageResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public DamageResolutionSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.Combat;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            IReadOnlyList<DamageWork> items = _world.Damage.Items;
            for (int i = 0; i < items.Count; i++)
            {
                DamageWork work = items[i];
                if (!_world.Entities.IsAlive(work.Target) ||
                    !_world.Health.TryGet(work.Target, out HealthState health))
                    continue;
                _world.Health.Set(work.Target, health.ApplyDamage(work.Amount));
            }

            _world.Damage.ClearResolved();
        }
    }

    internal sealed class DeathResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;
        private readonly List<EntityHandle> _entities = new List<EntityHandle>();

        public DeathResolutionSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.Death;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            _world.Entities.CopyAliveTo(_entities);
            for (int i = 0; i < _entities.Count; i++)
            {
                EntityHandle entity = _entities[i];
                if (!_world.Health.TryGet(entity, out HealthState health) || !health.IsDead ||
                    !_world.Humans.TryGet(entity, out HumanState human))
                    continue;

                HumanDefinition definition = _world.Content.Humans.GetRequired(human.DefinitionId);
                _world.DeathRewards.Add(new DeathRewardWork(entity, human.DefinitionId, definition.ExperienceReward));
                _world.Despawn.Add(new DespawnWork(entity, DespawnReason.Death));
            }
        }
    }

    internal sealed class DespawnResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public DespawnResolutionSystem(GameWorld world, RunSystemStage stage, int order)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Stage = stage;
            Order = order;
        }

        public RunSystemStage Stage { get; }
        public int Order { get; }
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            IReadOnlyList<DespawnWork> items = _world.Despawn.Items;
            for (int i = 0; i < items.Count; i++)
            {
                DespawnWork work = items[i];
                if (!_world.Entities.IsAlive(work.Entity)) continue;
                context.PresentationEvents.Add(
                    RunEvent.EntityDespawned(context.Clock.Tick, work.Entity, work.Reason));
                _world.Entities.Destroy(work.Entity);
            }
            _world.Despawn.ClearResolved();
        }
    }

    internal sealed class BabelWorkResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public BabelWorkResolutionSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.BabelWork;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            bool changed = false;
            IReadOnlyList<BuildWork> items = _world.Build.Items;
            for (int i = 0; i < items.Count; i++)
            {
                BuildWork work = items[i];
                if (!_world.Entities.IsAlive(work.Source) ||
                    !_world.Health.TryGet(work.Source, out HealthState health) ||
                    health.IsDead ||
                    !_world.Builders.TryGet(work.Source, out BuilderState builder))
                    continue;

                if (!builder.HasPendingContribution ||
                    builder.LayerIndex != work.LayerIndex ||
                    builder.TargetPointIndex != work.PointIndex)
                    continue;

                if (builder.RemainingCharges <= 0 ||
                    _world.Babel.IsPointCompleted(work.LayerIndex, work.PointIndex))
                {
                    builder.ClearTarget();
                    _world.Builders.Set(work.Source, builder);
                    continue;
                }

                bool accepted = _world.Babel.TryApplyBuild(
                    work.LayerIndex,
                    work.PointIndex,
                    work.Amount);
                builder.ClearTarget();
                if (accepted)
                {
                    builder.RemainingCharges--;
                    changed = true;
                    if (builder.RemainingCharges == 0)
                        _world.Despawn.Add(new DespawnWork(work.Source, DespawnReason.BuildChargesExhausted));
                }

                _world.Builders.Set(work.Source, builder);
            }

            _world.Build.ClearResolved();
            if (!changed) return;

            if (_world.Babel.IsCompleted)
                context.MarkBabelCompleted();
            else
                context.SetBabelProgress(_world.Babel.Progress);
        }
    }

    internal sealed class ProgressionResolutionSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public ProgressionResolutionSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.Progression;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            IReadOnlyList<DeathRewardWork> items = _world.DeathRewards.Items;
            if (items.Count == 0)
            {
                _world.DeathRewards.ClearResolved();
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                _world.Progression.GrantExperience(items[i].Experience, _world.Content.Experience);
                context.RecordKill();
            }

            context.SetProgression(
                _world.Progression.Level,
                _world.Progression.ProgressToNextLevel);
            _world.DeathRewards.ClearResolved();
        }
    }
}
