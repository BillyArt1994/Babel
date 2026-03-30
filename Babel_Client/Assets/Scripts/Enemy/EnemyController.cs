using System.Collections;
using UnityEngine;

/// <summary>
/// Implements the enemy runtime behavior defined by design/gdd/敌人生成系统.md.
/// Handles movement toward the tower, damage resolution, and pool lifecycle.
/// </summary>
public class EnemyController : MonoBehaviour, IPoolable
{
    private EnemyData _data;
    private float _currentHealth;
    private Vector2 _targetPosition;
    private bool _isActive;
    private SpriteRenderer _spriteRenderer;
    private static readonly Color HitFlashColor = new Color(1f, 0.3f, 0.3f);
    private Coroutine _hitFlashCoroutine;

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
        _isActive = true;
    }

    public void TakeDamage(float damage)
    {
        if (!_isActive)
        {
            return;
        }

        _currentHealth -= damage;

        if (_spriteRenderer != null)
        {
            if (_hitFlashCoroutine != null)
                StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlash());
        }

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    private IEnumerator HitFlash()
    {
        _spriteRenderer.color = HitFlashColor;
        yield return new WaitForSeconds(0.08f);
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
        _hitFlashCoroutine = null;
    }

    public void ApplySlow(float percent, float duration)
    {
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
        if (!_isActive || _data == null || GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying())
        {
            return;
        }

        Vector2 currentPos = transform.position;
        Vector2 newPos = Vector2.MoveTowards(currentPos, _targetPosition, _data.MoveSpeed * Time.deltaTime);
        transform.position = new Vector3(newPos.x, newPos.y, 0f);

        Vector3 localScale = transform.localScale;
        localScale.x = (newPos.x - currentPos.x) < 0f ? -1f : 1f;
        transform.localScale = localScale;

        if (Vector2.Distance(newPos, _targetPosition) < 0.1f)
        {
            ReachTower();
        }
    }

    public void OnGetFromPool()
    {
        _isActive = true;
    }

    public void OnReturnToPool()
    {
        _isActive = false;
    }
}
