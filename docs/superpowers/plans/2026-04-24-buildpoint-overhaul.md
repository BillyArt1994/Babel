# BuildPoint Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 BuildPoint 占位逻辑、加入持续建造时间、添加 BuildEvents 事件广播、改进 Path Scene 可视化、并集成到 Enemy 状态机。

**Architecture:** BuildPoint 清理为纯状态容器，占位管理集中到 Path（Reserve/Release），Enemy 的 Building 状态从瞬间改为持续 buildTime 秒，BuildEvents 静态事件供 UI/音效订阅，Path 的 OnDrawGizmos 增强为完整的层结构可视化。

**Tech Stack:** Unity 2022.3 / C# / QFramework (ViewController)

**Spec:** `docs/superpowers/specs/2026-04-24-buildpoint-overhaul-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `Babel_Client/Assets/Scripts/Game/BuildEvents.cs` | BuildEvents 静态事件类 |

### Modified Files

| File | Change |
|------|--------|
| `Scripts/Game/BuildPoint.cs` | 全面重写 |
| `Scripts/Game/Path.cs` | 删 FindNearestEmptyBuildPoint, 加 Reserve/Release/LayerIndex, 重写 Gizmos |
| `Scripts/Game/TowerManager.cs` | Awake 中设置 LayerIndex |
| `Scripts/Game/Enemy.cs` | Reserve/Release 占位, Building 持续时间, 死亡释放 |
| `Scripts/Spawning/EnemyData.cs` | 新增 BuildTime |
| `Scripts/Spawning/EnemyParser.cs` | 解析 buildTime 列 |
| `Assets/Data/Enemies/enemies.csv` | 新增 buildTime 列 |

---

### Task 1: BuildEvents + BuildPoint Rewrite

**Files:**
- Create: `Babel_Client/Assets/Scripts/Game/BuildEvents.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/BuildPoint.cs`

- [ ] **Step 1: Create BuildEvents.cs**

```csharp
// File: Babel_Client/Assets/Scripts/Game/BuildEvents.cs
using System;

namespace Babel
{
    public static class BuildEvents
    {
        public static event Action<BuildPoint> OnBuildStarted;
        public static event Action<BuildPoint> OnBuildCompleted;
        public static event Action<Path> OnLayerCompleted;

        public static void RaiseBuildStarted(BuildPoint bp) => OnBuildStarted?.Invoke(bp);
        public static void RaiseBuildCompleted(BuildPoint bp) => OnBuildCompleted?.Invoke(bp);
        public static void RaiseLayerCompleted(Path path) => OnLayerCompleted?.Invoke(path);
    }
}
```

- [ ] **Step 2: Rewrite BuildPoint.cs**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/BuildPoint.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/BuildPoint.cs
using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class BuildPoint : ViewController
    {
        [SerializeField] private int buildAmount = 50;
        [HideInInspector] public Path OwnerPath;
        public bool isGateway = false;

        public bool IsBuildCompleted { get; private set; }
        public bool IsOccupied { get; private set; }

        private int _currentProgress;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
        }

        public void AddBuildProgress(int value)
        {
            if (IsBuildCompleted) return;

            _currentProgress += value;

            if (_currentProgress >= buildAmount)
            {
                IsBuildCompleted = true;
                IsOccupied = false;
                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.red;

                if (OwnerPath != null)
                    OwnerPath.OnBuildPointCompleted();

                BuildEvents.RaiseBuildCompleted(this);
            }
        }

        public void Reset()
        {
            IsBuildCompleted = false;
            IsOccupied = false;
            _currentProgress = 0;
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/BuildEvents.cs Babel_Client/Assets/Scripts/Game/BuildPoint.cs
git commit -m "feat: rewrite BuildPoint with IsOccupied, Reset, BuildEvents broadcast"
```

---

### Task 2: Path Reserve/Release + Gizmos + LayerIndex

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Path.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/TowerManager.cs`

- [ ] **Step 1: Rewrite Path.cs**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/Path.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/Path.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Babel
{
    public class Path : MonoBehaviour
    {
        public BuildPoint[] wayPointList;
        public Babel.Path nextLayerPath;

        [HideInInspector] public int LayerIndex;

        private int _completedCount;

        public bool IsCompleted => _completedCount >= wayPointList.Length;

        public void OnBuildPointCompleted()
        {
            _completedCount++;
            if (IsCompleted)
            {
                BuildEvents.RaiseLayerCompleted(this);
            }
        }

        public int GetGatewayIndex()
        {
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].isGateway)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// 预定最近的空闲 BuildPoint。标记为已占位并返回索引。
        /// 找不到返回 -1。
        /// </summary>
        public int ReserveBuildPoint(Vector3 fromPos)
        {
            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].IsBuildCompleted) continue;
                if (wayPointList[i].IsOccupied) continue;
                float dist = Vector3.Distance(wayPointList[i].transform.position, fromPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                wayPointList[bestIndex].SetOccupied(true);
            }
            return bestIndex;
        }

        /// <summary>
        /// 释放已预定的 BuildPoint。
        /// </summary>
        public void ReleaseBuildPoint(int index)
        {
            if (index >= 0 && index < wayPointList.Length)
            {
                wayPointList[index].SetOccupied(false);
            }
        }

        private void OnDrawGizmos()
        {
            if (wayPointList == null || wayPointList.Length == 0) return;

            // BuildPoint markers + connections
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i] == null) continue;

                // Gateway = yellow large, normal = white small
                if (wayPointList[i].isGateway)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(wayPointList[i].transform.position, 0.4f);
                }
                else
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(wayPointList[i].transform.position, 0.2f);
                }

                // Connection lines between adjacent BuildPoints
                if (i < wayPointList.Length - 1 && wayPointList[i + 1] != null)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
                }
            }

            // Gateway -> next layer entrance line
            if (nextLayerPath != null && nextLayerPath.wayPointList != null && nextLayerPath.wayPointList.Length > 0)
            {
                int gwIdx = GetGatewayIndex();
                if (gwIdx >= 0 && gwIdx < wayPointList.Length && wayPointList[gwIdx] != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(
                        wayPointList[gwIdx].transform.position,
                        nextLayerPath.wayPointList[0].transform.position
                    );
                }
            }

#if UNITY_EDITOR
            // Layer ID label
            var style = new GUIStyle();
            style.normal.textColor = Color.cyan;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;
            style.alignment = TextAnchor.MiddleCenter;
            Handles.Label(transform.position + Vector3.up * 1.0f, $"Layer {LayerIndex}", style);
#endif
        }
    }
}
```

- [ ] **Step 2: Update TowerManager.cs to set LayerIndex**

In `Babel_Client/Assets/Scripts/Game/TowerManager.cs`, find the Awake loop and add LayerIndex:

Find:
```csharp
                layers[i].nextLayerPath = (i + 1 < layers.Length) ? layers[i + 1] : null;
```

Replace with:
```csharp
                layers[i].LayerIndex = i + 1;
                layers[i].nextLayerPath = (i + 1 < layers.Length) ? layers[i + 1] : null;
```

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/Path.cs Babel_Client/Assets/Scripts/Game/TowerManager.cs
git commit -m "feat: Path reserve/release occupancy, Gizmos visualization, LayerIndex"
```

---

### Task 3: EnemyData BuildTime + CSV Update

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Spawning/EnemyData.cs`
- Modify: `Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs`
- Modify: `Babel_Client/Assets/Data/Enemies/enemies.csv`

- [ ] **Step 1: Add BuildTime to EnemyData.cs**

In `Babel_Client/Assets/Scripts/Spawning/EnemyData.cs`, add after `public float AbilityCooldown;`:

```csharp
        public float BuildTime;
```

- [ ] **Step 2: Add buildTime parsing to EnemyParser.cs**

In `Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs`, after the abilitycooldown parsing block (after line 62), add:

```csharp
                    if (colMap.TryGetValue("buildtime", out int btIdx) && btIdx < fields.Length && !string.IsNullOrWhiteSpace(fields[btIdx]))
                        data.BuildTime = ParseFloat(fields[btIdx]);
```

- [ ] **Step 3: Update enemies.csv with buildTime column**

Replace `Babel_Client/Assets/Data/Enemies/enemies.csv`:

```csv
enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,abilityType,abilityRadius,abilityValue,abilityCooldown,buildTime
worker,工人,30,2.0,25,1,1,Enemies/Worker,,,,,2.0
elite,精英,120,3.0,25,1,5,Enemies/Elite,,,,,1.5
priest,祭司,60,1.5,25,1,3,Enemies/Priest,heal_aura,3.0,10,2.0,2.5
engineer,工程师,60,2.0,50,2,3,Enemies/Engineer,,,,,1.0
zealot,狂信者,20,4.5,25,1,2,Enemies/Zealot,speed_aura,4.0,1.5,0,2.0
```

- [ ] **Step 4: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/EnemyData.cs Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs Babel_Client/Assets/Data/Enemies/enemies.csv
git commit -m "feat: add BuildTime to EnemyData and enemies.csv"
```

---

### Task 4: Enemy Integration (Reserve/Release + Timed Building)

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Enemy.cs`

- [ ] **Step 1: Rewrite Enemy.cs with reserve/release and timed building**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/Enemy.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/Enemy.cs
using System;
using UnityEngine;
using QFramework;

namespace Babel
{
    public enum EnemyMoveState
    {
        MovingToBuildPoint,
        Building,
        MovingToPassage,
        ClimbingPassage,
        Finished
    }

    public partial class Enemy : ViewController, IDamageable
    {
        public float HP = 15;
        public float MovementSpeed = 2.0f;
        public int buildAbility = 25;
        public int buildCharges = 1;

        [HideInInspector] public Babel.Path currentPath;
        [HideInInspector] public int waveEventId = -1;

        private EnemyMoveState _moveState = EnemyMoveState.MovingToBuildPoint;
        private int _targetBuildPointIndex = -1;
        private Transform _passageTarget;
        private float _buildTimer;

        private IEnemyAbility _ability;
        private EnemyData _data;
        private float _speedBuffTimer;
        private float _speedBuffMult = 1.0f;

        public static event Action<int> OnChargesExhausted;

        // IDamageable
        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;
        public float EffectiveSpeed => MovementSpeed * _speedBuffMult;

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive) return;
            HP -= damage;
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            HP += amount;
        }

        public void ApplySpeedBuff(float mult, float duration)
        {
            _speedBuffMult = Mathf.Max(_speedBuffMult, mult);
            _speedBuffTimer = Mathf.Max(_speedBuffTimer, duration);
        }

        public void Init(Babel.Path startPath, EnemyData data, int eventId)
        {
            _data = data;
            HP = data.Hp;
            MovementSpeed = data.MoveSpeed;
            buildAbility = data.BuildContribution;
            buildCharges = data.BuildCharges;
            currentPath = startPath;
            waveEventId = eventId;
            _moveState = EnemyMoveState.MovingToBuildPoint;
            _targetBuildPointIndex = -1;
            _buildTimer = 0;
            _speedBuffTimer = 0;
            _speedBuffMult = 1.0f;
            ReserveNextTarget();

            // Ability
            _ability?.OnRemoved();
            _ability = data.AbilityType switch
            {
                "heal_aura" => new HealAura(),
                "speed_aura" => new SpeedAura(),
                _ => null
            };
            _ability?.Init(this, data);
        }

        private void Update()
        {
            // Death check
            if (HP <= 0)
            {
                ReleaseCurrentTarget();
                EnemyEvents.RaiseEnemyDied(Position);
                if (waveEventId >= 0)
                {
                    OnChargesExhausted?.Invoke(waveEventId);
                }
                _ability?.OnRemoved();
                _ability = null;
                this.DestroyGameObjGracefully();
                Global.Exp.Value += _data != null ? _data.ExpReward : 1;
                return;
            }

            // Ability tick
            _ability?.Tick(Time.deltaTime);

            // Speed buff tick
            if (_speedBuffTimer > 0)
            {
                _speedBuffTimer -= Time.deltaTime;
                if (_speedBuffTimer <= 0) _speedBuffMult = 1.0f;
            }

            switch (_moveState)
            {
                case EnemyMoveState.MovingToBuildPoint:
                    UpdateMovingToBuildPoint();
                    break;
                case EnemyMoveState.Building:
                    UpdateBuilding();
                    break;
                case EnemyMoveState.MovingToPassage:
                    UpdateMovingToPassage();
                    break;
                case EnemyMoveState.ClimbingPassage:
                    ExecuteClimbing();
                    break;
                case EnemyMoveState.Finished:
                    ExecuteFinished();
                    break;
            }
        }

        // ── State Handlers ──

        private void UpdateMovingToBuildPoint()
        {
            if (_targetBuildPointIndex < 0)
            {
                if (currentPath.IsCompleted)
                {
                    StartMovingToPassage();
                }
                return;
            }

            var target = currentPath.wayPointList[_targetBuildPointIndex];
            var targetPos = new Vector3(target.transform.position.x, target.transform.position.y - 0.5f, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
                _buildTimer = _data != null ? _data.BuildTime : 0f;
                _moveState = EnemyMoveState.Building;
                BuildEvents.RaiseBuildStarted(currentPath.wayPointList[_targetBuildPointIndex]);
            }
        }

        private void UpdateBuilding()
        {
            _buildTimer -= Time.deltaTime;
            if (_buildTimer > 0) return;

            // Building complete
            if (_targetBuildPointIndex >= 0 && _targetBuildPointIndex < currentPath.wayPointList.Length)
            {
                var bp = currentPath.wayPointList[_targetBuildPointIndex];
                if (!bp.IsBuildCompleted)
                {
                    bp.AddBuildProgress(buildAbility);
                }
            }
            currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;

            buildCharges--;

            if (buildCharges <= 0)
            {
                _moveState = EnemyMoveState.Finished;
                return;
            }

            // Find next target
            ReserveNextTarget();
            if (_targetBuildPointIndex >= 0)
            {
                _moveState = EnemyMoveState.MovingToBuildPoint;
            }
            else if (currentPath.IsCompleted)
            {
                StartMovingToPassage();
            }
            else
            {
                _moveState = EnemyMoveState.MovingToBuildPoint;
            }
        }

        private void StartMovingToPassage()
        {
            if (currentPath.nextLayerPath == null)
            {
                UIKit.OpenPanel<UIGameOverPanel>();
                return;
            }

            int gatewayIdx = currentPath.GetGatewayIndex();
            _passageTarget = currentPath.wayPointList[gatewayIdx].transform;
            _moveState = EnemyMoveState.MovingToPassage;
        }

        private void UpdateMovingToPassage()
        {
            if (_passageTarget == null) return;

            var targetPos = new Vector3(_passageTarget.position.x, transform.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
                _moveState = EnemyMoveState.ClimbingPassage;
            }
        }

        private void ExecuteClimbing()
        {
            currentPath = currentPath.nextLayerPath;

            // Teleport to entrance of new layer
            if (currentPath != null && currentPath.wayPointList.Length > 0)
            {
                transform.position = currentPath.wayPointList[0].transform.position;
            }

            ReserveNextTarget();
            _moveState = EnemyMoveState.MovingToBuildPoint;
        }

        private void ExecuteFinished()
        {
            ReleaseCurrentTarget();
            if (waveEventId >= 0)
            {
                OnChargesExhausted?.Invoke(waveEventId);
            }
            _ability?.OnRemoved();
            _ability = null;
            this.DestroyGameObjGracefully();
        }

        // ── Helpers ──

        private void ReserveNextTarget()
        {
            if (currentPath == null)
            {
                _targetBuildPointIndex = -1;
                return;
            }
            _targetBuildPointIndex = currentPath.ReserveBuildPoint(transform.position);
        }

        private void ReleaseCurrentTarget()
        {
            if (_targetBuildPointIndex >= 0 && currentPath != null)
            {
                currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
            }
        }
    }
}
```

Key changes from current:
- `FindNextTarget()` → `ReserveNextTarget()` (uses Path.ReserveBuildPoint)
- New `ReleaseCurrentTarget()` helper
- `ExecuteBuilding()` → `UpdateBuilding()` with `_buildTimer` countdown
- Entering Building state sets `_buildTimer = _data.BuildTime` and broadcasts `BuildEvents.RaiseBuildStarted`
- After building complete: `ReleaseBuildPoint` before finding next target
- Death handler calls `ReleaseCurrentTarget()` first
- `ExecuteFinished` calls `ReleaseCurrentTarget()` first
- `ExecuteClimbing` teleports to `wayPointList[0]` of new layer (entrance convention)
- Removed `bp.IsBilding = false` (no longer exists)

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/Enemy.cs
git commit -m "feat: Enemy uses Path.Reserve/Release, timed building, death releases occupancy"
```
