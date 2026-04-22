# 怪物生成系统设计

> **状态：** 已批准
> **日期：** 2026-04-22
> **范围：** WaveEvent 数据结构 / CSV 波次时间线 / 3 种生成模式 / 场景 SpawnPoint 标记 / 敌人移动状态机 / 对象池集成 / WaveScheduler
> **参考：** Death Must Die 波次系统（多模式 CSV 时间线 + 全局上限 + 对象池）

## 1. 设计决策摘要

| 决策点 | 结论 | 理由 |
|--------|------|------|
| 波次定义 | CSV 文本驱动（waves.csv） | 设计师改数值不用动代码，参考 DMD 的 MonSpawn.txt |
| 生成模式 | 3 种：Burst / Maintain / Timed | 简化自 DMD 的 4 种，去掉 Treasure（预算制太复杂）和 Elite（Maintain 够用） |
| 生成时机 | 固定间隔触发 | 可预测的节奏感更好，随机性放在类型和数量上 |
| 怪物选择 | 加权随机池 | 不用 EliteChance，Babel 的 5 种敌人是独立类型 |
| 生成位置 | 场景内 SpawnPoint 标记物 + ISpawnPositionProvider 接口 | 多地图支持，设计师可视化拖拽配置 |
| 敌人移动 | 状态机（MovingToBuild → Building → MovingToPassage → Climbing → ...） | 支持 buildCharges 多次建造 + 通道爬层 |
| 同屏上限 | 全局最大 100 只 | Babel 规模比 DMD(300) 小 |
| 实例管理 | 对象池 Get/Return | 参考 GDD 对象池系统设计 |

## 2. CSV 波次格式

**文件路径：** `Assets/Data/Waves/waves.csv`

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

**字段说明：**

| 字段 | 类型 | 说明 |
|------|------|------|
| `startTime` | float | 游戏开始后多少秒触发（基于 elapsed time = 900 - CurrentTime） |
| `endTime` | float | 结束时间。0 = 持续到游戏结束 |
| `mode` | string | Burst / Maintain / Timed |
| `enemyPool` | string | `类型:权重\|类型:权重` 格式的加权随机池 |
| `countMin` | int | 每次生成最少数量 |
| `countMax` | int | 每次生成最多数量（Maintain 模式下忽略，用 countMin 作为目标数） |
| `interval` | float | 触发间隔（秒）。Burst 模式忽略此字段 |
| `spawnSide` | string | Left / Right / Both / Random |

**enemyPool 格式示例：** `worker:0.7|elite:0.3` 表示 70% 概率选 worker，30% 概率选 elite。

## 3. 三种生成模式

### Burst（一次性注入）

`startTime` 到达时，一次性生成 `Random(countMin, countMax)` 只怪物，然后事件结束。

用途：精英入场、Boss 波、突然涌入制造压力。

### Timed（定时生成）

从 `startTime` 到 `endTime`，每隔 `interval` 秒生成 `Random(countMin, countMax)` 只怪物。

用途：常规波次，持续输出怪物流。压力通过缩短 interval 和增大 count 来升级。

### Maintain（维持数量）

从 `startTime` 到 `endTime`，每隔 `interval` 秒检查场上该波次的存活数。低于 `countMin` 时补充到 `countMin`。

用途："场上始终保持 N 只精英"。玩家杀不干净，持续压力。

**Maintain 模式特殊规则：**
- `countMin` 作为目标数量，`countMax` 忽略
- 追踪方式：每次 Maintain 事件生成的怪物记录其所属 waveEventId，死亡或 buildCharges 耗尽回收时从计数中移除
- 如果同屏上限已满，Maintain 暂时不补充，等有空间再补

## 4. 核心数据结构

### WaveEvent（解析后的波次事件）

```csharp
public class WaveEvent
{
    public float StartTime;
    public float EndTime;           // 0 = no end
    public SpawnMode Mode;          // Burst, Maintain, Timed
    public List<PoolEntry> EnemyPool;
    public int CountMin;
    public int CountMax;
    public float Interval;
    public SpawnSide Side;
}

public enum SpawnMode { Burst, Maintain, Timed }
public enum SpawnSide { Left, Right, Both, Random }

public struct PoolEntry
{
    public string EnemyId;
    public float Weight;
}
```

### WaveParser

静态 CSV 解析器，类似现有的 SkillParser：
- 解析 waves.csv 为 `List<WaveEvent>`
- 验证必填列、模式名、权重总和
- 失败时 fail-fast

## 5. 生成位置：场景 SpawnPoint 标记

### SpawnPoint 组件

场景中放置空 GameObject 并挂载 `SpawnPoint` 组件，标记出生点位置。

```csharp
public class SpawnPoint : MonoBehaviour
{
    public SpawnSide Side;              // Left / Right（CSV 中引用）
    public float SpreadRadius = 0.5f;   // 出生点附近的随机散布半径
}
```

每张地图场景可以有任意数量的 SpawnPoint，布局由关卡设计师拖拽决定。

**示例场景布局：**

```
Scene: Map_Desert
├── Tower
├── SpawnPoint_Left    ← position=(-12, 0), Side=Left, SpreadRadius=0.5
├── SpawnPoint_Right   ← position=(12, 0), Side=Right, SpreadRadius=0.5
└── ...
```

### ISpawnPositionProvider 接口

```csharp
public interface ISpawnPositionProvider
{
    Vector2 GetSpawnPosition(SpawnSide side);
}
```

### SceneSpawnProvider（当前实现）

启动时扫描场景中所有 SpawnPoint 组件，按 Side 分组缓存。

```csharp
public class SceneSpawnProvider : ISpawnPositionProvider
{
    private List<SpawnPoint> _leftPoints;
    private List<SpawnPoint> _rightPoints;

    public Vector2 GetSpawnPosition(SpawnSide side)
    {
        SpawnSide actualSide = side switch
        {
            SpawnSide.Both => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
            SpawnSide.Random => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
            _ => side
        };

        var points = actualSide == SpawnSide.Left ? _leftPoints : _rightPoints;
        var point = points[Random.Range(0, points.Count)];

        // 在 SpreadRadius 范围内随机散布
        Vector2 offset = Random.insideUnitCircle * point.SpreadRadius;
        return (Vector2)point.transform.position + offset;
    }
}
```

**多地图支持：** 每张地图场景有自己的 SpawnPoint 布局。SceneSpawnProvider 在场景加载时重新扫描，无需代码修改。

## 6. 敌人移动与建造状态机

### 敌人生命周期

敌人携带 `buildCharges`（可建造次数），到达建造点后贡献建造度并消耗 1 次 charge。charge 耗尽则回收到对象池。

```
出生（buildCharges = N）→ 找当前层空 BuildPoint → 走过去 → 建造 → charges--
    → charges > 0?
        → 同层还有空位? → 找下一个 BuildPoint
        → 同层满了? → 走到通道 → 爬到上层 → 找上层空 BuildPoint
    → charges == 0? → 回收到对象池
```

### EnemyMoveState 状态机

```csharp
public enum EnemyMoveState
{
    MovingToBuildPoint,  // 走向目标建造点
    Building,            // 到达，贡献建造度（可以是瞬间完成）
    MovingToPassage,     // 当前层满了，走向通道
    ClimbingPassage,     // 通过通道爬到上一层
    Finished             // buildCharges 用完，等待回收
}
```

**状态转换：**

```
MovingToBuildPoint ──到达──→ Building ──charges-- ──→ charges > 0?
    ├── 同层有空位 → MovingToBuildPoint（下一个目标）
    ├── 同层满了 → MovingToPassage ──到达通道──→ ClimbingPassage ──到达上层──→ MovingToBuildPoint
    └── charges == 0 → Finished → ObjectPool.Return()
```

### buildCharges 示例

| 敌人类型 | buildCharges | 说明 |
|---------|-------------|------|
| Worker | 1 | 建一个点就消失 |
| Elite | 1 | 建一个点就消失（但贡献更多建造度） |
| Engineer | 2 | 能建两个点才消失 |
| Priest | 1 | 建一个点就消失（但有治疗能力） |
| Zealot | 1 | 建一个点就消失（但有速度光环） |

### 通道（Passage）场景标记

场景中放置 `Passage` 标记物，连接相邻层。

```csharp
public class Passage : MonoBehaviour
{
    public int FromLayer;    // 从哪层
    public int ToLayer;      // 到哪层
    public Transform ExitPoint;  // 到达上层后的出口位置
}
```

**场景布局示例：**

```
Scene: Map_Desert
├── Tower
│   ├── Layer1/
│   │   ├── BuildPoint_1_1 ... BuildPoint_1_10
│   │   └── Passage_1to2   ← FromLayer=1, ToLayer=2, ExitPoint=Layer2入口
│   ├── Layer2/
│   │   ├── BuildPoint_2_1 ... BuildPoint_2_9
│   │   └── Passage_2to3
│   └── ...
├── SpawnPoint_Left
└── SpawnPoint_Right
```

敌人到达 Passage 后，位置移动到 ExitPoint，状态切到 `ClimbingPassage`（可播放爬升动画），然后切回 `MovingToBuildPoint` 在新层找目标。

## 7. WaveScheduler（核心调度器）

管理所有 WaveEvent 的生命周期和触发。

```csharp
public class WaveScheduler
{
    private List<WaveEvent> _events;
    private ISpawnPositionProvider _positionProvider;
    private IEnemyPool _pool;
    private List<ActiveWave> _activeWaves;

    void Update(float elapsedTime, float deltaTime)
    {
        StartPendingEvents(elapsedTime);

        foreach (var wave in _activeWaves)
        {
            wave.Timer -= deltaTime;
            if (wave.Timer <= 0)
            {
                ProcessWave(wave, elapsedTime);
                wave.Timer = wave.Event.Interval;
            }
        }

        RemoveExpiredWaves(elapsedTime);
    }
}
```

### ActiveWave（运行时波次状态）

```csharp
private class ActiveWave
{
    public WaveEvent Event;
    public float Timer;          // 距下次触发的剩余时间
    public int AliveCount;       // Maintain 模式追踪存活数
    public bool Fired;           // Burst 模式标记是否已触发
}
```

## 8. 同屏上限与对象池

**全局上限：** `MAX_ENEMIES = 100`

WaveScheduler 在每次 ProcessWave 时检查当前同屏敌人数，超过上限则跳过本次生成。

**对象池接口：**

```csharp
public interface IEnemyPool
{
    GameObject Get(string enemyId, Vector2 position);
    void Return(GameObject enemy);
}
```

**预热数量：** Worker×50, Elite×15, Priest×10, Engineer×10, Zealot×15 = 总计 100。

## 9. 与现有系统的集成

| 方向 | 系统 | 接口 |
|------|------|------|
| 读取 | GameLoopManager | `GetElapsedTime()` = 900 - CurrentTime |
| 读取 | waves.csv | WaveParser 解析 |
| 调用 | 对象池 | `IEnemyPool.Get/Return` |
| 调用 | ISpawnPositionProvider | `GetSpawnPosition(side)` |
| 订阅 | EnemyEvents.OnEnemyDied | Maintain 模式递减存活计数 |
| 订阅 | Enemy.OnChargesExhausted | Maintain 模式递减存活计数（非死亡回收） |
| 查询 | TowerBuildSystem | 获取当前活跃层、空 BuildPoint、Passage 位置 |
| 响应 | GameLoopManager 状态 | Paused → 暂停调度，Playing → 恢复 |

## 10. 需要新增的文件

| 文件 | 内容 |
|------|------|
| `Assets/Data/Waves/waves.csv` | 波次时间线数据 |
| `Scripts/Spawning/WaveEvent.cs` | WaveEvent + PoolEntry + SpawnMode + SpawnSide 枚举 |
| `Scripts/Spawning/WaveParser.cs` | CSV 解析器 |
| `Scripts/Spawning/WaveScheduler.cs` | 核心调度器 |
| `Scripts/Spawning/SpawnPoint.cs` | 场景出生点标记组件 |
| `Scripts/Spawning/Passage.cs` | 场景通道标记组件 |
| `Scripts/Spawning/ISpawnPositionProvider.cs` | 位置策略接口 |
| `Scripts/Spawning/SceneSpawnProvider.cs` | 场景 SpawnPoint 扫描实现 |
| `Scripts/Spawning/IEnemyPool.cs` | 对象池接口 |

## 11. 需要修改的文件

| 文件 | 变更 |
|------|------|
| `Scripts/Game/EnemyGenerator.cs` | 替换为 WaveScheduler 的 MonoBehaviour 宿主 |
| `Scripts/Game/Enemy.cs` | 添加 EnemyMoveState 状态机 + buildCharges + Passage 爬层逻辑 |

## 12. 不在本次范围内

- 对象池的具体实现（预热、扩容逻辑）— 单独 spec
- EnemyData ScriptableObject 定义（5 种敌人的属性配表）— 单独 spec
- 敌人特殊能力（Priest 治疗、Zealot 光环）— 敌人 AI 系统
- TowerBuildSystem 建造逻辑细节 — 已有 GDD
- 难度缩放 / 多难度配置 / Act 分级 — 未来迭代
