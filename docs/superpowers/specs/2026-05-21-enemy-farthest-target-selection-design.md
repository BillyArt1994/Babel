# Enemy Farthest Target Selection Design

## Context

Babel enemies currently reserve build targets through `Path.ReserveBuildPoint(Vector3 fromPos)`. The method filters out completed and occupied `BuildPoint` entries, then chooses the nearest remaining point by `Vector3.Distance`. This makes enemies start building too close to their current position, reducing the player's reaction and kill window.

## Goal

Change enemy build target selection so enemies prefer farther build points on the current layer while retaining controlled randomness. This should make enemies spend more time walking before construction, giving players more meaningful click-attack time.

## Confirmed Behavior

- Candidate points remain the same as today: any `BuildPoint` that is not completed and not occupied.
- `isGateway` points stay eligible if they satisfy the same candidate rules.
- Distance is measured from the enemy's current position passed into `ReserveBuildPoint`.
- Candidates are sorted by distance from farthest to nearest.
- The selectable pool is the farthest half of candidates, using `max(1, floor(candidateCount / 2))`.
- The reserved target is chosen randomly from that farthest-half pool.
- Examples:
  - 1 candidate: choose that candidate.
  - 2 candidates: choose the farthest 1.
  - 3 candidates: choose the farthest 1.
  - 4 candidates: randomly choose among the farthest 2.
  - 5 candidates: randomly choose among the farthest 2.
  - 6 candidates: randomly choose among the farthest 3.

## Architecture

Keep the behavior change inside `Path`. `Enemy` continues to call `currentPath.ReserveBuildPoint(transform.position)` and remains responsible only for movement/building state transitions. `Path` owns build-point availability, target ranking, random selection, and occupancy marking.

This preserves the existing data flow:

```text
Enemy.ReserveNextTarget()
  -> Path.ReserveBuildPoint(enemyPosition)
    -> filter available BuildPoints
    -> choose from farthest half
    -> SetOccupied(true)
  -> Enemy moves horizontally to reserved target
```

## Edge Cases

- No eligible candidates: return `-1` and do not mark any point occupied.
- Fewer than two candidates: choose the only candidate.
- Three candidates: choose only the farthest candidate to avoid small layers becoming too random.
- Already completed or occupied points never enter the random pool.

## Testing Plan

Add EditMode tests around `Path.ReserveBuildPoint`:

- Three eligible points should reserve the farthest point.
- Four eligible points should only reserve one of the farthest two points.
- Completed and occupied points should be excluded before computing the farthest half.

After implementation, run the full EditMode suite and validate in Play Mode that spawned enemies tend to walk toward farther build points before building.

## Scope Exclusions

- No new Inspector strategy field.
- No per-enemy targeting strategy.
- No changes to enemy movement, wave spawning, or UI.
