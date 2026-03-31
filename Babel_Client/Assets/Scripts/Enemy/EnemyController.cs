using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements enemy runtime behavior with slot-based pathfinding (S3-14).
/// Movement priority: normal slot → passage slot → climb stairs → enter tower.
/// Aura abilities (Priest/Zealot) are unchanged from S2.
/// </summary>
public class EnemyController : MonoBehaviour, IPoolable
{
    // ── Pathfinding state ─────────────────────────────────────────────────────
    private enum PathState
    {
        FindNormalSlot,    // searching/moving to nearest free normal slot
        FindPassageSlot,   // normal slots full; moving to nearest unbuilt passage slot
        ClimbStairs,       // passage slot built; climbing to next layer
        EnterTower         // at top layer or no MapConfig; moving to tower center
    }

    private static readonly Color HitFlashColor = new Color(1f, 0.3f, 0.3f);
    private static readonly Collider2D[] _auraBuffer = new Collider2D[256];
    private const float AURA_TICK_INTERVAL = 0.5f;
    private const float ARRIVE_THRESHOLD = 0.1f;

    [SerializeField] private LayerMask _enemyLayer;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private EnemyData _data;
    private float _currentHealth;
    private bool _isActive;
    private SpriteRenderer _spriteRenderer;
    private Coroutine _hitFlashCoroutine;
    private float _speedMult = 1f;
    private float _auraTimer;

    // ── Slot-based pathfinding ─────────────────────────────────────────────────
    private TowerConstructionSystem _towerSystem;
    private PathState _pathState;
    private int _currentLayerIndex;
    private readonly Queue<Vector2> _waypoints = new Queue<Vector2>();
    private Vector2 _currentWaypoint;  // active waypoint being moved toward
    private bool _slotOccupied;         // true while this enemy holds a slot
    private Vector2 _passageTargetPos;  // exact passage slot position (avoids float drift)

    public EnemyData Data => _data;
    public EnemyType EnemyType => _data != null ? _data.EnemyType : default;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialize the enemy with slot-based pathfinding.
    /// towerSystem is required for LayerSlotManager access.
    /// startLayer is the tower layer index the enemy should target first (normally 0).
    /// fallbackTarget is used when towerSystem is null (legacy/fallback).
    /// </summary>
    public void Initialize(EnemyData data, Vector2 fallbackTarget,
                           TowerConstructionSystem towerSystem = null, int startLayer = 0)
    {
        _data = data;
        _currentHealth = data != null ? data.MaxHealth : 0f;
        _speedMult = 1f;
        _auraTimer = 0f;
        _isActive = true;

        _towerSystem = towerSystem;
        _currentLayerIndex = startLayer;
        _slotOccupied = false;

        if (_towerSystem != null)
        {
            _pathState = PathState.FindNormalSlot;
            _waypoints.Clear();
            _currentWaypoint = (Vector2)_towerSystem.transform.position; // safe default
            BabelLogger.AC("S3-14", string.Concat(
                "Enemy initialized with slot pathfinding layer=", startLayer.ToString()));
        }
        else
        {
            // No tower system → fall back to legacy direct movement
            _pathState = PathState.EnterTower;
            SetDirectWaypoint(fallbackTarget);
        }
    }

    public void TakeDamage(float damage)
    {
        if (!_isActive) return;
        _currentHealth -= damage;
        if (_spriteRenderer != null)
        {
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlash());
        }
        if (_currentHealth <= 0f) Die();
    }

    public void ApplySpeedBoost(float mult)
    {
        if (mult > _speedMult) _speedMult = mult;
    }

    public void ApplySlow(float percent, float duration) { }

    // ── Pool lifecycle ────────────────────────────────────────────────────────
    public void OnGetFromPool()  => _isActive = true;

    public void OnReturnToPool()
    {
        _isActive = false;
        ReleaseOccupiedSlot();
    }

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_isActive || _data == null ||
            GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying())
            return;

        // Aura tick (unchanged)
        _auraTimer -= Time.deltaTime;
        if (_auraTimer <= 0f)
        {
            _auraTimer = AURA_TICK_INTERVAL;
            TickAura();
        }

        // Pathfinding state machine
        UpdatePathfinding();

        // Movement
        float effectiveSpeed = _data.MoveSpeed * _speedMult;
        _speedMult = 1f;

        Vector2 currentPos = transform.position;
        Vector2 newPos = Vector2.MoveTowards(currentPos, _currentWaypoint, effectiveSpeed * Time.deltaTime);
        transform.position = new Vector3(newPos.x, newPos.y, 0f);

        // Sprite flip — only when moving horizontally
        float dx = newPos.x - currentPos.x;
        if (Mathf.Abs(dx) > 0.001f)
        {
            Vector3 localScale = transform.localScale;
            localScale.x = dx < 0f ? -1f : 1f;
            transform.localScale = localScale;
        }

        // Waypoint arrival check
        if (Vector2.Distance(newPos, _currentWaypoint) < ARRIVE_THRESHOLD)
        {
            if (_waypoints.Count > 0)
                _currentWaypoint = _waypoints.Dequeue(); // advance to next waypoint
            else
                OnArrivedAtTarget(); // all waypoints consumed → final destination reached
        }
    }

    // ── Pathfinding state machine ─────────────────────────────────────────────

    private void UpdatePathfinding()
    {
        // Only recalculate target when not yet occupying a slot
        if (_slotOccupied) return;
        if (_towerSystem == null) return;

        switch (_pathState)
        {
            case PathState.FindNormalSlot:
                TryClaimNormalSlot();
                break;
            case PathState.FindPassageSlot:
                TryFindPassageSlot();
                break;
            // ClimbStairs and EnterTower: target already set, just move
        }
    }

    private void TryClaimNormalSlot()
    {
        LayerSlotManager mgr = _towerSystem.GetSlotManager(_currentLayerIndex);
        if (mgr == null)
        {
            // No slot config for this layer → skip to EnterTower
            FallbackToEnterTower();
            return;
        }

        Vector2 pos = transform.position;
        if (mgr.TryOccupyNormalSlot(this, pos, out Vector2 slotPos))
        {
            EnqueuePath(slotPos);
            _slotOccupied = true;
            BabelLogger.AC("S3-14", string.Concat(
                "Enemy seeking normal slot layer=", _currentLayerIndex.ToString(),
                " target=", slotPos.ToString()));
        }
        else
        {
            // Normal slots full → move to passage slot to build it
            BabelLogger.AC("S3-14", string.Concat(
                "Normal slots full on layer=", _currentLayerIndex.ToString(),
                ", seeking passage slot"));
            _pathState = PathState.FindPassageSlot;
            TryFindPassageSlot();
        }
    }

    private void TryFindPassageSlot()
    {
        LayerSlotManager mgr = _towerSystem.GetSlotManager(_currentLayerIndex);
        if (mgr == null) { FallbackToEnterTower(); return; }

        Vector2 pos = transform.position;
        if (mgr.TryGetNearestUnbuiltPassageSlot(pos, out Vector2 slotPos))
        {
            // Move to passage slot — use _slotOccupied as "en route" guard (no exclusive lock on slot)
            _passageTargetPos = slotPos; // save exact position for BuildPassageAtPosition
            EnqueuePath(slotPos);
            _slotOccupied = true; // prevents UpdatePathfinding from resetting waypoint each frame
            BabelLogger.AC("S3-14", string.Concat(
                "Enemy moving to passage slot layer=", _currentLayerIndex.ToString(),
                " target=", slotPos.ToString()));
        }
        else
        {
            // All passage slots already built → climb immediately
            BabelLogger.AC("S3-14", string.Concat(
                "All passage slots built on layer=", _currentLayerIndex.ToString(),
                ", climbing directly"));
            SetClimbTarget();
        }
    }

    private void SetClimbTarget()
    {
        _pathState = PathState.ClimbStairs;
        _slotOccupied = false;

        // Use enemy's own next layer index, not the tower's active layer
        float towerRootY = _towerSystem.transform.position.y;
        float nextLayerY = towerRootY + (_currentLayerIndex + 1) * 1.2f; // LAYER_HEIGHT = 1.2f
        Vector2 curPos = transform.position;

        // Find the nearest passage slot X on the current layer — that's the staircase entry
        LayerSlotManager mgr = _towerSystem.GetSlotManager(_currentLayerIndex);
        float? passageX = mgr?.GetNearestPassageX(curPos);
        float climbX = passageX ?? curPos.x; // fallback to current X if no passage defined

        // Path: horizontal to passage entry → vertical climb to next layer
        _waypoints.Clear();
        bool alreadyAtPassageX = Mathf.Abs(curPos.x - climbX) < 0.05f;
        if (!alreadyAtPassageX)
            _waypoints.Enqueue(new Vector2(climbX, curPos.y)); // step 1: walk to staircase
        _waypoints.Enqueue(new Vector2(climbX, nextLayerY));   // step 2: climb up
        _currentWaypoint = _waypoints.Dequeue();

        BabelLogger.AC("S3-14", string.Concat(
            "Climbing: layer=", _currentLayerIndex.ToString(),
            " passageX=", climbX.ToString("F2"),
            " nextLayerY=", nextLayerY.ToString("F2")));
    }

    private void FallbackToEnterTower()
    {
        _pathState = PathState.EnterTower;
        SetDirectWaypoint((Vector2)_towerSystem.transform.position);
    }

    /// <summary>
    /// Build a waypoint path from current position to destination.
    /// Horizontal first (X), then vertical (Y) — like human walking.
    /// </summary>
    private void EnqueuePath(Vector2 destination)
    {
        _waypoints.Clear();
        Vector2 cur = transform.position;

        bool sameX = Mathf.Abs(destination.x - cur.x) < 0.05f;
        bool sameY = Mathf.Abs(destination.y - cur.y) < 0.05f;

        if (sameX || sameY)
        {
            // Already aligned on one axis — move directly
            _waypoints.Enqueue(destination);
        }
        else
        {
            // Step 1: move horizontally to destination X (keep current Y)
            _waypoints.Enqueue(new Vector2(destination.x, cur.y));
            // Step 2: move vertically to destination Y
            _waypoints.Enqueue(destination);
        }

        _currentWaypoint = _waypoints.Count > 0 ? _waypoints.Dequeue() : destination;
    }

    /// <summary>Set a single direct waypoint (no axis split needed).</summary>
    private void SetDirectWaypoint(Vector2 destination)
    {
        _waypoints.Clear();
        _currentWaypoint = destination;
    }

    // ── Arrival handlers ──────────────────────────────────────────────────────

    private void OnArrivedAtTarget()
    {
        switch (_pathState)
        {
            case PathState.FindNormalSlot:
                OnArrivedAtNormalSlot();
                break;
            case PathState.FindPassageSlot:
                OnArrivedAtPassageSlot();
                break;
            case PathState.ClimbStairs:
                OnArrivedAtStairsTop();
                break;
            case PathState.EnterTower:
                ReachTower();
                break;
        }
    }

    private void OnArrivedAtNormalSlot()
    {
        ReachTower();
    }

    private void OnArrivedAtPassageSlot()
    {
        _slotOccupied = false;
        // Try to build the passage tile. Returns true = first builder (consumed), false = already built (keep going).
        bool firstBuild = _towerSystem != null && _towerSystem.BuildPassageAtPosition(_passageTargetPos, _currentLayerIndex);
        if (firstBuild)
        {
            BabelLogger.AC("S3-14", string.Concat(
                "Enemy consumed building passage: layer=", _currentLayerIndex.ToString()));
            ReachTower();
        }
        else
        {
            // Passage already built by another enemy — climb through instead of dying
            BabelLogger.AC("S3-14", string.Concat(
                "Passage already built, climbing through: layer=", _currentLayerIndex.ToString()));
            SetClimbTarget();
        }
    }

    private void OnArrivedAtStairsTop()
    {
        // Released slot on previous layer; now target next layer's slots
        _currentLayerIndex++;
        _slotOccupied = false;

        int maxLayer = TowerConstructionSystem.LAYER_COUNT - 1;
        if (_currentLayerIndex > maxLayer)
        {
            // Reached top of tower
            FallbackToEnterTower();
            return;
        }

        BabelLogger.AC("S3-14", string.Concat(
            "Enemy arrived at layer=", _currentLayerIndex.ToString(),
            " after climbing stairs"));
        _pathState = PathState.FindNormalSlot;
    }

    // ── Core gameplay (unchanged) ─────────────────────────────────────────────

    private void Die()
    {
        _isActive = false;
        ReleaseOccupiedSlot();
        EnemyEvents.RaiseEnemyDied(_data, transform.position);
        if (EnemyPool.Instance != null)
            EnemyPool.Instance.Return(this);
        BabelLogger.AC("S3-14", string.Concat(
            "Enemy slot released (enemy died) layer=", _currentLayerIndex.ToString()));
    }

    private void ReachTower()
    {
        _isActive = false;
        // Notify tower to reveal block BEFORE releasing slot (slot lookup needs occupancy intact)
        _towerSystem?.BuildSlotAtPosition(this, _currentLayerIndex);
        ReleaseOccupiedSlot();
        EnemyEvents.RaiseEnemyReachedTower(_data);
        if (EnemyPool.Instance != null)
            EnemyPool.Instance.Return(this);
    }

    private void ReleaseOccupiedSlot()
    {
        if (!_slotOccupied || _towerSystem == null) return;
        LayerSlotManager mgr = _towerSystem.GetSlotManager(_currentLayerIndex);
        mgr?.ReleaseSlot(this);
        _slotOccupied = false;
    }

    // ── Aura system (unchanged from S2) ───────────────────────────────────────

    private IEnumerator HitFlash()
    {
        _spriteRenderer.color = HitFlashColor;
        yield return new WaitForSeconds(0.08f);
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
        _hitFlashCoroutine = null;
    }

    private void TickAura()
    {
        if (_data == null) return;
        switch (_data.SpecialAbility)
        {
            case EnemySpecialAbility.Heal:    TickHealAura();  break;
            case EnemySpecialAbility.BuildFaster: TickSpeedAura(); break;
        }
    }

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

    private void TickSpeedAura()
    {
        float radius = _data.HealRadius;
        if (radius <= 0f) return;
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
}
