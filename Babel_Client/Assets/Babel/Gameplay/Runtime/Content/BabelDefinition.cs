using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    /// <summary>Immutable bottom-to-top authoring contract for the Tower of Babel.</summary>
    public sealed class BabelDefinition
    {
        private static readonly int[] DefaultBuildPoints = { 8, 7, 6, 6, 5, 4 };
        private static readonly int[] DefaultGateways = { 1, 1, 1, 1, 1, 0 };

        private readonly ReadOnlyCollection<int> _buildPointCounts;
        private readonly ReadOnlyCollection<int> _gatewayCounts;

        public BabelDefinition()
            : this(DefaultBuildPoints, DefaultGateways)
        {
        }

        public BabelDefinition(IEnumerable<int> buildPointCounts, IEnumerable<int> gatewayCounts)
        {
            if (buildPointCounts == null) throw new ArgumentNullException(nameof(buildPointCounts));
            if (gatewayCounts == null) throw new ArgumentNullException(nameof(gatewayCounts));

            var buildPoints = new List<int>(buildPointCounts);
            var gateways = new List<int>(gatewayCounts);
            if (buildPoints.Count == 0)
                throw new ArgumentException("Babel must contain at least one layer.", nameof(buildPointCounts));
            if (buildPoints.Count != gateways.Count)
                throw new ArgumentException("Build-point and gateway counts must describe the same number of layers.", nameof(gatewayCounts));

            for (int i = 0; i < buildPoints.Count; i++)
            {
                if (buildPoints[i] <= 0)
                    throw new ArgumentOutOfRangeException(nameof(buildPointCounts), "Every layer requires at least one build point.");
                if (gateways[i] < 0 || gateways[i] > buildPoints[i])
                    throw new ArgumentOutOfRangeException(nameof(gatewayCounts), "Gateway counts must fit within their layer.");
            }

            if (gateways[gateways.Count - 1] != 0)
                throw new ArgumentException("The top layer cannot lead to another gateway.", nameof(gatewayCounts));

            _buildPointCounts = buildPoints.AsReadOnly();
            _gatewayCounts = gateways.AsReadOnly();
        }

        public int LayerCount => _buildPointCounts.Count;
        public IReadOnlyList<int> BuildPointCounts => _buildPointCounts;
        public IReadOnlyList<int> GatewayCounts => _gatewayCounts;

        public int GetBuildPointCount(int bottomToTopLayerIndex)
        {
            if (bottomToTopLayerIndex < 0 || bottomToTopLayerIndex >= LayerCount)
                throw new ArgumentOutOfRangeException(nameof(bottomToTopLayerIndex));
            return _buildPointCounts[bottomToTopLayerIndex];
        }

        public int GetGatewayCount(int bottomToTopLayerIndex)
        {
            if (bottomToTopLayerIndex < 0 || bottomToTopLayerIndex >= LayerCount)
                throw new ArgumentOutOfRangeException(nameof(bottomToTopLayerIndex));
            return _gatewayCounts[bottomToTopLayerIndex];
        }
    }
}
