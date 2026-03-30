using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin execution layer for click-driven attacks from design/gdd/点击攻击系统.md.
/// Resolves targets through Physics2D, applies gameplay effects to enemies,
/// and broadcasts CombatEvents.OnAttackExecuted after each execution.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ClickAttackSystem : MonoBehaviour
{
    private const float DEFAULT_CHAIN_RADIUS = 3f;
    private const float DEFAULT_CHAIN_DECAY = 0.8f;
    private const float PIERCE_HALF_WIDTH = 0.3f;
    private const float SINGLE_RADIUS = 0.5f;
    private const int PHYSICS_BUFFER_SIZE = 64;

    public static ClickAttackSystem Instance { get; private set; }

    [SerializeField] private LayerMask _enemyLayer;

    // Reusable buffers — avoids per-call heap allocation on hot paths
    private readonly List<HitInfo> _hitBuffer = new List<HitInfo>(16);
    private readonly HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();
    private readonly Collider2D[] _overlapBuffer = new Collider2D[PHYSICS_BUFFER_SIZE];
    private readonly RaycastHit2D[] _castBuffer = new RaycastHit2D[PHYSICS_BUFFER_SIZE];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public AttackResult ExecuteAttack(AttackRequest request)
    {
        _hitBuffer.Clear();
        _hitEnemies.Clear();

        switch (request.attackType)
        {
            case AttackType.Single:
                CollectSingleHits(request);
                break;

            case AttackType.AOE:
                CollectAoeHits(request);
                break;

            case AttackType.Chain:
                CollectChainHits(request);
                break;

            case AttackType.Pierce:
                CollectPierceHits(request);
                break;
        }

        if (_hitBuffer.Count == 0)
            return new AttackResult { request = request, hits = new HitInfo[0] };

        AttackResult result = new AttackResult
        {
            request = request,
            hits = _hitBuffer.ToArray()
        };

        CombatEvents.RaiseAttackExecuted(result);
        return result;
    }

    private void CollectSingleHits(AttackRequest request)
    {
        int count = Physics2D.OverlapCircleNonAlloc(request.worldPos, SINGLE_RADIUS, _overlapBuffer, _enemyLayer);
        if (count == 0)
            return;

        EnemyController enemy = _overlapBuffer[0].GetComponent<EnemyController>();
        if (enemy == null)
            return;

        TryApplyHit(enemy, request.damage, request);
    }

    private void CollectAoeHits(AttackRequest request)
    {
        float radius = Mathf.Max(0f, request.radius);
        int count = Physics2D.OverlapCircleNonAlloc(request.worldPos, radius, _overlapBuffer, _enemyLayer);

        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy == null)
                continue;

            TryApplyHit(enemy, request.damage, request);
        }
    }

    private void CollectPierceHits(AttackRequest request)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("ClickAttackSystem requires Camera.main for Pierce attacks.");
            return;
        }

        float cameraDistance = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 leftWorld = mainCamera.ScreenToWorldPoint(new Vector3(0f, 0f, cameraDistance));
        Vector3 rightWorld = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, 0f, cameraDistance));
        float screenWidth = Mathf.Abs(rightWorld.x - leftWorld.x);
        Vector2 boxCenter = new Vector2((leftWorld.x + rightWorld.x) * 0.5f, request.worldPos.y);
        Vector2 boxSize = new Vector2(screenWidth, PIERCE_HALF_WIDTH * 2f);

        int count = Physics2D.BoxCastNonAlloc(boxCenter, boxSize, 0f, Vector2.right, _castBuffer, 0f, _enemyLayer);
        if (count == 0)
            return;

        System.Array.Sort(_castBuffer, 0, count, _pierceComparer);

        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = _castBuffer[i].collider != null
                ? _castBuffer[i].collider.GetComponent<EnemyController>()
                : null;
            if (enemy == null)
                continue;

            TryApplyHit(enemy, request.damage, request);
        }
    }

    private void CollectChainHits(AttackRequest request)
    {
        float chainRadius = request.chainRadius > 0f ? request.chainRadius : DEFAULT_CHAIN_RADIUS;
        float chainDecay = request.chainDecay > 0f ? request.chainDecay : DEFAULT_CHAIN_DECAY;

        EnemyController currentEnemy = FindClosestEnemy(request.worldPos, chainRadius);
        if (currentEnemy == null)
            return;

        float currentDamage = request.damage;
        EnemyController lastHitEnemy = currentEnemy;

        if (TryApplyHit(currentEnemy, currentDamage, request))
            lastHitEnemy = currentEnemy;

        for (int jumpIndex = 0; jumpIndex < request.chainCount; jumpIndex++)
        {
            Vector2 searchOrigin = lastHitEnemy != null
                ? (Vector2)lastHitEnemy.transform.position
                : request.worldPos;
            EnemyController nextEnemy = FindClosestEnemy(searchOrigin, chainRadius);
            if (nextEnemy == null)
                break;

            currentDamage *= chainDecay;
            if (TryApplyHit(nextEnemy, currentDamage, request))
                lastHitEnemy = nextEnemy;
        }
    }

    private EnemyController FindClosestEnemy(Vector2 center, float radius)
    {
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, _overlapBuffer, _enemyLayer);
        if (count == 0)
            return null;

        EnemyController closestEnemy = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = _overlapBuffer[i].GetComponent<EnemyController>();
            if (enemy == null || _hitEnemies.Contains(enemy))
                continue;

            float sqrDistance = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }

    private bool TryApplyHit(EnemyController enemy, float damage, AttackRequest request)
    {
        if (enemy == null || _hitEnemies.Contains(enemy))
            return false;

        Vector2 hitPosition = enemy.transform.position;
        enemy.TakeDamage(damage);

        if (request.slowPercent > 0f)
        {
            // TODO: EnemyController.ApplySlow() is a no-op stub until EnemyAI system is implemented
            enemy.ApplySlow(request.slowPercent, request.slowDuration);
        }

        _hitEnemies.Add(enemy);
        _hitBuffer.Add(new HitInfo
        {
            enemy = enemy,
            hitPosition = hitPosition,
            damageDone = damage
        });

        return true;
    }

    // Stateless comparer instance — avoids lambda allocation on each sort call
    private static readonly PierceHitComparer _pierceComparer = new PierceHitComparer();

    private class PierceHitComparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public int Compare(RaycastHit2D left, RaycastHit2D right)
        {
            float leftX = left.collider != null ? left.collider.transform.position.x : float.MaxValue;
            float rightX = right.collider != null ? right.collider.transform.position.x : float.MaxValue;
            return leftX.CompareTo(rightX);
        }
    }
}
