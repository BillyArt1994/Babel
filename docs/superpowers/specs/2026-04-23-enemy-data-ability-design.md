# 敌人数据层 + 能力系统设计

> **状态：** 已批准
> **日期：** 2026-04-23
> **范围：** EnemyData CSV 定义 / EnemyParser / EnemyDatabase / IEnemyAbility 接口 / HealAura + SpeedAura 实现 / Enemy.Init 集成
> **参考：** Death Must Die（CSV 单一数据源 + 运行时动态挂载能力）

## 1. 设计决策摘要

| 决策点 | 结论 | 理由 |
|--------|------|------|
| 数据存储 | CSV（enemies.csv） | 与 skills.csv / waves.csv 一致，数据驱动 |
| 属性列表 | 8 基础属性 + 4 能力属性 | 覆盖 5 种敌人需求，不过度设计 |
| 敌人能力架构 | 独立 IEnemyAbility 接口 | 敌人能力（辅助/增强）与玩家技能（进攻）职责不同，不混用 |
| 能力创建 | CSV 配置 abilityType + switch-case 创建 | 只有 2 种能力，不需要工厂/数据库 |
| 能力生命周期 | 只要敌人活着就生效，与移动状态无关 | 简单清晰，Priest 边走边治疗，Zealot 边走边加速 |
| 能力冷却 | 各实现自行管理 CD | HealAura 有 CD，SpeedAura 无 CD（被动光环） |
| 碰撞半径 | 不放 EnemyData，在 Prefab Collider2D 上配 | 运行时由物理系统读取 |

## 2. enemies.csv 格式

**文件路径：** `Assets/Data/Enemies/enemies.csv`

```csv
enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,abilityType,abilityRadius,abilityValue,abilityCooldown
worker,工人,30,2.0,25,1,1,Enemies/Worker,,,,
elite,精英,120,3.0,25,1,5,Enemies/Elite,,,,
priest,祭司,60,1.5,25,1,3,Enemies/Priest,heal_aura,3.0,10,2.0
engineer,工程师,60,2.0,50,2,3,Enemies/Engineer,,,,
zealot,狂信者,20,4.5,25,1,2,Enemies/Zealot,speed_aura,4.0,1.5,0
```

**字段说明：**

| 字段 | 类型 | 说明 |
|------|------|------|
| `enemyId` | string | 唯一标识，waves.csv 的 enemyPool 引用此 ID |
| `enemyName` | string | 显示名称 |
| `hp` | float | 生命值 |
| `moveSpeed` | float | 移动速度（Unity 单位/秒） |
| `buildContribution` | int | 每次到达 BuildPoint 贡献的建造量 |
| `buildCharges` | int | 可建造次数（耗尽后回收） |
| `expReward` | int | 击杀奖励经验值 |
| `prefab` | string | Prefab 的 Resources 路径 |
| `abilityType` | string | 能力类型（空=无，heal_aura/speed_aura） |
| `abilityRadius` | float | 能力作用半径 |
| `abilityValue` | float | 能力数值（治疗量/速度倍率） |
| `abilityCooldown` | float | 能力冷却时间（秒），0=无冷却 |

## 3. EnemyData 数据结构

```csharp
public class EnemyData
{
    public string EnemyId = "";
    public string EnemyName = "";
    public float Hp;
    public float MoveSpeed;
    public int BuildContribution;
    public int BuildCharges;
    public int ExpReward;
    public string Prefab = "";

    // 能力
    public string AbilityType = "";
    public float AbilityRadius;
    public float AbilityValue;
    public float AbilityCooldown;
}
```

## 4. EnemyParser + EnemyDatabase

### EnemyParser

静态 CSV 解析器，与 SkillParser / WaveParser 同风格：
- 解析 enemies.csv 为 `List<EnemyData>`
- BOM 处理、列名映射、类型转换
- 必填列验证：enemyId, enemyName, hp, moveSpeed, buildContribution, buildCharges, expReward, prefab
- 能力列可选（空=无能力）

### EnemyDatabase

静态数据库，启动时加载：

```csharp
public static class EnemyDatabase
{
    private static Dictionary<string, EnemyData> _byId;

    public static void Init(string csvText);
    public static EnemyData GetById(string enemyId);
    public static IReadOnlyList<EnemyData> GetAll();
}
```

WaveScheduler 在 SpawnOneEnemy 时通过 `EnemyDatabase.GetById(enemyId)` 获取敌人配置。

## 5. IEnemyAbility 接口

```csharp
public interface IEnemyAbility
{
    void Init(Enemy owner, EnemyData data);
    void Tick(float deltaTime);
    void OnRemoved();
}
```

**生命周期：**
- `Init`：Enemy 出生时调用，从 EnemyData 读取能力参数
- `Tick`：Enemy.Update 中每帧调用（不管移动状态，只要活着就调用）
- `OnRemoved`：Enemy 死亡或 buildCharges 耗尽回收时调用

### 能力创建（switch-case）

在 Enemy.Init 中：

```csharp
_ability = data.AbilityType switch
{
    "heal_aura" => new HealAura(),
    "speed_aura" => new SpeedAura(),
    _ => null
};
_ability?.Init(this, data);
```

## 6. 友军范围检测方式

光环能力使用 `Physics2D.OverlapCircleNonAlloc` + `LayerMask` 检测范围内友军。

**前置要求：** Unity 项目中配置 "Enemy" Layer，所有敌人 Prefab 的 GameObject Layer 设为 "Enemy"。

```csharp
private static readonly Collider2D[] _auraBuffer = new Collider2D[64];
private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

// 检测范围内友军
int count = Physics2D.OverlapCircleNonAlloc(owner.Position, radius, _auraBuffer, EnemyLayerMask);
for (int i = 0; i < count; i++)
{
    if (_auraBuffer[i].TryGetComponent<Enemy>(out var enemy) && enemy != _owner && enemy.IsAlive)
    {
        // 治疗 / 加速
    }
}
```

**优势：** NonAlloc 零 GC，LayerMask 底层 C++ 过滤非 Enemy 物体，与 DamageEffect 的 Physics2D 检测方式一致。

## 7. HealAura 实现

**行为：** 每隔 `abilityCooldown` 秒，对半径 `abilityRadius` 内的友方敌人恢复 `abilityValue` 点 HP。

```csharp
public class HealAura : IEnemyAbility
{
    private Enemy _owner;
    private float _radius;
    private float _healAmount;
    private float _cooldown;
    private float _cdTimer;

    private static readonly Collider2D[] _buffer = new Collider2D[64];
    private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

    public void Init(Enemy owner, EnemyData data)
    {
        _owner = owner;
        _radius = data.AbilityRadius;
        _healAmount = data.AbilityValue;
        _cooldown = data.AbilityCooldown;
        _cdTimer = _cooldown;
    }

    public void Tick(float deltaTime)
    {
        _cdTimer -= deltaTime;
        if (_cdTimer > 0) return;
        _cdTimer = _cooldown;

        int count = Physics2D.OverlapCircleNonAlloc(_owner.Position, _radius, _buffer, EnemyLayerMask);
        for (int i = 0; i < count; i++)
        {
            if (_buffer[i].TryGetComponent<Enemy>(out var enemy) && enemy != _owner && enemy.IsAlive)
            {
                enemy.Heal(_healAmount);
            }
        }
    }

    public void OnRemoved() { }
}
```

**注意：** Enemy 需新增 `Heal(float amount)` 方法：`HP = Mathf.Min(HP + amount, maxHp)`。

## 8. SpeedAura 实现

**行为：** 存活时每 0.5 秒扫描半径 `abilityRadius` 内友方敌人，设置速度倍率 `abilityValue`。

```csharp
public class SpeedAura : IEnemyAbility
{
    private Enemy _owner;
    private float _radius;
    private float _speedMultiplier;
    private float _checkTimer;
    private const float CHECK_INTERVAL = 0.5f;

    private static readonly Collider2D[] _buffer = new Collider2D[64];
    private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

    public void Init(Enemy owner, EnemyData data)
    {
        _owner = owner;
        _radius = data.AbilityRadius;
        _speedMultiplier = data.AbilityValue;
    }

    public void Tick(float deltaTime)
    {
        _checkTimer -= deltaTime;
        if (_checkTimer > 0) return;
        _checkTimer = CHECK_INTERVAL;

        int count = Physics2D.OverlapCircleNonAlloc(_owner.Position, _radius, _buffer, EnemyLayerMask);
        for (int i = 0; i < count; i++)
        {
            if (_buffer[i].TryGetComponent<Enemy>(out var enemy) && enemy != _owner && enemy.IsAlive)
            {
                enemy.ApplySpeedBuff(_speedMultiplier);
            }
        }
    }

    public void OnRemoved() { }
}
```

**速度 Buff 机制（Buff 栈模式）：**

光环每 0.5 秒给范围内友军施加一个 **0.6 秒的短时限 Buff**。友军走出范围或 Zealot 死亡后，Buff 自然过期，加速消失。

Enemy 上用 2 个字段实现（当前只有速度 Buff 一种，YAGNI 不做通用容器）：

```csharp
// Enemy 新增
private float _speedBuffTimer;
private float _speedBuffMult = 1.0f;

public float EffectiveSpeed => MoveSpeed * _speedBuffMult;

public void ApplySpeedBuff(float mult, float duration)
{
    _speedBuffMult = Mathf.Max(_speedBuffMult, mult);   // 多光环取最大
    _speedBuffTimer = Mathf.Max(_speedBuffTimer, duration); // 取最长时间
}

void TickBuffs(float dt)
{
    if (_speedBuffTimer > 0)
    {
        _speedBuffTimer -= dt;
        if (_speedBuffTimer <= 0) _speedBuffMult = 1.0f; // 过期恢复
    }
}
```

SpeedAura 调用：`enemy.ApplySpeedBuff(1.5f, 0.6f)`（0.5 秒扫描间隔 + 0.6 秒 Buff 时限 = 范围内永不过期，离开后 0.6 秒消失）。

移动时使用 `EffectiveSpeed` 而非 `MoveSpeed`。未来需要更多 Buff 类型时再提取为通用容器。

## 8. Enemy.Init 集成

Enemy.Init 扩展为：

```csharp
public void Init(Babel.Path startPath, EnemyData data, int waveEventId)
{
    // 基础属性
    HP = data.Hp;
    MovementSpeed = data.MoveSpeed;
    buildAbility = data.BuildContribution;
    buildCharges = data.BuildCharges;
    currentPath = startPath;
    waveEventId = eventId;

    // 移动状态机重置
    _moveState = EnemyMoveState.MovingToBuildPoint;
    _targetBuildPointIndex = -1;
    FindNextTarget();

    // 能力初始化
    _ability?.OnRemoved();
    _ability = data.AbilityType switch
    {
        "heal_aura" => new HealAura(),
        "speed_aura" => new SpeedAura(),
        _ => null
    };
    _ability?.Init(this, data);
}
```

**Init 签名变更：** 从 `Init(Path, int charges, int eventId)` 改为 `Init(Path, EnemyData, int eventId)`。EnemyData 包含了所有需要的数据。

## 9. Enemy.Update 流程

```csharp
void Update()
{
    // 1. 死亡检查
    if (HP <= 0) { HandleDeath(); return; }

    // 2. 能力 Tick（只要活着就执行，不管移动状态）
    _ability?.Tick(Time.deltaTime);

    // 3. 移动状态机
    switch (_moveState) { ... }
}
```

## 10. 与现有系统的集成变更

| 变更点 | 说明 |
|--------|------|
| WaveScheduler.SpawnOneEnemy | 从 EnemyDatabase 获取 EnemyData，传给 Enemy.Init |
| Enemy.Init 签名 | 从 (Path, int, int) 改为 (Path, EnemyData, int) |
| Enemy 新增 | `_ability` 字段、Heal() 方法、SpeedMultiplier 属性 |
| Global.Exp.Value++ | 改为 `Global.Exp.Value += data.ExpReward` |

## 11. 需要新增的文件

| 文件 | 内容 |
|------|------|
| `Assets/Data/Enemies/enemies.csv` | 敌人数据 |
| `Scripts/Spawning/EnemyData.cs` | EnemyData 数据结构 |
| `Scripts/Spawning/EnemyParser.cs` | CSV 解析器 |
| `Scripts/Spawning/EnemyDatabase.cs` | 按 ID 查询的静态数据库 |
| `Scripts/Spawning/IEnemyAbility.cs` | 能力接口 |
| `Scripts/Spawning/Abilities/HealAura.cs` | 治疗光环 |
| `Scripts/Spawning/Abilities/SpeedAura.cs` | 速度光环 |

## 12. 需要修改的文件

| 文件 | 变更 |
|------|------|
| `Scripts/Game/Enemy.cs` | Init 签名变更 + _ability 集成 + Heal() + SpeedMultiplier |
| `Scripts/Spawning/WaveScheduler.cs` | SpawnOneEnemy 查 EnemyDatabase + 传 EnemyData |

## 13. 不在本次范围内

- 对象池的具体实现
- Prefab 制作（5 种敌人的 Prefab）
- 敌人动画/视觉
- 更多能力类型扩展
