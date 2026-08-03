using Babel.Unity.Infrastructure.Content;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// Legacy encounter presenter retained during migration. Authored data comes only from
    /// GameContentManifest; WP2 replaces its per-frame scheduler with EncounterSystem.
    /// </summary>
    public partial class EnemyGenerator : MonoBehaviour
    {
        [SerializeField] private TextAsset enemiesCSV;
        [SerializeField] private TextAsset wavesCSV;
        [SerializeField] private TowerManager towerManager;

        private WaveScheduler _scheduler;
        private SceneSpawnProvider _spawnProvider;
        private IEnemyPool _pool;

        private void Start()
        {
            ResolveMissingReferences();
            if (!ValidateStartupReferences()) return;

            EnemyDatabase.Init(enemiesCSV.text);
            var events = WaveParser.Parse(wavesCSV.text);
            if (events.Count == 0)
            {
                Debug.LogWarning("[Babel][EnemyGenerator] No wave events loaded.");
                return;
            }

            _spawnProvider = new SceneSpawnProvider();
            _spawnProvider.ScanScene();
            _pool = new TransientEnemyPool();
            _scheduler = new WaveScheduler(events, _spawnProvider, _pool, towerManager.StartPath);
            Debug.Log("[Babel][EnemyGenerator] Started scheduler with " + events.Count + " wave events.");
        }

        private void Update()
        {
            if (!ShouldUpdateScheduler()) return;

            float elapsedTime = GameSession.ElapsedTime;
            _scheduler.Update(elapsedTime, Time.deltaTime);
        }

        private bool ShouldUpdateScheduler()
        {
            return _scheduler != null && GameSession.IsPlaying;
        }

        private void OnDestroy()
        {
            if (_scheduler != null) _scheduler.Dispose();
            _scheduler = null;
            if (_pool is System.IDisposable disposablePool) disposablePool.Dispose();
            _pool = null;
            _spawnProvider = null;
        }

        private void ResolveMissingReferences()
        {
            if (towerManager == null) towerManager = GetComponent<TowerManager>();

            GameContentManifest manifest = GameContentRegistry.Current;
            if (manifest == null) return;
            if (enemiesCSV == null) enemiesCSV = manifest.EnemiesCsv;
            if (wavesCSV == null) wavesCSV = manifest.WavesCsv;
        }

        private bool ValidateStartupReferences()
        {
            if (enemiesCSV == null)
            {
                Debug.LogWarning("[Babel][EnemyGenerator] Manifest enemies CSV is unavailable.");
                return false;
            }

            if (wavesCSV == null)
            {
                Debug.LogWarning("[Babel][EnemyGenerator] Manifest waves CSV is unavailable.");
                return false;
            }

            if (towerManager == null || towerManager.StartPath == null)
            {
                Debug.LogWarning("[Babel][EnemyGenerator] No valid TowerManager StartPath.");
                return false;
            }

            return true;
        }
    }
}
