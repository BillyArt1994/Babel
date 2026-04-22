# 怪物生成系统设计

> **状态：** 已批准（讨论阶段，待完整设计后更新）
> **日期：** 2026-04-22
> **范围：** WaveEvent 数据结构 / CSV 波次时间线 / 3 种生成模式 / 模块化生成位置 / 对象池集成 / WaveScheduler
> **参考：** Death Must Die 波次系统（多模式 CSV 时间线 + 全局上限 + 对象池）

## 1. 设计决策摘要

| 决策点 | 结论 | 理由 |
|--------|------|------|
| 波次定义 | CSV 文本驱动（waves.csv） | 设计师改数值不用动代码，参考 DMD 的 MonSpawn.txt |
| 生成模式 | 3 种：Burst / Maintain / Timed | 简化自 DMD 的 4 种，去掉 Treasure（预算制太复杂）和 Elite（Maintain 够用） |
| 生成时机 | 固定间隔触发 | 可预测的节奏感更好，随机性放在类型和数量上 |
| 怪物选择 | 加权随机池 | 不用 EliteChance，Babel 的 5 种敌人是独立类型 |
| 生成位置 | ISpawnPositionProvider 接口 + ScreenEdgeSpawnProvider 实现 | 模块化可插拔，当前从屏幕边缘生成 |
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
- 追踪方式：每次 Maintain 事件生成的怪物记录其所属 waveEventId，死亡时从计数中移除
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

## 5. 生成位置模块化

### ISpawnPositionProvider 接口

```csharp
public interface ISpawnPositionProvider
{
    Vector2 GetSpawnPosition(SpawnSide side);
}
```

### ScreenEdgeSpawnProvider（当前实现）

从屏幕左/右边缘外侧生成。

```csharp
public class ScreenEdgeSpawnProvider : ISpawnPositionProvider
{
    private const float SPAWN_OFFSET = 1.5f;

    public Vector2 GetSpawnPosition(SpawnSide side)
    {
        SpawnSide actualSide = side switch
        {
            SpawnSide.Both => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
            SpawnSide.Random => Random.value < 0.5f ? SpawnSide.Left : SpawnSide.Right,
            _ => side
        };

        float screenEdgeX = actualSide == SpawnSide.Left
            ? GetScreenLeftEdge() - SPAWN_OFFSET
            : GetScreenRightEdge() + SPAWN_OFFSET;

        float y = GetGroundY();
        return new Vector2(screenEdgeX, y);
    }
}
```

WaveScheduler 通过构造函数注入 `ISpawnPositionProvider`，不直接依赖具体实现。

## 6. WaveScheduler（核心调度器）

管理所有 WaveEvent 的生命周期和触发。

```csharp
public class WaveScheduler
{
    private List<WaveEvent> _events;
    private ISpawnPositionProvider _positionProvider;
    private List<ActiveWave> _activeWaves;  // 正在运行的波次

    // 每帧调用
    void Update(float elapsedTime, float deltaTime)
    {
        // 1. 激活到时间的新事件
        StartPendingEvents(elapsedTime);

        // 2. 更新所有活跃波次
        foreach (var wave in _activeWaves)
        {
            wave.Timer -= deltaTime;
            if (wave.Timer <= 0)
            {
                ProcessWave(wave, elapsedTime);
                wave.Timer = wave.Event.Interval;
            }
        }

        // 3. 移除过期波次
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

## 7. 同屏上限与对象池

**全局上限：** `MAX_ENEMIES = 100`

WaveScheduler 在每次 ProcessWave 时检查当前同屏敌人数，超过上限则跳过本次生成。

**对象池接口（与 GDD 对象池系统对齐）：**

```csharp
public interface IEnemyPool
{
    GameObject Get(string enemyId, Vector2 position);
    void Return(GameObject enemy);
}
```

WaveScheduler 通过构造函数注入 `IEnemyPool`。

**预热数量：**
- Worker: 50
- Elite: 15
- Priest: 10
- Engineer: 10
- Zealot: 15
- 总计: 100

## 8. 与现有系统的集成

| 方向 | 系统 | 接口 |
|------|------|------|
| 读取 | GameLoopManager | `GetElapsedTime()` = 900 - CurrentTime |
| 读取 | waves.csv | WaveParser 解析 |
| 调用 | 对象池 | `IEnemyPool.Get/Return` |
| 调用 | ISpawnPositionProvider | `GetSpawnPosition(side)` |
| 订阅 | EnemyEvents.OnEnemyDied | Maintain 模式递减存活计数 |
| 响应 | GameLoopManager 状态 | Paused → 暂停调度，Playing → 恢复 |

## 9. 需要新增的文件

| 文件 | 内容 |
|------|------|
| `Assets/Data/Waves/waves.csv` | 波次时间线数据 |
| `Scripts/Spawning/WaveEvent.cs` | WaveEvent + PoolEntry + 枚举 |
| `Scripts/Spawning/WaveParser.cs` | CSV 解析器 |
| `Scripts/Spawning/WaveScheduler.cs` | 核心调度器 |
| `Scripts/Spawning/ISpawnPositionProvider.cs` | 位置策略接口 |
| `Scripts/Spawning/ScreenEdgeSpawnProvider.cs` | 屏幕边缘位置实现 |
| `Scripts/Spawning/IEnemyPool.cs` | 对象池接口 |

## 10. 需要修改的文件

| 文件 | 变更 |
|------|------|
| `Scripts/Game/EnemyGenerator.cs` | 替换为 WaveScheduler 的 MonoBehaviour 宿主，或直接重写 |

## 11. 不在本次范围内

- 对象池的具体实现（预热、扩容逻辑）— 单独 spec
- EnemyData ScriptableObject 定义 — 单独 spec
- 敌人特殊能力（Priest 治疗、Zealot 光环）— 敌人 AI 系统
- 塔建造系统集成 — 已有 GDD
- 难度缩放 / Act 分级 — 未来迭代
