using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class WaveScheduler
    {
        public const int MAX_ENEMIES = 100;

        private readonly List<WaveEvent> _events;
        private readonly ISpawnPositionProvider _positionProvider;
        private readonly IEnemyPool _pool;
        private readonly Babel.Path _startPath;
        private readonly List<ActiveWave> _activeWaves = new();
        private readonly HashSet<int> _startedEventIndices = new();
        private readonly Dictionary<int, int> _maintainCounts = new();

        public WaveScheduler(
            List<WaveEvent> events,
            ISpawnPositionProvider positionProvider,
            IEnemyPool pool,
            Babel.Path startPath)
        {
            _events = events;
            _positionProvider = positionProvider;
            _pool = pool;
            _startPath = startPath;

            Enemy.OnChargesExhausted += OnEnemyRemoved;
        }

        public void Dispose()
        {
            Enemy.OnChargesExhausted -= OnEnemyRemoved;
        }

        public void Update(float elapsedTime, float deltaTime)
        {
            StartPendingEvents(elapsedTime);
            UpdateActiveWaves(deltaTime);
            RemoveExpiredWaves(elapsedTime);
        }

        private void StartPendingEvents(float elapsedTime)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_startedEventIndices.Contains(i)) continue;
                if (elapsedTime < _events[i].StartTime) continue;

                _startedEventIndices.Add(i);
                var wave = new ActiveWave
                {
                    Event = _events[i],
                    EventIndex = i,
                    Timer = 0f,
                    Fired = false
                };
                _activeWaves.Add(wave);
            }
        }

        private void UpdateActiveWaves(float deltaTime)
        {
            for (int i = 0; i < _activeWaves.Count; i++)
            {
                var wave = _activeWaves[i];
                wave.Timer -= deltaTime;

                if (wave.Timer <= 0f)
                {
                    ProcessWave(wave);

                    if (wave.Event.Mode == SpawnMode.Burst)
                    {
                        wave.Fired = true;
                    }
                    else
                    {
                        wave.Timer = wave.Event.Interval;
                    }
                }
            }
        }

        private void RemoveExpiredWaves(float elapsedTime)
        {
            for (int i = _activeWaves.Count - 1; i >= 0; i--)
            {
                var wave = _activeWaves[i];

                if (wave.Event.Mode == SpawnMode.Burst && wave.Fired)
                {
                    _activeWaves.RemoveAt(i);
                    continue;
                }

                if (wave.Event.EndTime > 0 && elapsedTime >= wave.Event.EndTime)
                {
                    _activeWaves.RemoveAt(i);
                }
            }
        }

        private void ProcessWave(ActiveWave wave)
        {
            switch (wave.Event.Mode)
            {
                case SpawnMode.Burst:
                case SpawnMode.Timed:
                    SpawnBatch(wave);
                    break;
                case SpawnMode.Maintain:
                    SpawnMaintain(wave);
                    break;
            }
        }

        private void SpawnBatch(ActiveWave wave)
        {
            int count = Random.Range(wave.Event.CountMin, wave.Event.CountMax + 1);
            for (int i = 0; i < count; i++)
            {
                if (_pool.ActiveCount >= MAX_ENEMIES) break;
                SpawnOneEnemy(wave);
            }
        }

        private void SpawnMaintain(ActiveWave wave)
        {
            int target = wave.Event.CountMin;
            _maintainCounts.TryGetValue(wave.EventIndex, out int current);
            int needed = target - current;

            for (int i = 0; i < needed; i++)
            {
                if (_pool.ActiveCount >= MAX_ENEMIES) break;
                SpawnOneEnemy(wave);
                if (!_maintainCounts.ContainsKey(wave.EventIndex))
                    _maintainCounts[wave.EventIndex] = 0;
                _maintainCounts[wave.EventIndex]++;
            }
        }

        private void SpawnOneEnemy(ActiveWave wave)
        {
            string enemyId = PickFromPool(wave.Event.EnemyPool);
            Vector2 pos = _positionProvider.GetSpawnPosition(wave.Event.Side);
            GameObject go = _pool.Get(enemyId, pos);

            if (go == null) return;

            var enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Init(_startPath, 1, wave.EventIndex);
            }
        }

        private static string PickFromPool(List<PoolEntry> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
                totalWeight += pool[i].Weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].Weight;
                if (roll <= cumulative)
                    return pool[i].EnemyId;
            }
            return pool[pool.Count - 1].EnemyId;
        }

        private void OnEnemyRemoved(int waveEventId)
        {
            if (_maintainCounts.ContainsKey(waveEventId))
            {
                _maintainCounts[waveEventId] = Mathf.Max(0, _maintainCounts[waveEventId] - 1);
            }
        }

        private class ActiveWave
        {
            public WaveEvent Event;
            public int EventIndex;
            public float Timer;
            public bool Fired;
        }
    }
}
