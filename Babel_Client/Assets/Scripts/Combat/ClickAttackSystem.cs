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

    public static ClickAttackSystem Instance { get; private set; }

    [SerializeField] private LayerMask _enemyLayer;

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
        List<HitInfo> collectedHits = new List<HitInfo>();
        HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();

        switch (request.attackType)
        {
            case AttackType.Single:
                CollectSingleHits(request, collectedHits, hitEnemies);
                break;

            case AttackType.AOE:
                CollectAoeHits(request, collectedHits, hitEnemies);
                break;

            case AttackType.Chain:
                CollectChainHits(request, collectedHits, hitEnemies);
                break;

            case AttackType.Pierce:
                CollectPierceHits(request, collectedHits, hitEnemies);
                break;
        }

        if (collectedHits.Count == 0)
            return new AttackResult { request = request, hits = new HitInfo[0] };

        AttackResult result = new AttackResult
        {
            request = request,
            hits = collectedHits.ToArray()
        };

        CombatEvents.RaiseAttackExecuted(result);
        return result;
    }

    private void CollectSingleHits(AttackRequest request, List<HitInfo> hits, HashSet<EnemyController> hitEnemies)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(request.worldPos, SINGLE_RADIUS, _enemyLayer);
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        EnemyController enemy = colliders[0].GetComponent<EnemyController>();
        if (enemy == null)
        {
            return;
        }

        TryApplyHit(enemy, request.damage, request, hits, hitEnemies);
    }

    private void CollectAoeHits(AttackRequest request, List<HitInfo> hits, HashSet<EnemyController> hitEnemies)
    {
        float radius = Mathf.Max(0f, request.radius);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(request.worldPos, radius, _enemyLayer);

        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyController enemy = colliders[i].GetComponent<EnemyController>();
            if (enemy == null)
            {
                continue;
            }

            TryApplyHit(enemy, request.damage, request, hits, hitEnemies);
        }
    }

    private void CollectPierceHits(AttackRequest request, List<HitInfo> hits, HashSet<EnemyController> hitEnemies)
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

        RaycastHit2D[] hitsRaw = Physics2D.BoxCastAll(boxCenter, boxSize, 0f, Vector2.right, 0f, _enemyLayer);
        if (hitsRaw == null || hitsRaw.Length == 0)
        {
            return;
        }

        System.Array.Sort(hitsRaw, ComparePierceHits);

        for (int i = 0; i < hitsRaw.Length; i++)
        {
            EnemyController enemy = hitsRaw[i].collider != null ? hitsRaw[i].collider.GetComponent<EnemyController>() : null;
            if (enemy == null)
            {
                continue;
            }

            TryApplyHit(enemy, request.damage, request, hits, hitEnemies);
        }
    }

    private void CollectChainHits(AttackRequest request, List<HitInfo> hits, HashSet<EnemyController> hitEnemies)
    {
        float chainRadius = request.chainRadius > 0f ? request.chainRadius : DEFAULT_CHAIN_RADIUS;
        float chainDecay = request.chainDecay > 0f ? request.chainDecay : DEFAULT_CHAIN_DECAY;

        EnemyController currentEnemy = FindClosestEnemy(request.worldPos, chainRadius, hitEnemies);
        if (currentEnemy == null)
        {
            return;
        }

        float currentDamage = request.damage;
        EnemyController lastHitEnemy = currentEnemy;

        if (TryApplyHit(currentEnemy, currentDamage, request, hits, hitEnemies))
        {
            lastHitEnemy = currentEnemy;
        }

        for (int jumpIndex = 0; jumpIndex < request.chainCount; jumpIndex++)
        {
            Vector2 searchOrigin = lastHitEnemy != null ? (Vector2)lastHitEnemy.transform.position : request.worldPos;
            EnemyController nextEnemy = FindClosestEnemy(searchOrigin, chainRadius, hitEnemies);
            if (nextEnemy == null)
            {
                break;
            }

            currentDamage *= chainDecay;
            if (TryApplyHit(nextEnemy, currentDamage, request, hits, hitEnemies))
            {
                lastHitEnemy = nextEnemy;
            }
        }
    }

    private EnemyController FindClosestEnemy(Vector2 center, float radius, HashSet<EnemyController> excludedEnemies)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius, _enemyLayer);
        if (colliders == null || colliders.Length == 0)
        {
            return null;
        }

        EnemyController closestEnemy = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            EnemyController enemy = colliders[i].GetComponent<EnemyController>();
            if (enemy == null || excludedEnemies.Contains(enemy))
            {
                continue;
            }

            float sqrDistance = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }

    private bool TryApplyHit(
        EnemyController enemy,
        float damage,
        AttackRequest request,
        List<HitInfo> hits,
        HashSet<EnemyController> hitEnemies)
    {
        if (enemy == null || hitEnemies.Contains(enemy))
        {
            return false;
        }

        Vector2 hitPosition = enemy.transform.position;
        enemy.TakeDamage(damage);

        if (request.slowPercent > 0f)
        {
            // TODO: EnemyController.ApplySlow() is a no-op stub until EnemyAI system is implemented
            enemy.ApplySlow(request.slowPercent, request.slowDuration);
        }

        hitEnemies.Add(enemy);
        hits.Add(new HitInfo
        {
            enemy = enemy,
            hitPosition = hitPosition,
            damageDone = damage
        });

        return true;
    }

    private int ComparePierceHits(RaycastHit2D left, RaycastHit2D right)
    {
        float leftX = left.collider != null ? left.collider.transform.position.x : float.MaxValue;
        float rightX = right.collider != null ? right.collider.transform.position.x : float.MaxValue;
        return leftX.CompareTo(rightX);
    }
}
