using UnityEngine;
using QFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Babel
{
    /// <summary>
    /// CSV 驱动的敌人生成入口，负责初始化敌人数据、波次调度和临时生成池。
    /// </summary>
    public partial class EnemyGenerator : ViewController
    {
        private const string ENEMIES_CSV_ASSET_PATH = "Assets/Data/Enemies/enemies.csv";
        private const string WAVES_CSV_ASSET_PATH = "Assets/Data/Waves/waves.csv";

        [SerializeField] private TextAsset enemiesCSV;
        [SerializeField] private TextAsset wavesCSV;
        [SerializeField] private TowerManager towerManager;

        private WaveScheduler _scheduler;
        private SceneSpawnProvider _spawnProvider;
        private IEnemyPool _pool;

        private void Start()
        {
            ResolveMissingReferences();
            if (!ValidateStartupReferences())
            {
                return;
            }

            EnemyDatabase.Init(enemiesCSV.text);
            var events = WaveParser.Parse(wavesCSV.text);
            if (events.Count == 0)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No wave events loaded");
                return;
            }

            _spawnProvider = new SceneSpawnProvider();
            _spawnProvider.ScanScene();
            _pool = new TransientEnemyPool();
            _scheduler = new WaveScheduler(events, _spawnProvider, _pool, towerManager.StartPath);

            Debug.Log($"[BABEL][EnemyGenerator] Started scheduler with {events.Count} wave events");
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

        private void ResolveMissingReferences()
        {
            if (towerManager == null)
            {
                towerManager = GetComponent<TowerManager>();
            }

#if UNITY_EDITOR
            if (enemiesCSV == null)
            {
                enemiesCSV = AssetDatabase.LoadAssetAtPath<TextAsset>(ENEMIES_CSV_ASSET_PATH);
            }

            if (wavesCSV == null)
            {
                wavesCSV = AssetDatabase.LoadAssetAtPath<TextAsset>(WAVES_CSV_ASSET_PATH);
            }
#endif
        }

        private bool ValidateStartupReferences()
        {
            if (enemiesCSV == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No enemies CSV assigned");
                return false;
            }

            if (wavesCSV == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No waves CSV assigned");
                return false;
            }

            if (towerManager == null || towerManager.StartPath == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No valid TowerManager StartPath");
                return false;
            }

            return true;
        }
    }
}
