using System.Collections;
using UnityEngine;

/// <summary>
/// Implements the enemy runtime behavior defined by design/gdd/敌人生成系统.md.
/// Handles movement toward the tower, damage resolution, pool lifecycle,
/// and special abilities: Priest (heal aura), Zealot (speed aura).
/// Engineer's higher BuildContribution is handled via EnemyData.
/// </summary>
public class EnemyController : MonoBehaviour, IPoolable
{
    private static readonly Color HitFlashColor = new Color(1f, 0.3f, 0.3f);

    // Shared buffer for Physics2D.OverlapCircleNonAlloc – avoids per-call allocation.
    private static readonly Collider2D[] _auraBuffer = new Collider2D[256];

    // Aura tick interval in seconds
    private const float AURA_TICK_INTERVAL = 0.5f;

    [SerializeField] private LayerMask _enemyLayer;

    private EnemyData _data;
    private float _currentHealth;
    private Vector2 _targetPosition;
    private bool _isActive;
    private SpriteRenderer _spriteRenderer;
    private Coroutine _hitFlashCoroutine;

    // Speed multiplier applied by Zealot aura on neighbouring units
    private float _speedMult = 1f;
    private float _auraTimer;

    public EnemyData Data => _data;
    public EnemyType EnemyType => _data != null ? _data.EnemyType : default;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(EnemyData data, Vector2 targetPosition)
    {
        _data = data;
        _targetPosition = targetPosition;
        _currentHealth = data != null ? data.MaxHealth : 0f;
        _speedMult = 1f;
        _auraTimer = 0f;
        _isActive = true;
    }

    public void TakeDamage(float damage)
    {
        if (!_isActive) return;

        _currentHealth -= damage;

        if (_spriteRenderer != null)
        {
            if (_hitFlashCoroutine != null)
                StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlash());
        }

        if (_currentHealth <= 0f)
            Die();
    }

    /// <summary>Apply a speed boost from a Zealot aura (multiplicative).</summary>
    public void ApplySpeedBoost(float mult)
    {
        // Only keep the highest multiplier this frame; reset each aura tick
        if (mult > _speedMult)
            _speedMult = mult;
    }

    public void ApplySlow(float percent, float duration) { }

    private IEnumerator HitFlash()
    {
        _spriteRenderer.color = HitFlashColor;
        yield return new WaitForSeconds(0.08f);
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
        _hitFlashCoroutine = null;
    }

    private void Die()
    {
        _isActive = false;
        EnemyEvents.RaiseEnemyDied(_data, transform.position);
        if (EnemyPool.Instance != null)
            EnemyPool.Instance.Return(this);
    }

    private void ReachTower()
    {
        _isActive = false;
        EnemyEvents.RaiseEnemyReachedTower(_data);
        if (EnemyPool.Instance != null)
            EnemyPool.Instance.Return(this);
    }

    private void Update()
    {
        if (!_isActive || _data == null ||
            GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying())
            return;

        // Tick aura abilities
        _auraTimer -= Time.deltaTime;
        if (_auraTimer <= 0f)
        {
            _auraTimer = AURA_TICK_INTERVAL;
            TickAura();
        }

        // Movement (speed modified by external aura boosts)
        float effectiveSpeed = _data.MoveSpeed * _speedMult;
        _speedMult = 1f;  // reset each frame; re-applied next tick if aura still active

        Vector2 currentPos = transform.position;
        Vector2 newPos = Vector2.MoveTowards(currentPos, _targetPosition, effectiveSpeed * Time.deltaTime);
        transform.position = new Vector3(newPos.x, newPos.y, 0f);

        Vector3 localScale = transform.localScale;
        localScale.x = (newPos.x - currentPos.x) < 0f ? -1f : 1f;
        transform.localScale = localScale;

        if (Vector2.Distance(newPos, _targetPosition) < 0.1f)
            ReachTower();
    }

    private void TickAura()
    {
        if (_data == null) return;

        switch (_data.SpecialAbility)
        {
            case EnemySpecialAbility.Heal:
                TickHealAura();
                break;

            case EnemySpecialAbility.BuildFaster:
                TickSpeedAura();
                break;
        }
    }

    // Priest: heal nearby allies (zero-alloc query)
    private void TickHealAura()
    {
        float radius = _data.HealRadius;
        float healPerTick = _data.HealPerSecond * AURA_TICK_INTERVAL;
        if (radius <= 0f || healPerTick <= 0f) return;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _auraBuffer, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            EnemyController ally = _auraBuffer[i].GetComponent<EnemyController>();
            if (ally == null || ally == this || !ally._isActive) continue;
            ally.Heal(healPerTick);
        }
    }

    // Zealot: boost speed of nearby allies (zero-alloc query)
    private void TickSpeedAura()
    {
        float radius = _data.HealRadius;   // reuse HealRadius field as aura radius
        if (radius <= 0f) return;

        // HealPerSecond reused as speed multiplier (e.g. 1.5 = +50% speed)
        float mult = _data.HealPerSecond > 1f ? _data.HealPerSecond : 1.5f;

        int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _auraBuffer, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            EnemyController ally = _auraBuffer[i].GetComponent<EnemyController>();
            if (ally == null || ally == this || !ally._isActive) continue;
            ally.ApplySpeedBoost(mult);
        }
    }

    private void Heal(float amount)
    {
        if (!_isActive || _data == null) return;
        _currentHealth = Mathf.Min(_currentHealth + amount, _data.MaxHealth);
    }

    public void OnGetFromPool()  => _isActive = true;
    public void OnReturnToPool() => _isActive = false;
}
