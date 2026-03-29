using UnityEngine;

/// <summary>
/// Implements enemy spawning flow defined by design/gdd/敌人生成系统.md.
/// Spawns enemies over time, alternates spawn sides, and routes tower progress on arrival.
/// </summary>
public class EnemySpawnSystem : MonoBehaviour
{
    private const float BASE_INTERVAL = 2.0f;
    private const float DIFFICULTY_SCALE = 3.0f;
    private const float MIN_INTERVAL = 0.3f;

    public static EnemySpawnSystem Instance { get; private set; }

    [SerializeField] private EnemyDatabase _enemyDatabase;
    [SerializeField] private TowerConstructionSystem _towerSystem;
    [SerializeField] private Transform _leftSpawnPoint;
    [SerializeField] private Transform _rightSpawnPoint;

    private float _spawnTimer;
    private bool _isSpawning;
    private bool _spawnFromLeft = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnGameStart += StartSpawning;
        GameEvents.OnGamePaused += HandleGamePaused;
        GameEvents.OnGameResumed += HandleGameResumed;
        GameEvents.OnVictory += StopSpawning;
        GameEvents.OnDefeat += StopSpawning;
        EnemyEvents.OnEnemyReachedTower += OnEnemyReachedTower;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart -= StartSpawning;
        GameEvents.OnGamePaused -= HandleGamePaused;
        GameEvents.OnGameResumed -= HandleGameResumed;
        GameEvents.OnVictory -= StopSpawning;
        GameEvents.OnDefeat -= StopSpawning;
        EnemyEvents.OnEnemyReachedTower -= OnEnemyReachedTower;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartSpawning()
    {
        _isSpawning = true;
        _spawnTimer = 0f;
    }

    public void StopSpawning()
    {
        _isSpawning = false;
    }

    private void Update()
    {
        if (!_isSpawning)
        {
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (GameLoopManager.Instance == null)
        {
            _spawnTimer = BASE_INTERVAL;
            return;
        }

        float elapsed = GameLoopManager.Instance.GetElapsedTime();
        float progress = GameLoopManager.Instance.GetGameProgress();
        EnemyData[] candidates = _enemyDatabase != null ? _enemyDatabase.GetSpawnableAtTime(elapsed) : null;

        if (candidates == null || candidates.Length == 0)
        {
            ResetSpawnTimer(progress);
            return;
        }

        EnemyData selected = PickWeightedRandom(candidates);
        if (selected == null)
        {
            ResetSpawnTimer(progress);
            return;
        }

        Transform spawnPoint = _spawnFromLeft ? _leftSpawnPoint : _rightSpawnPoint;
        _spawnFromLeft = !_spawnFromLeft;
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        Vector2 targetPos = _towerSystem != null ? (Vector2)_towerSystem.transform.position : Vector2.zero;
        EnemyController enemy = EnemyPool.Instance.Get(selected, spawnPoint.position);
        if (enemy == null)
        {
            ResetSpawnTimer(progress);
            return;
        }

        enemy.Initialize(selected, targetPos);

        float interval = Mathf.Max(MIN_INTERVAL, BASE_INTERVAL / (1f + progress * DIFFICULTY_SCALE));
        _spawnTimer = interval;
    }

    private void OnEnemyReachedTower(EnemyData data)
    {
        if (_towerSystem != null)
        {
            _towerSystem.AddProgress(data);
        }
    }

    private EnemyData PickWeightedRandom(EnemyData[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        EnemyData firstValidCandidate = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyData candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            if (firstValidCandidate == null)
            {
                firstValidCandidate = candidate;
            }

            if (candidate.SpawnWeight > 0f)
            {
                totalWeight += candidate.SpawnWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return firstValidCandidate;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyData candidate = candidates[i];
            if (candidate == null || candidate.SpawnWeight <= 0f)
            {
                continue;
            }

            cumulativeWeight += candidate.SpawnWeight;
            if (roll <= cumulativeWeight)
            {
                return candidate;
            }
        }

        return firstValidCandidate;
    }

    private void HandleGamePaused()
    {
        _isSpawning = false;
    }

    private void HandleGameResumed()
    {
        _isSpawning = true;
    }

    private void ResetSpawnTimer(float progress)
    {
        _spawnTimer = Mathf.Max(MIN_INTERVAL, BASE_INTERVAL / (1f + progress * DIFFICULTY_SCALE));
    }
}
