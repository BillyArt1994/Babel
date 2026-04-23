using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class EnemyGenerator : ViewController
    {
        [SerializeField] private TextAsset wavesCSV;
        [SerializeField] private TowerManager towerManager;

        private WaveScheduler _scheduler;
        private SceneSpawnProvider _spawnProvider;

        private void Start()
        {
            if (wavesCSV == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No waves CSV assigned");
                return;
            }

            if (towerManager == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No TowerManager assigned");
                return;
            }

            var events = WaveParser.Parse(wavesCSV.text);

            _spawnProvider = new SceneSpawnProvider();
            _spawnProvider.ScanScene();

            Debug.Log($"[BABEL][EnemyGenerator] Loaded {events.Count} wave events");
            // WaveScheduler instantiation requires IEnemyPool implementation.
            // Uncomment when object pool system is ready:
            // _scheduler = new WaveScheduler(events, _spawnProvider, pool, towerManager.StartPath);
        }

        private void Update()
        {
            if (_scheduler == null) return;

            float elapsedTime = 900f - Global.CurrentTime.Value;
            _scheduler.Update(elapsedTime, Time.deltaTime);
        }

        private void OnDestroy()
        {
            _scheduler?.Dispose();
        }
    }
}
