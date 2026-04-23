using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class SceneSpawnProvider : ISpawnPositionProvider
    {
        private readonly Dictionary<string, List<SpawnPoint>> _pointsById = new();

        public void ScanScene()
        {
            _pointsById.Clear();

            var allPoints = Object.FindObjectsOfType<SpawnPoint>();
            foreach (var point in allPoints)
            {
                string id = string.IsNullOrEmpty(point.Id) ? "default" : point.Id;
                if (!_pointsById.ContainsKey(id))
                    _pointsById[id] = new List<SpawnPoint>();
                _pointsById[id].Add(point);
            }

            if (_pointsById.Count == 0)
                Debug.LogWarning("[BABEL][SceneSpawnProvider] No SpawnPoints found in scene");
        }

        public Vector2 GetSpawnPosition(string spawnPointId)
        {
            if (!_pointsById.TryGetValue(spawnPointId, out var points) || points.Count == 0)
            {
                Debug.LogWarning($"[BABEL][SceneSpawnProvider] No SpawnPoints with Id '{spawnPointId}', returning zero");
                return Vector2.zero;
            }

            var point = points[Random.Range(0, points.Count)];
            float offsetX = Random.Range(-point.SpreadWidth / 2f, point.SpreadWidth / 2f);
            return (Vector2)point.transform.position + new Vector2(offsetX, 0f);
        }
    }
}
