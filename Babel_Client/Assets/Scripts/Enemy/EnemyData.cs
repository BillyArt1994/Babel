using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Babel/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identification")]
    [SerializeField] private string _enemyId;
    [SerializeField] private string _enemyName;
    [SerializeField] private EnemyType _enemyType;

    [Header("Stats")]
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _faithValue;
    [SerializeField] private float _buildContribution = 1.0f;
    [SerializeField] private float _spawnWeight = 1.0f;

    [Header("Special Ability")]
    [SerializeField] private EnemySpecialAbility _specialAbility;
    [SerializeField] private float _healRadius;
    [SerializeField] private float _healPerSecond;
    [SerializeField] private float _deathExplosionRadius;
    [SerializeField] private float _deathExplosionForce;

    [Header("Spawn Timing")]
    [SerializeField] private float _spawnStartTime;

    [Header("Visuals")]
    [SerializeField] private Sprite _sprite;
    [SerializeField] private RuntimeAnimatorController _animatorController;
    [SerializeField] private GameObject _prefab;

    public string EnemyId => _enemyId;
    public string EnemyName => _enemyName;
    public EnemyType EnemyType => _enemyType;
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public float FaithValue => _faithValue;
    public float BuildContribution => _buildContribution;
    public float SpawnWeight => _spawnWeight;
    public EnemySpecialAbility SpecialAbility => _specialAbility;
    public float HealRadius => _healRadius;
    public float HealPerSecond => _healPerSecond;
    public float DeathExplosionRadius => _deathExplosionRadius;
    public float DeathExplosionForce => _deathExplosionForce;
    public float SpawnStartTime => _spawnStartTime;
    public Sprite Sprite => _sprite;
    public RuntimeAnimatorController AnimatorController => _animatorController;
    public GameObject Prefab => _prefab;

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(0.01f, _maxHealth);
        _moveSpeed = Mathf.Max(0.01f, _moveSpeed);
        _faithValue = Mathf.Max(0.01f, _faithValue);
        _spawnWeight = Mathf.Max(0.01f, _spawnWeight);
        _buildContribution = Mathf.Max(0.1f, _buildContribution);

#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(_enemyId))
        {
            Debug.LogWarning($"EnemyData '{name}' has an empty enemyId.", this);
        }

        if (_prefab == null)
        {
            Debug.LogWarning($"EnemyData '{name}' does not have a prefab assigned.", this);
        }
#endif
    }
}
