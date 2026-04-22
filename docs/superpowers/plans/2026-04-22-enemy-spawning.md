# 怪物生成系统 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 CSV 驱动的波次调度系统（3 种模式）+ 场景 SpawnPoint 标记 + 敌人移动状态机（buildCharges + 通道爬层）+ Path 事件驱动层完成检测。

**Architecture:** WaveParser 解析 waves.csv → WaveScheduler 按 3 种模式（Burst/Maintain/Timed）调度生成 → SceneSpawnProvider 从场景 SpawnPoint 获取位置 → Enemy 通过状态机（MovingToBuildPoint → Building → MovingToPassage → Climbing）执行建造循环。Path 使用事件驱动的 completedCount 替代每帧遍历。

**Tech Stack:** Unity 2022.3 / C# / QFramework (ViewController) / Physics2D

**Spec:** `docs/superpowers/specs/2026-04-22-enemy-spawning-design.md`

---

## File Map

### New Files (under `Babel_Client/Assets/Scripts/Spawning/`)

| File | Responsibility |
|------|---------------|
| `WaveEvent.cs` | WaveEvent class + PoolEntry struct + SpawnMode/SpawnSide enums |
| `WaveParser.cs` | CSV → List<WaveEvent> 解析器 |
| `ISpawnPositionProvider.cs` | 生成位置策略接口 |
| `SpawnPoint.cs` | 场景出生点标记组件 (MonoBehaviour) |
| `SceneSpawnProvider.cs` | 扫描场景 SpawnPoint 并提供位置 |
| `Passage.cs` | 场景通道标记组件 (MonoBehaviour) |
| `IEnemyPool.cs` | 对象池接口 |
| `WaveScheduler.cs` | 核心波次调度器 |

### New Data File

| File | Responsibility |
|------|---------------|
| `Babel_Client/Assets/Data/Waves/waves.csv` | 波次时间线配置 |

### Modified Files

| File | Change |
|------|--------|
| `Scripts/Game/BuildPoint.cs` | 添加 Path 反向引用，建造完成时通知 Path |
| `Scripts/Game/Path.cs` | 添加 _completedCount + IsCompleted + OnBuildPointCompleted() |
| `Scripts/Game/Enemy.cs` | 重写为 EnemyMoveState 状态机 + buildCharges |
| `Scripts/Game/EnemyGenerator.cs` | 重写为 WaveScheduler 的 MonoBehaviour 宿主 |

---

### Task 1: WaveEvent Data Structures

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/WaveEvent.cs`

- [ ] **Step 1: Create WaveEvent, PoolEntry, and enums**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/WaveEvent.cs
using System.Collections.Generic;

namespace Babel
{
    public enum SpawnMode { Burst, Maintain, Timed }
    public enum SpawnSide { Left, Right, Both, Random }

    public struct PoolEntry
    {
        public string EnemyId;
        public float Weight;

        public PoolEntry(string enemyId, float weight)
        {
            EnemyId = enemyId;
            Weight = weight;
        }
    }

    public class WaveEvent
    {
        public float StartTime;
        public float EndTime;           // 0 = no end (until game ends)
        public SpawnMode Mode;
        public List<PoolEntry> EnemyPool = new();
        public int CountMin;
        public int CountMax;
        public float Interval;
        public SpawnSide Side;
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/WaveEvent.cs
git commit -m "feat: add WaveEvent data structures and spawn enums"
```

---

### Task 2: WaveParser (CSV → List<WaveEvent>)

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/WaveParser.cs`

- [ ] **Step 1: Create WaveParser**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/WaveParser.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Babel
{
    public static class WaveParser
    {
        private const string LOG_PREFIX = "[BABEL][WaveParser]";

        public static List<WaveEvent> Parse(string csvText)
        {
            var results = new List<WaveEvent>();
            if (string.IsNullOrEmpty(csvText)) return results;

            // Handle BOM
            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return results; // header + at least 1 data row

            // Parse header to build column index map
            var header = lines[0].Split(',');
            var colMap = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
                colMap[header[i].Trim().ToLower()] = i;

            // Validate required columns
            string[] required = { "starttime", "endtime", "mode", "enemypool", "countmin", "countmax", "interval", "spawnside" };
            foreach (var col in required)
            {
                if (!colMap.ContainsKey(col))
                    throw new FormatException($"{LOG_PREFIX} Missing required column: '{col}'");
            }

            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
            {
                var fields = lines[lineIdx].Split(',');
                if (fields.Length < required.Length) continue;

                try
                {
                    var evt = new WaveEvent
                    {
                        StartTime = ParseFloat(fields[colMap["starttime"]]),
                        EndTime = ParseFloat(fields[colMap["endtime"]]),
                        Mode = ParseMode(fields[colMap["mode"]].Trim()),
                        EnemyPool = ParsePool(fields[colMap["enemypool"]].Trim()),
                        CountMin = int.Parse(fields[colMap["countmin"]].Trim()),
                        CountMax = int.Parse(fields[colMap["countmax"]].Trim()),
                        Interval = ParseFloat(fields[colMap["interval"]]),
                        Side = ParseSide(fields[colMap["spawnside"]].Trim())
                    };
                    results.Add(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_PREFIX} Error parsing line {lineIdx + 1}: {ex.Message}");
                }
            }

            return results;
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }

        private static SpawnMode ParseMode(string value)
        {
            return value switch
            {
                "Burst" => SpawnMode.Burst,
                "Maintain" => SpawnMode.Maintain,
                "Timed" => SpawnMode.Timed,
                _ => throw new FormatException($"Unknown spawn mode: '{value}'")
            };
        }

        private static SpawnSide ParseSide(string value)
        {
            return value switch
            {
                "Left" => SpawnSide.Left,
                "Right" => SpawnSide.Right,
                "Both" => SpawnSide.Both,
                "Random" => SpawnSide.Random,
                _ => throw new FormatException($"Unknown spawn side: '{value}'")
            };
        }

        private static List<PoolEntry> ParsePool(string value)
        {
            var pool = new List<PoolEntry>();
            var entries = value.Split('|');
            foreach (var entry in entries)
            {
                var parts = entry.Split(':');
                if (parts.Length != 2)
                    throw new FormatException($"Invalid pool entry: '{entry}'. Expected 'enemyId:weight'");

                pool.Add(new PoolEntry(
                    parts[0].Trim(),
                    float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture)
                ));
            }
            return pool;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/WaveParser.cs
git commit -m "feat: add WaveParser CSV-to-WaveEvent parser"
```

---

### Task 3: SpawnPoint + Passage Scene Markers

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/SpawnPoint.cs`
- Create: `Babel_Client/Assets/Scripts/Spawning/Passage.cs`

- [ ] **Step 1: Create SpawnPoint MonoBehaviour**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/SpawnPoint.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 场景出生点标记。在场景中放置空 GameObject 并挂载此组件。
    /// WaveScheduler 通过 SceneSpawnProvider 扫描这些标记获取生成位置。
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("此出生点所属的方向（Left/Right），CSV 中 spawnSide 引用")]
        public SpawnSide Side;

        [Tooltip("出生点附近的随机散布半径")]
        [Min(0f)]
        public float SpreadRadius = 0.5f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Side == SpawnSide.Left ? Color.blue : Color.green;
            Gizmos.DrawWireSphere(transform.position, SpreadRadius);
            Gizmos.DrawIcon(transform.position, "d_Animation.Play", true);
        }
    }
}
```

- [ ] **Step 2: Create Passage MonoBehaviour**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/Passage.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 场景通道标记。连接相邻层，敌人通过通道爬到上一层。
    /// </summary>
    public class Passage : MonoBehaviour
    {
        [Tooltip("从哪层（编号，从 1 开始）")]
        public int FromLayer;

        [Tooltip("到哪层")]
        public int ToLayer;

        [Tooltip("到达上层后的出口位置")]
        public Transform ExitPoint;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
            if (ExitPoint != null)
            {
                Gizmos.DrawLine(transform.position, ExitPoint.position);
                Gizmos.DrawWireSphere(ExitPoint.position, 0.2f);
            }
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/SpawnPoint.cs Babel_Client/Assets/Scripts/Spawning/Passage.cs
git commit -m "feat: add SpawnPoint and Passage scene marker components"
```

---

### Task 4: ISpawnPositionProvider + SceneSpawnProvider

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/ISpawnPositionProvider.cs`
- Create: `Babel_Client/Assets/Scripts/Spawning/SceneSpawnProvider.cs`

- [ ] **Step 1: Create ISpawnPositionProvider interface**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/ISpawnPositionProvider.cs
using UnityEngine;

namespace Babel
{
    public interface ISpawnPositionProvider
    {
        Vector2 GetSpawnPosition(SpawnSide side);
    }
}
```

- [ ] **Step 2: Create SceneSpawnProvider**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/SceneSpawnProvider.cs
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 扫描场景中所有 SpawnPoint 组件，按 Side 分组，提供随机散布的生成位置。
    /// </summary>
    public class SceneSpawnProvider : ISpawnPositionProvider
    {
        private readonly List<SpawnPoint> _leftPoints = new();
        private readonly List<SpawnPoint> _rightPoints = new();

        /// <summary>
        /// 扫描场景中所有 SpawnPoint 并缓存。在场景加载后调用一次。
        /// </summary>
        public void ScanScene()
        {
            _leftPoints.Clear();
            _rightPoints.Clear();

            var allPoints = Object.FindObjectsOfType<SpawnPoint>();
            foreach (var point in allPoints)
            {
                if (point.Side == SpawnSide.Left)
                    _leftPoints.Add(point);
                else if (point.Side == SpawnSide.Right)
                    _rightPoints.Add(point);
            }

            if (_leftPoints.Count == 0)
                Debug.LogWarning("[BABEL][SceneSpawnProvider] No Left SpawnPoints found in scene");
            if (_rightPoints.Count == 0)
                Debug.LogWarning("[BABEL][SceneSpawnProvider] No Right SpawnPoints found in scene");
        }

        public Vector2 GetSpawnPosition(SpawnSide side)
        {
            SpawnSide actualSide = side switch
            {
                SpawnSide.Both => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
                SpawnSide.Random => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
                _ => side
            };

            var points = actualSide == SpawnSide.Left ? _leftPoints : _rightPoints;
            if (points.Count == 0)
            {
                Debug.LogWarning($"[BABEL][SceneSpawnProvider] No SpawnPoints for side {actualSide}, returning zero");
                return Vector2.zero;
            }

            var point = points[Random.Range(0, points.Count)];
            Vector2 offset = Random.insideUnitCircle * point.SpreadRadius;
            return (Vector2)point.transform.position + offset;
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/ISpawnPositionProvider.cs Babel_Client/Assets/Scripts/Spawning/SceneSpawnProvider.cs
git commit -m "feat: add ISpawnPositionProvider interface and SceneSpawnProvider"
```

---

### Task 5: IEnemyPool Interface

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/IEnemyPool.cs`

- [ ] **Step 1: Create IEnemyPool interface**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/IEnemyPool.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 对象池接口。WaveScheduler 通过此接口获取和回收敌人实例。
    /// 具体实现在对象池系统 spec 中定义。
    /// </summary>
    public interface IEnemyPool
    {
        /// <summary>
        /// 获取一个敌人实例并放置到指定位置。
        /// </summary>
        /// <param name="enemyId">敌人类型 ID（如 "worker", "elite"）</param>
        /// <param name="position">生成位置</param>
        /// <returns>敌人 GameObject，失败时返回 null</returns>
        GameObject Get(string enemyId, Vector2 position);

        /// <summary>
        /// 回收敌人实例到池中。
        /// </summary>
        void Return(GameObject enemy);

        /// <summary>
        /// 当前同屏活跃敌人数量。
        /// </summary>
        int ActiveCount { get; }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/IEnemyPool.cs
git commit -m "feat: add IEnemyPool interface for object pool abstraction"
```

---

### Task 6: Path Layer Completion Detection (Event-Driven)

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Path.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/BuildPoint.cs`

- [ ] **Step 1: Update Path.cs with completedCount and IsCompleted**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/Path.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/Path.cs
using UnityEngine;

namespace Babel
{
    public class Path : MonoBehaviour
    {
        public BuildPoint[] wayPointList;
        public Babel.Path nextLayerPath;

        private int _completedCount;

        /// <summary>
        /// 当前层是否全部建完（O(1) 查询，替代每帧遍历）。
        /// </summary>
        public bool IsCompleted => _completedCount >= wayPointList.Length;

        /// <summary>
        /// 由 BuildPoint 建造完成时调用，递增完成计数。
        /// </summary>
        public void OnBuildPointCompleted()
        {
            _completedCount++;
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
        /// 获取当前层最近的未完成 BuildPoint 的索引。
        /// 如果全部完成返回 -1。
        /// </summary>
        public int FindNearestEmptyBuildPoint(Vector3 fromPosition)
        {
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].IsBuildCompleted) continue;
                if (wayPointList[i].IsBilding) continue;
                float dist = Vector3.Distance(wayPointList[i].transform.position, fromPosition);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private void OnDrawGizmos()
        {
            if (wayPointList == null || wayPointList.Length == 0) return;
            for (int i = 0; i < wayPointList.Length - 1; i++)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
            }
        }
    }
}
```

Key changes:
- Added `_completedCount` + `IsCompleted` property (replaces `IsCurrentLayerBuildCompleted()`)
- Added `OnBuildPointCompleted()` method
- Added `FindNearestEmptyBuildPoint(Vector3)` helper
- Renamed `getGatewayIndex()` to `GetGatewayIndex()` (C# convention)

- [ ] **Step 2: Update BuildPoint.cs to notify Path on completion**

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
        private int currentBuildProgress = 0;
        public bool isGateway = false;
        public bool IsBuildCompleted = false;
        public bool IsBilding = false;

        /// <summary>
        /// 反向引用所属的 Path，由 Path 或场景设置。
        /// </summary>
        [HideInInspector]
        public Path OwnerPath;

        public void AddBuildProgress(int value)
        {
            if (IsBuildCompleted) return;

            IsBilding = true;
            currentBuildProgress += value;
            this.gameObject.SetActive(true);

            if (currentBuildProgress >= buildAmount)
            {
                IsBuildCompleted = true;
                IsBilding = false;
                GetComponent<SpriteRenderer>().color = Color.red;

                // Notify Path of completion
                if (OwnerPath != null)
                {
                    OwnerPath.OnBuildPointCompleted();
                }
            }
        }
    }
}
```

Key changes:
- Added `OwnerPath` back-reference
- Added early return guard for already completed
- Reset `IsBilding = false` on completion
- Call `OwnerPath.OnBuildPointCompleted()` when completed

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/Path.cs Babel_Client/Assets/Scripts/Game/BuildPoint.cs
git commit -m "feat: event-driven layer completion detection in Path + BuildPoint"
```

---

### Task 7: Enemy Movement State Machine

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Enemy.cs`

- [ ] **Step 1: Rewrite Enemy.cs with EnemyMoveState + buildCharges**

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
        [HideInInspector] public int waveEventId = -1; // Maintain 模式追踪用

        private EnemyMoveState _moveState = EnemyMoveState.MovingToBuildPoint;
        private int _targetBuildPointIndex = -1;
        private Transform _passageTarget;
        private Transform _passageExit;

        /// <summary>
        /// buildCharges 耗尽回收时触发（非死亡），Maintain 模式用。
        /// </summary>
        public static event Action<int> OnChargesExhausted; // param: waveEventId

        // ── IDamageable ──

        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive) return;
            HP -= damage;
        }

        /// <summary>
        /// 生成后由 WaveScheduler 调用，初始化状态。
        /// </summary>
        public void Init(Babel.Path startPath, int charges, int eventId)
        {
            currentPath = startPath;
            buildCharges = charges;
            waveEventId = eventId;
            _moveState = EnemyMoveState.MovingToBuildPoint;
            _targetBuildPointIndex = -1;
            FindNextTarget();
        }

        private void Update()
        {
            // Death check
            if (HP <= 0)
            {
                EnemyEvents.RaiseEnemyDied(Position);
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
                return;
            }

            switch (_moveState)
            {
                case EnemyMoveState.MovingToBuildPoint:
                    UpdateMovingToBuildPoint();
                    break;
                case EnemyMoveState.Building:
                    ExecuteBuilding();
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
                // No valid target found, try to go up a layer
                if (currentPath.IsCompleted)
                {
                    StartMovingToPassage();
                }
                return;
            }

            var target = currentPath.wayPointList[_targetBuildPointIndex];
            var targetPos = new Vector3(target.transform.position.x, target.transform.position.y - 0.5f, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
                _moveState = EnemyMoveState.Building;
            }
        }

        private void ExecuteBuilding()
        {
            if (_targetBuildPointIndex >= 0 && _targetBuildPointIndex < currentPath.wayPointList.Length)
            {
                var bp = currentPath.wayPointList[_targetBuildPointIndex];
                if (!bp.IsBuildCompleted)
                {
                    bp.AddBuildProgress(buildAbility);
                    bp.IsBilding = false;
                }
            }

            buildCharges--;

            if (buildCharges <= 0)
            {
                _moveState = EnemyMoveState.Finished;
                return;
            }

            // Find next target
            FindNextTarget();
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
                // Tower top reached - Game Over
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
            transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
                _moveState = EnemyMoveState.ClimbingPassage;
            }
        }

        private void ExecuteClimbing()
        {
            // Switch to next layer
            currentPath = currentPath.nextLayerPath;
            FindNextTarget();
            _moveState = EnemyMoveState.MovingToBuildPoint;
        }

        private void ExecuteFinished()
        {
            if (waveEventId >= 0)
            {
                OnChargesExhausted?.Invoke(waveEventId);
            }
            this.DestroyGameObjGracefully();
        }

        // ── Helpers ──

        private void FindNextTarget()
        {
            if (currentPath == null)
            {
                _targetBuildPointIndex = -1;
                return;
            }
            _targetBuildPointIndex = currentPath.FindNearestEmptyBuildPoint(transform.position);
        }
    }
}
```

Key changes from original:
- Added `EnemyMoveState` enum and state machine
- Added `buildCharges`, `waveEventId`, `Init()` method
- Added `OnChargesExhausted` static event for Maintain tracking
- Replaced monolithic Update with state-based dispatch
- Uses `Path.IsCompleted` (O(1)) instead of `IsCurrentLayerBuildCompleted()` (O(n))
- Uses `Path.FindNearestEmptyBuildPoint()` helper
- Added alive guard in `TakeDamage`

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/Enemy.cs
git commit -m "feat: rewrite Enemy with EnemyMoveState state machine and buildCharges"
```

---

### Task 8: WaveScheduler + EnemyGenerator Rewrite

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/WaveScheduler.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/EnemyGenerator.cs`

- [ ] **Step 1: Create WaveScheduler**

```csharp
// File: Babel_Client/Assets/Scripts/Spawning/WaveScheduler.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 核心波次调度器。管理所有 WaveEvent 的生命周期和触发。
    /// 纯 C# 类，由 EnemyGenerator (MonoBehaviour) 驱动。
    /// </summary>
    public class WaveScheduler
    {
        public const int MAX_ENEMIES = 100;

        private readonly List<WaveEvent> _events;
        private readonly ISpawnPositionProvider _positionProvider;
        private readonly IEnemyPool _pool;
        private readonly Babel.Path _startPath;
        private readonly List<ActiveWave> _activeWaves = new();
        private readonly HashSet<int> _startedEventIndices = new();

        // Maintain mode tracking: waveEventIndex -> alive count
        private readonly Dictionary<int, int> _maintainCounts = new();

        public WaveScheduler(
            List<WaveEvent> events,
            ISpawnPositionProvider positionProvider,
            IEnemyPool pool,
            Babel.Path startPath)
        {
            _events = events;
            _positionProvider = positionProvider;
            _pool = pool;
            _startPath = startPath;

            // Subscribe to enemy removal events for Maintain tracking
            EnemyEvents.OnEnemyDied += OnEnemyRemoved;
            Enemy.OnChargesExhausted += OnEnemyRemoved;
        }

        public void Dispose()
        {
            EnemyEvents.OnEnemyDied -= OnEnemyRemoved;
            Enemy.OnChargesExhausted -= OnEnemyRemoved;
        }

        /// <summary>
        /// 每帧由 EnemyGenerator 调用。
        /// </summary>
        public void Update(float elapsedTime, float deltaTime)
        {
            StartPendingEvents(elapsedTime);
            UpdateActiveWaves(deltaTime, elapsedTime);
            RemoveExpiredWaves(elapsedTime);
        }

        // ── Event Lifecycle ──

        private void StartPendingEvents(float elapsedTime)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_startedEventIndices.Contains(i)) continue;
                if (elapsedTime < _events[i].StartTime) continue;

                _startedEventIndices.Add(i);
                var wave = new ActiveWave
                {
                    Event = _events[i],
                    EventIndex = i,
                    Timer = 0f, // Process immediately on first activation
                    Fired = false
                };
                _activeWaves.Add(wave);
            }
        }

        private void UpdateActiveWaves(float deltaTime, float elapsedTime)
        {
            for (int i = 0; i < _activeWaves.Count; i++)
            {
                var wave = _activeWaves[i];
                wave.Timer -= deltaTime;

                if (wave.Timer <= 0f)
                {
                    ProcessWave(wave);

                    if (wave.Event.Mode == SpawnMode.Burst)
                    {
                        wave.Fired = true;
                    }
                    else
                    {
                        wave.Timer = wave.Event.Interval;
                    }
                }
            }
        }

        private void RemoveExpiredWaves(float elapsedTime)
        {
            for (int i = _activeWaves.Count - 1; i >= 0; i--)
            {
                var wave = _activeWaves[i];

                // Burst: remove after fired
                if (wave.Event.Mode == SpawnMode.Burst && wave.Fired)
                {
                    _activeWaves.RemoveAt(i);
                    continue;
                }

                // Timed/Maintain: remove if past endTime
                if (wave.Event.EndTime > 0 && elapsedTime >= wave.Event.EndTime)
                {
                    _activeWaves.RemoveAt(i);
                }
            }
        }

        // ── Spawn Logic ──

        private void ProcessWave(ActiveWave wave)
        {
            switch (wave.Event.Mode)
            {
                case SpawnMode.Burst:
                    SpawnBatch(wave);
                    break;
                case SpawnMode.Timed:
                    SpawnBatch(wave);
                    break;
                case SpawnMode.Maintain:
                    SpawnMaintain(wave);
                    break;
            }
        }

        private void SpawnBatch(ActiveWave wave)
        {
            int count = UnityEngine.Random.Range(wave.Event.CountMin, wave.Event.CountMax + 1);
            for (int i = 0; i < count; i++)
            {
                if (_pool.ActiveCount >= MAX_ENEMIES) break;
                SpawnOneEnemy(wave);
            }
        }

        private void SpawnMaintain(ActiveWave wave)
        {
            int target = wave.Event.CountMin;
            _maintainCounts.TryGetValue(wave.EventIndex, out int current);
            int needed = target - current;

            for (int i = 0; i < needed; i++)
            {
                if (_pool.ActiveCount >= MAX_ENEMIES) break;
                SpawnOneEnemy(wave);
                _maintainCounts[wave.EventIndex] = (_maintainCounts.GetValueOrDefault(wave.EventIndex)) + 1;
            }
        }

        private void SpawnOneEnemy(ActiveWave wave)
        {
            string enemyId = PickFromPool(wave.Event.EnemyPool);
            Vector2 pos = _positionProvider.GetSpawnPosition(wave.Event.Side);
            GameObject go = _pool.Get(enemyId, pos);

            if (go == null) return;

            var enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                // TODO: buildCharges should come from EnemyData config, hardcode 1 for now
                enemy.Init(_startPath, 1, wave.EventIndex);
            }
        }

        private static string PickFromPool(List<PoolEntry> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
                totalWeight += pool[i].Weight;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].Weight;
                if (roll <= cumulative)
                    return pool[i].EnemyId;
            }
            return pool[pool.Count - 1].EnemyId;
        }

        // ── Maintain Tracking ──

        private void OnEnemyRemoved(Vector2 _) { /* EnemyDied sends Vector2, can't track waveEventId here */ }

        private void OnEnemyRemoved(int waveEventId)
        {
            if (_maintainCounts.ContainsKey(waveEventId))
            {
                _maintainCounts[waveEventId] = Mathf.Max(0, _maintainCounts[waveEventId] - 1);
            }
        }

        // ── Internal Types ──

        private class ActiveWave
        {
            public WaveEvent Event;
            public int EventIndex;
            public float Timer;
            public bool Fired;
        }
    }
}
```

Note: The `OnEnemyRemoved(Vector2)` for death events can't directly track waveEventId. For Maintain tracking of deaths, Enemy.Update death handler should also invoke `OnChargesExhausted` with its waveEventId. Let me fix this in the Enemy code.

- [ ] **Step 2: Update Enemy death handler to also notify waveEventId**

In `Babel_Client/Assets/Scripts/Game/Enemy.cs`, find the death check in Update and update it:

Find:
```csharp
            // Death check
            if (HP <= 0)
            {
                EnemyEvents.RaiseEnemyDied(Position);
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
                return;
            }
```

Replace with:
```csharp
            // Death check
            if (HP <= 0)
            {
                EnemyEvents.RaiseEnemyDied(Position);
                if (waveEventId >= 0)
                {
                    OnChargesExhausted?.Invoke(waveEventId);
                }
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
                return;
            }
```

And in WaveScheduler, remove the unused `OnEnemyRemoved(Vector2)` overload and change the subscription:

In WaveScheduler constructor, replace:
```csharp
            EnemyEvents.OnEnemyDied += OnEnemyRemoved;
            Enemy.OnChargesExhausted += OnEnemyRemoved;
```

With:
```csharp
            Enemy.OnChargesExhausted += OnEnemyRemoved;
```

And in Dispose, replace:
```csharp
            EnemyEvents.OnEnemyDied -= OnEnemyRemoved;
            Enemy.OnChargesExhausted -= OnEnemyRemoved;
```

With:
```csharp
            Enemy.OnChargesExhausted -= OnEnemyRemoved;
```

And remove the `private void OnEnemyRemoved(Vector2 _)` method entirely.

- [ ] **Step 3: Rewrite EnemyGenerator as WaveScheduler host**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/EnemyGenerator.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/EnemyGenerator.cs
using UnityEngine;
using QFramework;

namespace Babel
{
    /// <summary>
    /// WaveScheduler 的 MonoBehaviour 宿主。
    /// 加载 CSV、初始化 SceneSpawnProvider、每帧驱动调度器。
    /// </summary>
    public partial class EnemyGenerator : ViewController
    {
        [SerializeField] private TextAsset wavesCSV;
        [SerializeField] private Babel.Path startPath;

        private WaveScheduler _scheduler;
        private SceneSpawnProvider _spawnProvider;

        private void Start()
        {
            if (wavesCSV == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No waves CSV assigned");
                return;
            }

            if (startPath == null)
            {
                Debug.LogWarning("[BABEL][EnemyGenerator] No start path assigned");
                return;
            }

            // Parse CSV
            var events = WaveParser.Parse(wavesCSV.text);

            // Setup spawn position provider
            _spawnProvider = new SceneSpawnProvider();
            _spawnProvider.ScanScene();

            // TODO: Replace with real IEnemyPool implementation
            // For now, WaveScheduler requires IEnemyPool - this will be implemented
            // when the object pool system spec is done
            Debug.Log($"[BABEL][EnemyGenerator] Loaded {events.Count} wave events");
        }

        private void Update()
        {
            if (_scheduler == null) return;

            float elapsedTime = 900f - Global.CurrentTime.Value;
            _scheduler.Update(elapsedTime, Time.deltaTime);
        }

        private void OnDestroy()
        {
            _scheduler?.Dispose();
        }
    }
}
```

Note: WaveScheduler instantiation is commented out because it requires IEnemyPool, which is not yet implemented. The object pool system is in a separate spec. When it's ready, the Start method will add:

```csharp
_scheduler = new WaveScheduler(events, _spawnProvider, pool, startPath);
```

- [ ] **Step 4: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Spawning/WaveScheduler.cs Babel_Client/Assets/Scripts/Game/EnemyGenerator.cs Babel_Client/Assets/Scripts/Game/Enemy.cs
git commit -m "feat: add WaveScheduler and rewrite EnemyGenerator as host"
```

---

### Task 9: waves.csv Data File

**Files:**
- Create: `Babel_Client/Assets/Data/Waves/waves.csv`

- [ ] **Step 1: Create waves.csv with default timeline**

```csv
startTime,endTime,mode,enemyPool,countMin,countMax,interval,spawnSide
0,120,Timed,worker:1.0,1,2,4,Both
120,300,Timed,worker:0.7|elite:0.3,2,3,3.5,Both
120,0,Maintain,elite:1.0,2,2,5,Random
300,480,Timed,worker:0.5|priest:0.2|engineer:0.3,2,3,3,Both
480,720,Timed,worker:0.3|elite:0.3|zealot:0.4,3,4,2.5,Both
720,870,Timed,worker:0.2|elite:0.3|priest:0.2|zealot:0.3,4,6,2,Both
870,900,Burst,elite:0.5|zealot:0.5,6,8,0,Both
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Data/Waves/waves.csv
git commit -m "feat: add default waves.csv spawn timeline"
```
