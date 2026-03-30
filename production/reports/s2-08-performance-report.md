# S2-08: 100 Unit On-Screen Performance Benchmark

## Performance Report -- Sprint-2 / 2026-03-30

### Executive Summary

Static code analysis of the Babel enemy system indicates that **100 units at
60 fps is achievable** on mid-range desktop hardware, with two notable
bottleneck areas that should be addressed to ensure headroom and stable P5
frame times. A runtime benchmark script (`PerformanceBenchmark.cs`) has been
created and is ready to be run in-editor for empirical validation.

---

### Frame Time Budget: 16.67ms (60 fps target)

| Category | Budget | Estimated Actual | Status |
|----------|--------|-----------------|--------|
| Gameplay Logic (EnemyController.Update) | 4ms | 1.5 - 2.5ms | OK |
| Physics (Aura OverlapCircleAll) | 4ms | 2 - 6ms | AT RISK |
| Rendering (100 Sprites) | 4ms | 1 - 2ms | OK |
| SkillSystem / ClickAttack | 2ms | 0.5 - 1.5ms | OK |
| UI (DebugHUD) | 1ms | 0.3ms | OK |
| Other (GameLoopManager, Events) | 1.67ms | < 0.5ms | OK |

### Memory Budget: 256MB (estimated project allocation)

| Category | Budget | Estimated Actual | Status |
|----------|--------|-----------------|--------|
| Enemy GameObjects (100 units) | 20MB | 5 - 10MB | OK |
| Object Pool (Stack + HashSet overhead) | 5MB | < 1MB | OK |
| Textures (enemy sprites) | 50MB | 10 - 30MB | OK |
| Physics (100 Collider2D) | 10MB | 2 - 5MB | OK |

---

### System-by-System Code Analysis

#### 1. EnemyController.Update() -- Per-Unit Cost

**File**: `Assets/Scripts/Enemy/EnemyController.cs`, lines 98-126

Each active EnemyController runs the following per frame:

```
Cost breakdown per unit per frame:
  - GameLoopManager.Instance null check + IsPlaying()    ~0.001ms
  - Aura timer decrement + comparison                     ~0.001ms
  - TickAura() (only every 0.5s)                          see "Aura" below
  - Speed calculation (multiply + reset)                  ~0.001ms
  - Vector2.MoveTowards()                                 ~0.002ms
  - transform.position set (triggers transform change)    ~0.005ms
  - transform.localScale set (flip direction)             ~0.003ms
  - Vector2.Distance check                                ~0.001ms
  -------------------------------------------------------
  Total per unit per frame (no aura tick):                ~0.014ms
  100 units:                                              ~1.4ms
```

**Verdict**: The per-frame movement logic is lightweight. 100 units at ~1.4ms
is well within budget. The `transform.position` and `transform.localScale`
writes are the most expensive operations due to Unity's internal TransformChanged
notifications, but this is unavoidable for moving objects.

**Note**: The `_speedMult` reset happens every frame (line 114) while the aura
only ticks every 0.5s. This is a minor design detail, not a performance issue.

#### 2. Aura System (TickHealAura / TickSpeedAura) -- BOTTLENECK #1

**File**: `Assets/Scripts/Enemy/EnemyController.cs`, lines 128-176

Every 0.5 seconds, enemies with `Heal` or `BuildFaster` abilities run:

```csharp
Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
```

**Critical Issues**:

1. **`Physics2D.OverlapCircleAll` allocates a new array every call.** With N
   Priest/Zealot units active, each aura tick generates N array allocations on
   the managed heap. At 100 units with even 10 aura units, that is 10
   allocations every 0.5s (20/sec), each potentially containing up to 100
   Collider2D references.

2. **No LayerMask filtering.** The OverlapCircle queries hit ALL colliders in
   the scene, not just enemies. This wastes physics query time on UI colliders,
   terrain, tower, etc.

3. **`GetComponent<EnemyController>()` is called per hit.** For 100 units with
   colliders, each aura tick calls GetComponent up to 100 times. While Unity
   caches component lookups internally, this is still O(N) work per aura unit.

**Estimated cost per aura tick** (10 aura units, 100 total colliders):
```
  10 * OverlapCircleAll (no layer mask, ~100 hits each)    ~1 - 3ms
  10 * 100 * GetComponent<EnemyController>()               ~0.5 - 1ms
  Array allocations (GC pressure)                          sporadic 1 - 5ms spikes
  ---------------------------------------------------------
  Total per aura tick (every 0.5s):                        ~2 - 6ms
```

**Worst case**: If an aura tick aligns with a frame, a single frame could spike
to 6ms+ from aura processing alone, threatening the 16.67ms budget.

#### 3. Object Pool System -- WELL IMPLEMENTED

**File**: `Assets/Scripts/Utilities/ObjectPool.cs`

The pool implementation is solid:

- Uses `Stack<T>` for O(1) Get/Return
- `HashSet<T>` guards against double-returns
- Pre-allocates `_initialSize` (default 20) instances in Awake
- Grows dynamically via `CreateInstance()` when pool is exhausted

**Minor observation**: The default `_initialSize = 20` means that spawning 100
units will cause 80 additional `Instantiate` calls during gameplay. For a
benchmark test this causes a one-time spike. In production, the pool should be
pre-warmed to the expected maximum (e.g., `_initialSize = 50` per type).

**Memory pattern**: Active objects are tracked in `_activeObjects` (HashSet)
and inactive in `_pool` (Stack). No leak vectors detected -- Return() properly
moves objects between sets.

#### 4. EnemyPool Routing -- EFFICIENT

**File**: `Assets/Scripts/Utilities/EnemyPool.cs`

Dictionary lookup by `EnemyType` enum is O(1). The singleton pattern with
null-check destroy is standard. No performance concerns.

#### 5. ClickAttackSystem -- BOTTLENECK #2 (under load)

**File**: `Assets/Scripts/Combat/ClickAttackSystem.cs`

The system uses pre-allocated buffers (good):
- `_overlapBuffer = new Collider2D[64]` -- fixed buffer, no GC
- `_castBuffer = new RaycastHit2D[64]` -- fixed buffer, no GC
- `_hitBuffer` and `_hitEnemies` are List/HashSet, cleared each call

**Potential issue with 100 units**: The `PHYSICS_BUFFER_SIZE = 64` means AOE
attacks near dense clusters will silently miss enemies beyond the 64th collider.
This is a correctness issue, not performance, but worth noting.

**Chain attack FindClosestEnemy** performs O(N) scan per chain jump with
Physics2D.OverlapCircleNonAlloc, which is efficient. No concern at 100 units.

**`_hitBuffer.ToArray()` on line 78** allocates a new array every attack.
At high attack frequencies (auto-attacks every 1-2s), this generates sustained
GC pressure. Consider returning a ReadOnlySpan or reusing the list.

#### 6. SkillSystem Auto-Attacks -- MODERATE CONCERN

**File**: `Assets/Scripts/Skills/SkillSystem.cs`, line 447

```csharp
Collider2D[] colliders = Physics2D.OverlapCircleAll(
    GetTowerBasePosition(), AUTO_ATTACK_SEARCH_RADIUS, _enemyLayer);
```

The `AUTO_ATTACK_SEARCH_RADIUS = 100f` is extremely large. With 100 enemies
on screen, this query will return all of them every auto-attack tick. The
result array is allocated fresh each time (GC pressure).

However, auto-attacks use the `_enemyLayer` mask (good) and only fire at
configured intervals (`TriggerInterval`), so the frequency is controlled.

#### 7. EnemySpawnSystem -- NO CONCERN

**File**: `Assets/Scripts/Enemy/EnemySpawnSystem.cs`

Spawns one enemy per timer tick. The weighted random selection uses a pre-
allocated `_spawnCandidates` list (capacity 8). The timer-based approach
naturally rate-limits spawning. No performance concern.

#### 8. Rendering -- LOW CONCERN

Each enemy is a simple SpriteRenderer. 100 sprite draw calls will likely be
batched by Unity's 2D renderer (SRP Batcher or dynamic batching) if they share
the same material/texture atlas. Expected draw calls: 10-30 batched calls from
100 sprites.

The HitFlash coroutine (`WaitForSeconds(0.08f)`) creates a small managed
allocation per hit but is infrequent and short-lived.

---

### Top 5 Bottlenecks

| # | Component | Issue | Impact | Recommendation | Effort |
|---|-----------|-------|--------|----------------|--------|
| 1 | EnemyController.TickHealAura / TickSpeedAura | `Physics2D.OverlapCircleAll` allocates every call, no layer mask | 2-6ms spikes every 0.5s + GC pressure | Replace with `Physics2D.OverlapCircleNonAlloc` using a shared static buffer; add `[SerializeField] LayerMask _enemyLayer` and pass it to the query | Low (1-2h) |
| 2 | ClickAttackSystem.ExecuteAttack | `_hitBuffer.ToArray()` allocates per attack | GC spikes under sustained auto-attack fire | Return the list directly or use ArrayPool; downstream consumers read-only | Low (1h) |
| 3 | SkillSystem.FireAutoAttack | `Physics2D.OverlapCircleAll` with radius=100, allocates per call | Array allocation + large query for every auto-attack | Replace with `OverlapCircleNonAlloc` using a pre-allocated buffer (similar to ClickAttackSystem pattern) | Low (1h) |
| 4 | ObjectPool pre-warm | Default `_initialSize = 20` causes 80 runtime Instantiate calls for 100 units | One-time 50-200ms spawn spike | Increase `_initialSize` to 50 per enemy type, or add a `PreWarm(int count)` API | Low (30min) |
| 5 | EnemyController HitFlash | Coroutine + `WaitForSeconds` allocation per hit | Minor GC pressure under heavy fire (100 simultaneous hits) | Cache a single WaitForSeconds instance as a static field; or use a timer-based approach without coroutines | Low (1h) |

### Optimization Priority

**Phase 1 -- Must Fix (before 100-unit gameplay is common)**:
- Bottleneck #1: Aura system OverlapCircleAll -> NonAlloc + LayerMask
- Bottleneck #3: SkillSystem auto-attack OverlapCircleAll -> NonAlloc

**Phase 2 -- Should Fix (before 200+ unit scaling)**:
- Bottleneck #2: ToArray allocation in attack results
- Bottleneck #4: Pool pre-warm sizing

**Phase 3 -- Nice to Have**:
- Bottleneck #5: HitFlash coroutine optimization

---

### Regressions Since Last Report

- None detected (first benchmark report)

---

### Theoretical Performance Estimate

Based on the code analysis, the expected frame budget at 100 units:

```
Best case (no aura units, no attacks):
  EnemyController.Update x 100:   ~1.4ms
  GameLoopManager.Update:          ~0.1ms
  EnemySpawnSystem.Update:         ~0.1ms
  Rendering (100 sprites):         ~1.5ms
  Physics step:                    ~1.0ms
  UI (DebugHUD):                   ~0.3ms
  -----------------------------------------
  Total:                           ~4.4ms  (approx 227 fps)

Typical case (10 aura units, occasional attacks):
  EnemyController.Update x 100:   ~1.4ms
  Aura tick (amortized per frame): ~0.5ms  (2.5ms / 5 frames in 0.5s window)
  SkillSystem.Update:              ~0.2ms
  Auto-attack (amortized):        ~0.3ms
  Rendering:                       ~1.5ms
  Physics step:                    ~2.0ms
  UI:                              ~0.3ms
  -----------------------------------------
  Total:                           ~6.2ms  (approx 161 fps)

Worst case (aura tick frame + AOE attack + GC):
  EnemyController.Update x 100:   ~1.4ms
  Aura tick (10 units, this frame): ~4.0ms
  Attack + aftershock processing:  ~2.0ms
  GC spike (accumulated allocs):   ~3.0ms
  Rendering:                       ~1.5ms
  Physics step:                    ~2.0ms
  UI:                              ~0.3ms
  -----------------------------------------
  Total:                           ~14.2ms (approx 70 fps -- cutting it close)
```

**Conclusion**: The average case comfortably meets the 60fps target. However,
worst-case frame spikes from aura ticks + GC could briefly dip below 60fps.
Implementing the Phase 1 optimizations (NonAlloc + LayerMask) would bring the
worst case down to approximately 8-10ms, providing comfortable headroom.

---

### Runtime Benchmark Script

A runtime benchmark script has been created at:

```
Assets/Scripts/Debug/PerformanceBenchmark.cs
```

**Usage**:
1. Attach to any GameObject in the scene (e.g., Bootstrap)
2. Optionally assign a fallback enemy prefab in the Inspector
3. Right-click the component header -> "Run Benchmark"
4. Results appear in Console and are written to `production/reports/`

The script:
- Spawns 100 units via EnemyPool (falls back to direct Instantiate)
- Disables VSync and frame rate cap
- Records `Time.unscaledDeltaTime` for 5 seconds
- Computes: Avg/Min/Max/Median FPS, P5/P1 low FPS, percentile frame times
- Outputs a markdown report

---

### Recommendations for Engine Programmer

1. **Shared Physics Buffer**: Create a static utility class with a shared
   `Collider2D[256]` buffer that any system can use for NonAlloc queries.
   This prevents each system from maintaining its own buffer.

2. **Enemy Registry**: Consider maintaining a `List<EnemyController>` of all
   active enemies instead of relying on physics queries for aura/auto-attack
   targeting. This would eliminate physics overhead entirely for "find nearby
   enemies" operations and replace it with a simple distance check loop.

3. **Spatial Partitioning**: If unit counts scale to 200+, consider a simple
   grid-based spatial hash for neighbor queries instead of Physics2D overlaps.

4. **GC Monitoring**: Add `System.GC.GetTotalMemory(false)` sampling to the
   DebugHUD to track allocation pressure in real-time during playtesting.
