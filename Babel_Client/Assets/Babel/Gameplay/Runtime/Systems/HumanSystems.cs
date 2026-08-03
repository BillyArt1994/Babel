using System;
using System.Collections.Generic;
using Babel.Foundation;
using Babel.Gameplay.Content;
using Babel.Gameplay.RunFlow;
using Babel.Gameplay.World;

namespace Babel.Gameplay.Systems
{
    internal sealed class HumanBrainSystem : IRunSystem
    {
        private readonly GameWorld _world;
        private readonly List<int> _candidates = new List<int>();

        public HumanBrainSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.HumanBrain;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            foreach (EntityHandle entity in _world.Entities)
            {
                if (!_world.Humans.TryGet(entity, out HumanState human) ||
                    !_world.Health.TryGet(entity, out HealthState health) ||
                    !_world.Builders.TryGet(entity, out BuilderState builder))
                    continue;
                if (human.SpawnTick >= context.Clock.Tick || health.IsDead || builder.RemainingCharges <= 0)
                    continue;

                if (builder.HasTarget && !TargetIsIncomplete(builder))
                    builder.ClearTarget();
                if (builder.HasPendingContribution)
                {
                    _world.Builders.Set(entity, builder);
                    continue;
                }
                if (builder.HasTarget)
                {
                    _world.Builders.Set(entity, builder);
                    continue;
                }

                while (builder.LayerIndex < _world.Babel.LayerCount &&
                       _world.Babel.IsLayerCompleted(builder.LayerIndex))
                    builder.LayerIndex++;

                if (builder.LayerIndex >= _world.Babel.LayerCount)
                {
                    _world.Builders.Set(entity, builder);
                    continue;
                }

                int pointIndex = SelectTarget(context, builder);
                if (pointIndex < 0)
                {
                    _world.Builders.Set(entity, builder);
                    continue;
                }

                HumanDefinition definition = _world.Content.Humans.GetRequired(human.DefinitionId);
                builder.AssignTarget(builder.LayerIndex, pointIndex, definition.BuildTimeSeconds);
                _world.Builders.Set(entity, builder);
            }
        }

        private int SelectTarget(RunContext context, BuilderState builder)
        {
            if (builder.WorkMode == HumanWorkMode.Scout)
            {
                _world.Babel.CopyIncompletePoints(builder.LayerIndex, true, _candidates);
                if (_candidates.Count > 0) return _candidates[0];
            }

            _world.Babel.CopyIncompletePoints(builder.LayerIndex, false, _candidates);
            if (_candidates.Count == 0) return -1;
            return _candidates[context.Random.NextInt(0, _candidates.Count)];
        }

        private bool TargetIsIncomplete(BuilderState builder)
        {
            if (builder.LayerIndex < 0 || builder.LayerIndex >= _world.Babel.LayerCount) return false;
            if (builder.TargetPointIndex < 0 ||
                builder.TargetPointIndex >= _world.Babel.GetPointCount(builder.LayerIndex))
                return false;
            return !_world.Babel.IsPointCompleted(builder.LayerIndex, builder.TargetPointIndex);
        }
    }

    internal sealed class HumanBuildIntentSystem : IRunSystem
    {
        private readonly GameWorld _world;

        public HumanBuildIntentSystem(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RunSystemStage Stage => RunSystemStage.NavigationAndWorkIntent;
        public int Order => 0;
        public int TickInterval => 1;

        public void Step(RunContext context, double fixedDeltaSeconds)
        {
            foreach (EntityHandle entity in _world.Entities)
            {
                if (!_world.Humans.TryGet(entity, out HumanState human) ||
                    !_world.Health.TryGet(entity, out HealthState health) ||
                    !_world.Builders.TryGet(entity, out BuilderState builder))
                    continue;
                if (human.SpawnTick >= context.Clock.Tick || health.IsDead ||
                    builder.RemainingCharges <= 0 || !builder.HasTarget || builder.HasPendingContribution)
                    continue;

                if (builder.LayerIndex < 0 || builder.LayerIndex >= _world.Babel.LayerCount ||
                    builder.TargetPointIndex < 0 ||
                    builder.TargetPointIndex >= _world.Babel.GetPointCount(builder.LayerIndex) ||
                    _world.Babel.IsPointCompleted(builder.LayerIndex, builder.TargetPointIndex))
                {
                    builder.ClearTarget();
                    _world.Builders.Set(entity, builder);
                    continue;
                }

                builder.RemainingBuildSeconds -= fixedDeltaSeconds;
                if (builder.RemainingBuildSeconds > 0.000001d)
                {
                    _world.Builders.Set(entity, builder);
                    continue;
                }

                HumanDefinition definition = _world.Content.Humans.GetRequired(human.DefinitionId);
                _world.Build.Add(new BuildWork(
                    entity,
                    builder.LayerIndex,
                    builder.TargetPointIndex,
                    definition.BuildContribution));
                builder.RemainingBuildSeconds = 0d;
                builder.HasPendingContribution = true;
                _world.Builders.Set(entity, builder);
            }
        }
    }
}
