using System;
using System.Collections.Generic;
using Babel.Gameplay.Content;
using UnityEngine;

namespace Babel.Unity.Presentation.Babel
{
    [Serializable]
    public sealed class BabelLayerAuthoring
    {
        [SerializeField] private string _stableId;
        [SerializeField] private BabelPointView[] _points;

        public string StableId => _stableId;
        public IReadOnlyList<BabelPointView> Points => _points ?? Array.Empty<BabelPointView>();

#if UNITY_EDITOR
        public BabelLayerAuthoring(string stableId, BabelPointView[] points)
        {
            _stableId = stableId;
            _points = points ?? Array.Empty<BabelPointView>();
        }
#endif
    }

    /// <summary>Bottom-to-top scene authoring for the pure gameplay Babel definition.</summary>
    [DisallowMultipleComponent]
    public sealed class BabelAuthoring : MonoBehaviour
    {
        [Tooltip("Layers ordered from bottom to top.")]
        [SerializeField] private BabelLayerAuthoring[] _layers;

        public IReadOnlyList<BabelLayerAuthoring> Layers => _layers ?? Array.Empty<BabelLayerAuthoring>();

        public BabelDefinition CreateDefinition()
        {
            ValidateOrThrow();
            var pointCounts = new int[_layers.Length];
            var gatewayCounts = new int[_layers.Length];
            for (int layerIndex = 0; layerIndex < _layers.Length; layerIndex++)
            {
                IReadOnlyList<BabelPointView> points = _layers[layerIndex].Points;
                pointCounts[layerIndex] = points.Count;
                for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
                    if (points[pointIndex].IsGateway) gatewayCounts[layerIndex]++;
            }
            return new BabelDefinition(pointCounts, gatewayCounts);
        }

        public void ValidateOrThrow()
        {
            if (_layers == null || _layers.Length == 0)
                throw new InvalidOperationException("Babel authoring requires at least one layer.");

            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < _layers.Length; layerIndex++)
            {
                BabelLayerAuthoring layer = _layers[layerIndex];
                if (layer == null) throw new InvalidOperationException($"Babel layer {layerIndex} is missing.");
                if (string.IsNullOrWhiteSpace(layer.StableId) || !identities.Add(layer.StableId))
                    throw new InvalidOperationException($"Babel layer {layerIndex} has a missing or duplicate stable id.");

                IReadOnlyList<BabelPointView> points = layer.Points;
                if (points.Count == 0) throw new InvalidOperationException($"Babel layer '{layer.StableId}' has no points.");
                for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
                {
                    BabelPointView point = points[pointIndex];
                    if (point == null) throw new InvalidOperationException($"Babel layer '{layer.StableId}' contains a missing point.");
                    if (string.IsNullOrWhiteSpace(point.StableId) || !identities.Add(point.StableId))
                        throw new InvalidOperationException($"Babel point {layerIndex}:{pointIndex} has a missing or duplicate stable id.");
                }
            }
        }

#if UNITY_EDITOR
        public void ReplaceLayersForEditor(BabelLayerAuthoring[] layers)
        {
            _layers = layers ?? Array.Empty<BabelLayerAuthoring>();
        }
#endif

        private void OnValidate()
        {
            try { ValidateOrThrow(); }
            catch (InvalidOperationException exception) { Debug.LogWarning(exception.Message, this); }
        }
    }
}
