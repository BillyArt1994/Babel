using UnityEngine;

[DefaultExecutionOrder(-200)]
public class SceneBootstrap : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameLoopManager _gameLoopManager;
    [SerializeField] private PlayerInputHandler _inputHandler;
    [SerializeField] private WaveSpawnSystem _spawnSystem;
    [SerializeField] private TowerConstructionSystem _towerSystem;
    [SerializeField] private EnemyPool _enemyPool;

    [SerializeField] private EnemyStatsLoader _statsLoader;

    [Header("Enemy Pools")]
    [SerializeField] private EnemyControllerPool[] _enemyPoolComponents;
    [SerializeField] private EnemyType[] _enemyPoolTypes;

    [Header("Development")]
    [SerializeField] private bool _autoStartForTesting = true;

    private void Awake()
    {
        Application.targetFrameRate = 60;

        ValidateReference(_gameLoopManager, nameof(_gameLoopManager));
        ValidateReference(_inputHandler, nameof(_inputHandler));
        ValidateReference(_spawnSystem, nameof(_spawnSystem));
        ValidateReference(_towerSystem, nameof(_towerSystem));
        ValidateReference(_enemyPool, nameof(_enemyPool));

        ValidateSingletonAssignment(_gameLoopManager, GameLoopManager.Instance, nameof(GameLoopManager));
        ValidateSingletonAssignment(_inputHandler, PlayerInputHandler.Instance, nameof(PlayerInputHandler));
        ValidateSingletonAssignment(_enemyPool, EnemyPool.Instance, nameof(EnemyPool));
    }

    private void Start()
    {
        ValidateRuntimeSingleton(GameLoopManager.Instance, nameof(GameLoopManager));
        ValidateRuntimeSingleton(PlayerInputHandler.Instance, nameof(PlayerInputHandler));
        ValidateRuntimeSingleton(EnemyPool.Instance, nameof(EnemyPool));

        RegisterEnemyPools();

        if (_statsLoader != null)
            _statsLoader.LoadAndApply();

        if (!_autoStartForTesting)
        {
            return;
        }

        if (_gameLoopManager == null)
        {
            Debug.LogError($"{nameof(SceneBootstrap)} cannot auto-start because {nameof(_gameLoopManager)} is not assigned.", this);
            return;
        }

        _gameLoopManager.StartGame();
    }

    private void RegisterEnemyPools()
    {
        if (_enemyPool == null) return;
        if (_enemyPoolComponents == null || _enemyPoolTypes == null) return;

        int count = Mathf.Min(_enemyPoolComponents.Length, _enemyPoolTypes.Length);
        for (int i = 0; i < count; i++)
        {
            if (_enemyPoolComponents[i] != null)
                _enemyPool.RegisterPool(_enemyPoolTypes[i], _enemyPoolComponents[i]);
        }
    }

    private void ValidateReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogError($"{nameof(SceneBootstrap)} is missing required reference: {fieldName}. Assign it in the inspector.", this);
    }

    private void ValidateSingletonAssignment<T>(T serializedReference, T instance, string singletonName) where T : MonoBehaviour
    {
        if (serializedReference == null && instance == null)
        {
            Debug.LogError($"{nameof(SceneBootstrap)} could not find required singleton {singletonName}. Add it to the scene and assign the reference.", this);
            return;
        }

        if (serializedReference == null && instance != null)
        {
            Debug.LogError($"{nameof(SceneBootstrap)} is missing its inspector reference for singleton {singletonName}, even though an instance exists in the scene.", this);
            return;
        }

        if (serializedReference != null && instance != null && serializedReference != instance)
        {
            Debug.LogError($"{nameof(SceneBootstrap)} reference mismatch for singleton {singletonName}. The assigned scene reference does not match {singletonName}.Instance.", this);
        }
    }

    private void ValidateRuntimeSingleton<T>(T instance, string singletonName) where T : MonoBehaviour
    {
        if (instance != null)
        {
            return;
        }

        Debug.LogError($"{singletonName}.Instance is null at runtime. Ensure the component exists in the scene and its Awake initializes the singleton.", this);
    }
}
