using System;
using System.Collections.Generic;
using Babel.Gameplay.Content;

namespace Babel.Gameplay.World
{
    public readonly struct BabelBuildPointState
    {
        public BabelBuildPointState(int layerIndex, int pointIndex, int progress, int requiredProgress, bool isGateway)
        {
            LayerIndex = layerIndex;
            PointIndex = pointIndex;
            Progress = progress;
            RequiredProgress = requiredProgress;
            IsGateway = isGateway;
        }

        public int LayerIndex { get; }
        public int PointIndex { get; }
        public int Progress { get; }
        public int RequiredProgress { get; }
        public bool IsGateway { get; }
        public bool IsCompleted => Progress >= RequiredProgress;
    }

    /// <summary>
    /// Bottom-to-top tower state. Until authored point metadata is available, the first
    /// GatewayCount points of a layer are treated as gateways.
    /// </summary>
    public sealed class BabelState
    {
        private readonly BabelDefinition _definition;
        private readonly int _requiredProgress;
        private readonly int[][] _progressByLayer;
        private readonly int _totalRequiredProgress;
        private readonly int _totalPointCount;
        private int _totalProgress;
        private int _completedPointCount;

        public BabelState(BabelDefinition definition, int requiredProgress)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (requiredProgress <= 0) throw new ArgumentOutOfRangeException(nameof(requiredProgress));
            _requiredProgress = requiredProgress;
            _progressByLayer = new int[definition.LayerCount][];

            int pointCount = 0;
            for (int layer = 0; layer < definition.LayerCount; layer++)
            {
                int count = definition.GetBuildPointCount(layer);
                _progressByLayer[layer] = new int[count];
                pointCount += count;
            }

            _totalPointCount = pointCount;
            _totalRequiredProgress = checked(pointCount * requiredProgress);
        }

        public int LayerCount => _progressByLayer.Length;
        public int RequiredProgressPerPoint => _requiredProgress;
        public int CompletedPointCount => _completedPointCount;
        public int TotalPointCount => _totalPointCount;
        public bool IsCompleted => _completedPointCount == _totalPointCount;
        public float Progress => _totalRequiredProgress == 0 ? 1f : _totalProgress / (float)_totalRequiredProgress;

        public int GetPointCount(int layerIndex)
        {
            ValidateLayer(layerIndex);
            return _progressByLayer[layerIndex].Length;
        }

        public BabelBuildPointState GetPoint(int layerIndex, int pointIndex)
        {
            ValidatePoint(layerIndex, pointIndex);
            return new BabelBuildPointState(
                layerIndex,
                pointIndex,
                _progressByLayer[layerIndex][pointIndex],
                _requiredProgress,
                IsGateway(layerIndex, pointIndex));
        }

        public bool IsGateway(int layerIndex, int pointIndex)
        {
            ValidatePoint(layerIndex, pointIndex);
            return pointIndex < _definition.GetGatewayCount(layerIndex);
        }

        public bool IsPointCompleted(int layerIndex, int pointIndex)
        {
            ValidatePoint(layerIndex, pointIndex);
            return _progressByLayer[layerIndex][pointIndex] >= _requiredProgress;
        }

        public bool IsLayerCompleted(int layerIndex)
        {
            ValidateLayer(layerIndex);
            int[] points = _progressByLayer[layerIndex];
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] < _requiredProgress) return false;
            }

            return true;
        }

        public int CopyIncompletePoints(int layerIndex, bool gatewaysOnly, List<int> destination)
        {
            ValidateLayer(layerIndex);
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();

            int gatewayCount = _definition.GetGatewayCount(layerIndex);
            int[] points = _progressByLayer[layerIndex];
            for (int i = 0; i < points.Length; i++)
            {
                if (gatewaysOnly && i >= gatewayCount) continue;
                if (points[i] < _requiredProgress) destination.Add(i);
            }

            return destination.Count;
        }

        public bool TryApplyBuild(int layerIndex, int pointIndex, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!ContainsPoint(layerIndex, pointIndex)) return false;

            int current = _progressByLayer[layerIndex][pointIndex];
            if (current >= _requiredProgress) return false;

            int applied = Math.Min(amount, _requiredProgress - current);
            int next = current + applied;
            _progressByLayer[layerIndex][pointIndex] = next;
            _totalProgress += applied;
            if (next >= _requiredProgress) _completedPointCount++;
            return true;
        }

        public void Reset()
        {
            for (int layer = 0; layer < _progressByLayer.Length; layer++)
                Array.Clear(_progressByLayer[layer], 0, _progressByLayer[layer].Length);
            _totalProgress = 0;
            _completedPointCount = 0;
        }

        private bool ContainsPoint(int layerIndex, int pointIndex)
        {
            return layerIndex >= 0 &&
                   layerIndex < _progressByLayer.Length &&
                   pointIndex >= 0 &&
                   pointIndex < _progressByLayer[layerIndex].Length;
        }

        private void ValidateLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= _progressByLayer.Length)
                throw new ArgumentOutOfRangeException(nameof(layerIndex));
        }

        private void ValidatePoint(int layerIndex, int pointIndex)
        {
            ValidateLayer(layerIndex);
            if (pointIndex < 0 || pointIndex >= _progressByLayer[layerIndex].Length)
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
        }
    }
}
