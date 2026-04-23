using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class SceneSpawnProvider : ISpawnPositionProvider
    {
        private readonly List<SpawnPoint> _leftPoints = new();
        private readonly List<SpawnPoint> _rightPoints = new();

        public void ScanScene()
        {
            _leftPoints.Clear();
            _rightPoints.Clear();

            var allPoints = Object.FindObjectsOfType<SpawnPoint>();
            foreach (var point in allPoints)
            {
                if (point.Side == SpawnSide.Left)
                    _leftPoints.Add(point);
                else if (point.Side == SpawnSide.Right)
                    _rightPoints.Add(point);
            }

            if (_leftPoints.Count == 0)
                Debug.LogWarning("[BABEL][SceneSpawnProvider] No Left SpawnPoints found in scene");
            if (_rightPoints.Count == 0)
                Debug.LogWarning("[BABEL][SceneSpawnProvider] No Right SpawnPoints found in scene");
        }

        public Vector2 GetSpawnPosition(SpawnSide side)
        {
            SpawnSide actualSide = side switch
            {
                SpawnSide.Both => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
                SpawnSide.Random => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
                _ => side
            };

            var points = actualSide == SpawnSide.Left ? _leftPoints : _rightPoints;
            if (points.Count == 0)
            {
                Debug.LogWarning($"[BABEL][SceneSpawnProvider] No SpawnPoints for side {actualSide}, returning zero");
                return Vector2.zero;
            }

            var point = points[Random.Range(0, points.Count)];
            Vector2 offset = Random.insideUnitCircle * point.SpreadRadius;
            return (Vector2)point.transform.position + offset;
        }
    }
}
