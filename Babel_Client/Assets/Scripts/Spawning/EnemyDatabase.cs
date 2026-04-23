using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public static class EnemyDatabase
    {
        private const string LOG_PREFIX = "[BABEL][EnemyDB]";
        private static readonly Dictionary<string, EnemyData> _byId = new();
        private static readonly List<EnemyData> _allEnemies = new();
        private static bool _initialized;

        public static void Init(string csvText)
        {
            _initialized = false;
            _byId.Clear();
            _allEnemies.Clear();

            var parsed = EnemyParser.Parse(csvText);
            for (int i = 0; i < parsed.Count; i++)
            {
                var data = parsed[i];
                if (_byId.ContainsKey(data.EnemyId))
                {
                    Debug.LogWarning($"{LOG_PREFIX} Duplicate enemyId '{data.EnemyId}'. Overwriting.");
                }
                _byId[data.EnemyId] = data;
                _allEnemies.Add(data);
            }

            _initialized = true;
            Debug.Log($"{LOG_PREFIX} Initialized with {_allEnemies.Count} enemy types");
        }

        public static EnemyData GetById(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;
            _byId.TryGetValue(enemyId, out var data);
            return data;
        }

        public static IReadOnlyList<EnemyData> GetAll()
        {
            return _allEnemies.AsReadOnly();
        }

        public static int Count => _allEnemies.Count;
    }
}
