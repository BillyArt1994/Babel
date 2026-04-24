# BuildPoint 完善设计

> **状态：** 已批准
> **日期：** 2026-04-24
> **范围：** BuildPoint 状态修复 / Path 占位管理 / 持续建造时间 / BuildEvents 事件广播 / Enemy 集成变更

## 1. 设计决策摘要

| 决策点 | 结论 | 理由 |
|--------|------|------|
| 占位管理 | Path 统一管理 Reserve/Release | 集中管理，Enemy 死亡时统一释放 |
| 建造时间 | 由 EnemyData.BuildTime 决定 | 不同敌人建造速度不同（Engineer 快，Worker 慢） |
| 事件广播 | BuildEvents 静态事件类 | UI/音效等系统可订阅，与 EnemyEvents/InputEvents 风格一致 |
| 拼写修复 | IsBilding → IsOccupied | 修正拼写 + 语义（从"正在建造"改为"被占位"） |

## 2. BuildPoint 改造

### 改造后的 BuildPoint

```csharp
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

    /// <summary>
    /// 设置占位状态。由 Path.ReserveBuildPoint / ReleaseBuildPoint 调用。
    /// </summary>
    public void SetOccupied(bool occupied)
    {
        IsOccupied = occupied;
    }

    /// <summary>
    /// 贡献建造进度。到达 buildAmount 时标记完成并通知 Path。
    /// </summary>
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

    /// <summary>
    /// 重置 BuildPoint 到初始状态（新一局游戏时调用）。
    /// </summary>
    public void Reset()
    {
        IsBuildCompleted = false;
        IsOccupied = false;
        _currentProgress = 0;
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
    }
}
```

**变更清单：**
- `IsBilding` → `IsOccupied`（拼写 + 语义）
- `IsBuildCompleted` 改为 `{ get; private set; }`
- `SpriteRenderer` 在 `Awake` 中缓存
- 去掉 `SetActive(true)` 调用
- 新增 `SetOccupied(bool)` 方法
- 新增 `Reset()` 方法
- `AddBuildProgress` 完成时广播 `BuildEvents.RaiseBuildCompleted`
- `currentBuildProgress` 重命名为 `_currentProgress`（私有字段命名规范）

## 3. Path 占位管理

Path 新增预定/释放方法，替代原有的 `FindNearestEmptyBuildPoint`。

```csharp
// Path 新增

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
/// 释放已预定的 BuildPoint。敌人到达建造完成、死亡或回收时调用。
/// </summary>
public void ReleaseBuildPoint(int index)
{
    if (index >= 0 && index < wayPointList.Length)
    {
        wayPointList[index].SetOccupied(false);
    }
}
```

**原有的 `FindNearestEmptyBuildPoint` 方法删除**，由 `ReserveBuildPoint` 替代。

**Path.OnBuildPointCompleted 中新增层完成事件广播：**

```csharp
public void OnBuildPointCompleted()
{
    _completedCount++;
    if (IsCompleted)
    {
        BuildEvents.RaiseLayerCompleted(this);
    }
}
```

## 4. BuildEvents 事件广播

```csharp
public static class BuildEvents
{
    /// <summary>敌人开始建造某个 BuildPoint 时广播。</summary>
    public static event Action<BuildPoint> OnBuildStarted;

    /// <summary>单个 BuildPoint 建造完成时广播。</summary>
    public static event Action<BuildPoint> OnBuildCompleted;

    /// <summary>一整层所有 BuildPoint 建完时广播。</summary>
    public static event Action<Path> OnLayerCompleted;

    public static void RaiseBuildStarted(BuildPoint bp) => OnBuildStarted?.Invoke(bp);
    public static void RaiseBuildCompleted(BuildPoint bp) => OnBuildCompleted?.Invoke(bp);
    public static void RaiseLayerCompleted(Path path) => OnLayerCompleted?.Invoke(path);
}
```

**触发时机：**
- `OnBuildStarted` — Enemy 进入 Building 状态时（Enemy.cs 中触发）
- `OnBuildCompleted` — BuildPoint.AddBuildProgress 内，进度达到 buildAmount 时
- `OnLayerCompleted` — Path.OnBuildPointCompleted 内，IsCompleted 变为 true 时

## 5. Enemy 集成变更

### EnemyData 新增字段

```csharp
public float BuildTime;  // 建造所需时间（秒）
```

**enemies.csv 新增列：**

```csv
enemyId,...,buildTime
worker,...,2.0
elite,...,1.5
priest,...,2.5
engineer,...,1.0
zealot,...,2.0
```

### Enemy.cs 变更

**新增字段：**
```csharp
private float _buildTimer;
```

**Init 中新增：**
```csharp
// buildTime 从 EnemyData 读取，存到 _data 中即可（已有 _data 引用）
```

**选目标改为 Reserve：**
```csharp
// 原来：
_targetBuildPointIndex = currentPath.FindNearestEmptyBuildPoint(transform.position);
// 改为：
_targetBuildPointIndex = currentPath.ReserveBuildPoint(transform.position);
```

**Building 状态改为持续建造：**

进入 Building 时：
```csharp
_moveState = EnemyMoveState.Building;
_buildTimer = _data.BuildTime;
BuildEvents.RaiseBuildStarted(currentPath.wayPointList[_targetBuildPointIndex]);
```

Building Update：
```csharp
private void UpdateBuilding()
{
    _buildTimer -= Time.deltaTime;
    if (_buildTimer > 0) return;

    // 建造完成
    var bp = currentPath.wayPointList[_targetBuildPointIndex];
    if (!bp.IsBuildCompleted)
    {
        bp.AddBuildProgress(buildAbility);
    }
    currentPath.ReleaseBuildPoint(_targetBuildPointIndex);

    buildCharges--;

    if (buildCharges <= 0)
    {
        _moveState = EnemyMoveState.Finished;
        return;
    }

    // 找下一个目标
    _targetBuildPointIndex = currentPath.ReserveBuildPoint(transform.position);
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
```

**死亡/回收时释放占位：**
```csharp
// 死亡处理 和 ExecuteFinished 中
if (_targetBuildPointIndex >= 0 && currentPath != null)
{
    currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
    _targetBuildPointIndex = -1;
}
```

## 6. Path Scene 可视化（OnDrawGizmos 改进）

Path 的 OnDrawGizmos 重写，提供完整的编辑器预览：

### 6.1 层数 ID 标签

Path 新增 `public int LayerIndex` 字段（由 TowerManager.Awake 自动设置）。OnDrawGizmos 中用 `Handles.Label` 在 Path 的中心位置绘制层编号文字。

```csharp
// Path 中心 = 所有 BuildPoint 的平均位置
Vector3 center = GetCenter();
Handles.Label(center + Vector3.up * 1.0f, $"Layer {LayerIndex}", style);
```

### 6.2 Gateway 标识

Gateway BuildPoint 用**黄色大圆圈**标识，普通 BuildPoint 用**白色小圆圈**：

```csharp
for (int i = 0; i < wayPointList.Length; i++)
{
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
}
```

### 6.3 BuildPoint 之间的连线

当前层内相邻 BuildPoint 用**灰色线**连接（已有逻辑，保留）。

### 6.4 Gateway → 上层的连线

如果 `nextLayerPath != null`，从 Gateway BuildPoint 画一条**绿色虚线**连到下一层 Path 的中心位置，表示层间通道：

```csharp
if (nextLayerPath != null)
{
    int gwIdx = GetGatewayIndex();
    Gizmos.color = Color.green;
    Gizmos.DrawLine(
        wayPointList[gwIdx].transform.position,
        nextLayerPath.GetCenter()
    );
}
```

### Path 新增辅助方法

```csharp
public Vector3 GetCenter()
{
    if (wayPointList == null || wayPointList.Length == 0)
        return transform.position;
    Vector3 sum = Vector3.zero;
    for (int i = 0; i < wayPointList.Length; i++)
        sum += wayPointList[i].transform.position;
    return sum / wayPointList.Length;
}
```

### TowerManager.Awake 中自动设置 LayerIndex

```csharp
for (int i = 0; i < layers.Length; i++)
{
    layers[i].LayerIndex = i + 1;  // 从 1 开始
    // ... 原有的链表和 OwnerPath 设置
}
```

## 7. 需要新增的文件

| 文件 | 内容 |
|------|------|
| `Scripts/Game/BuildEvents.cs` | BuildEvents 静态事件类 |

## 7. 需要修改的文件

| 文件 | 变更 |
|------|------|
| `Scripts/Game/BuildPoint.cs` | IsBilding→IsOccupied, 缓存 SpriteRenderer, 去 SetActive, 加 Reset/SetOccupied, 广播事件 |
| `Scripts/Game/Path.cs` | 删 FindNearestEmptyBuildPoint, 加 ReserveBuildPoint/ReleaseBuildPoint, 层完成广播, 加 LayerIndex/GetCenter, 重写 OnDrawGizmos |
| `Scripts/Game/Enemy.cs` | 选目标用 Reserve, Building 改持续建造, 死亡释放占位 |
| `Scripts/Game/TowerManager.cs` | Awake 中设置 LayerIndex |
| `Scripts/Spawning/EnemyData.cs` | 新增 BuildTime 字段 |
| `Scripts/Spawning/EnemyParser.cs` | 解析 buildTime 列 |
| `Assets/Data/Enemies/enemies.csv` | 新增 buildTime 列 |

## 8. 不在本次范围内

- BuildPoint 视觉效果（建造中的动画、进度条）
- TowerManager 的完成度 UI 联动
- 难度缩放对建造的影响
