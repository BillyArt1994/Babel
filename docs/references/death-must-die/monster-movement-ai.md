# DMD 怪物移动 / AI 系统 — 逆向分析

> **分析日期**：2026-06-04
> 源码根：`H:\Death_Must_Die\DeathMustDie\Assets\Scripts\Death\`（下文 `<S>\`）
> 配置根：`H:\Death_Must_Die\DeathMustDie_Config\`

---

## 0. 结论先行

DMD 怪物移动是 **100% 数据驱动的双层架构**：

| 层次 | 职责 | 配置来源 |
|------|------|---------|
| **决策层（AI 行为树）** | 决定"当前做什么"——追击、施法、游荡…… | `Ai.ini`（TOML 格式）；Monsters.csv 的 `ai` 列指向 AI 模板名 |
| **执行层（Steering）** | 决定"每帧怎么移动"——多种转向行为按权重叠加，输出加速度 | `SteeringSettings`（ScriptableObject）里的 `Behaviours[]` 列表 |

**对 Babel 最直接有价值的结论**：

- 「群聚靠近队友」的需求，DMD 已有 `Steering_Cohesion`（向感知范围内队友质心靠近）+ `Steering_Separation`（避免互相重叠）完整实现，核心思路是 `Physics2D.OverlapCircleNonAlloc` + 质心计算 + 朝质心方向求加速度。
- DMD 完整双层架构对 Babel **过重**；Babel 的塔防分层路径移动比 DMD 简单，只需借鉴 Cohesion 的感知-质心-移动思路，而不是引入完整行为树 + Steering 权重叠加系统。

---

## 1. 决策层：AI 行为树

### 1.1 关键类

| 类名 | 文件路径 | 一句话职责 |
|------|---------|-----------|
| `Controller_Ai` | `<S>\Death.Run.Behaviours.AI\Controller_Ai.cs` | 怪物 AI 控制器；持有 `MonsterAiTree`；`ScheduledUpdate`（每 10 帧）跑决策，`FixedUpdate` 调 `_brain.Current?.UpdateMovement()` 驱动转向 |
| `MonsterAiTree` | 同目录 | 行为树根，包装 `IAiNode`；持有当前活动节点 `Current` |
| `AiNodeTemplate` | 同目录 | AI 节点工厂模板，从 Ai.ini 解析后缓存，`Instantiate()` 创建运行时节点实例 |
| `AiBindings` | 同目录 | CSV 的 `ais_N`/`ability_N` 槽位 → 能力代码映射；创建 `MonsterAiTree` 实例 |
| `AiNode_Approach` | 同目录 | 基础追踪节点，提供 `MoveTo()` / `MaintainDistance()` 接口，内部调用 `SteeringAgent2D` |
| `AiNode_InsectMove` | 同目录 | 昆虫跳跃式移动，参数 `ApproachConeDegrees`、`MoveDistance.Min/Max` |
| `TomlAi` | `<S>\Death.Run.Behaviours.AI\TomlAi.cs` | Ai.ini 解析器；`_parserLookup` 字典映射节点类型字符串 → 工厂（共约 28 种） |

### 1.2 Ai.ini 节点类型枚举

| 节点类型 | 分类 | 关键参数 |
|---------|------|---------|
| `c_random` | 控制（随机选子节点） | `weight`、子节点列表 |
| `c_selector` | 控制（依序选第一个成功子节点） | 子节点列表 |
| `c_sequence` | 控制（依序执行，有一个失败就停） | 子节点列表 |
| `?_in_range` | 条件 | `range` |
| `?_hp_below` | 条件 | `percent` |
| `?_repeat_guard` | 条件/装饰 | `max_repeats`、`reset_timer`、`child` |
| `a_approach` | 动作 | `stop_distance`、`maintain_distance`、`from_attack_pos` |
| `a_melee` | 动作 | `code_slot`、`timeout` |
| `a_missile` | 动作 | `code_slot`、`timeout` |
| `a_insect_move` | 动作 | `approach_cone`、`move_distance{min, max}` |
| `a_insect_cast` | 动作 | `code_slot`、`timeout` |
| `a_wander` | 动作 | `distance`、`timeout` |
| ……共约 28 种 | — | 详见 `TomlAi.cs` 的 `_parserLookup` 字典 |

### 1.3 Ai.ini 真实示例

```ini
[ai_insect]
type = "c_random"
children = [
  { weight = 1000, type = "?_repeat_guard", max_repeats = 3, reset_timer = 2.0,
    child = { type = "a_insect_move", code_slot = "move",
              approach_cone = 45.0, move_distance = { min = 1.8, max = 3.5 } } },
  { weight = 100, type = "a_insect_cast", code_slot = "cast", timeout = 0.6 },
  { weight = 1,   type = "a_missile",     code_slot = "shockwave", timeout = 0.6 },
]

[ai_golem]
type = "a_approach"
stop_distance = 1.0
maintain_distance = true
from_attack_pos = true
```

---

## 2. 执行层：Steering 转向系统

### 2.1 关键类

| 类名 | 文件路径 | 一句话职责 |
|------|---------|-----------|
| `SteeringAgent2D` | `<S>\Death.Steering\SteeringAgent2D.cs` | 转向代理引擎；持有 `SteeringSettings`；`FixedUpdate → Steer() → CalculateAcceleration()` 遍历 `Behaviours[]` 加权求和 → `Rigidbody2D.MovePosition` |
| `SteeringSettings` | `<S>\Death.Steering\SteeringSettings.cs` | ScriptableObject；含 `MaxAcceleration` + `Behaviours[]`（每个元素含行为实例 + 权重） |
| `SteeringBehaviour` | `<S>\Death.Steering.Behaviours\SteeringBehaviour.cs` | 抽象基类；接口 `Evaluate(agent, targetPos, stoppingDistance) → Vector2 加速度` |

### 2.2 11 种内置 Steering 行为库

| 类名 | 职责 | 关键参数 |
|------|------|---------|
| `Steering_Seek` | 直线追目标（不减速） | `_maxAcceleration`、`_stopDistance`、`_overshootTarget` |
| `Steering_Arrive` | 渐进减速到达目标 | `_maxAcceleration`、`_slowRadius` |
| `Steering_MaintainPos` | 原地停止（零加速度） | 无参 |
| `Steering_Cohesion` | 靠近检测范围内队友质心 | `_detectionRange`、`_targetLayers`；内含 `_seek` 子行为 |
| `Steering_Separation` | 远离过近单位，避免重叠 | `_detectionRange`、`_acceleration`、`_mode`（InverseSquare / Linear）、`_decayCoefficient`、`_ignoreIfLowerMass` |
| `Steering_Avoidance` | 预测性碰撞躲避（动态单位） | `_detectionRange`、`_maxAcceleration`、`_targetLayers` |
| `Steering_MakeWay` | 给更高质量单位让路 | `_detectionRange`、`_acceleration`、`_decayCoefficient` |
| `Steering_ObstacleAvoidance` | 躲静态障碍（触须 Raycast） | `_obstacleMask`、`_maxAcceleration`、`_lookahead` |
| `Steering_WallAvoidance` | 躲墙（触须 Raycast） | `_wallMask`、`_lookahead`、`_avoidDistance`；内含 `_seek` |
| `Steering_Bomber` | 投掷者曲线运动 | `_accelerationPercent` |
| `Steering_Flee`（推测） | 逃离目标（Seek 反向） | — |

### 2.3 Steering_Cohesion 核心逻辑（Babel 最关心）

文件：`<S>\Death.Steering.Behaviours\Steering_Cohesion.cs`

```
每帧 Evaluate(agent, targetPos, stoppingDistance):
  1. Physics2D.OverlapCircleNonAlloc(agent.Position, _detectionRange, buffer, _targetLayers)
     → 获取范围内最多 ~6 个单位
  2. 遍历 buffer，累加坐标 → centerOfMass = sum / count
  3. _seek.Evaluate(agent, centerOfMass, stoppingDistance)
     → 以质心为目标调用内部 Seek，输出朝质心方向加速度
  4. 动态追踪：每帧重算质心，队形变化自动响应
```

### 2.4 Steering_Separation 核心逻辑

文件：`<S>\Death.Steering.Behaviours\Steering_Separation.cs`

```
遍历范围内每个邻近单位（dist = 距离）：
  InverseSquare 模式：accel += direction * (_decayCoefficient / dist²)
  Linear 模式：       accel += direction * _acceleration * ((range - dist) / range)
  若 _ignoreIfLowerMass == true 且邻近单位质量更低，则跳过
结果：多个排斥加速度叠加，推开过近单位
```

---

## 3. 数据流图

### 3.1 初始化流程

```
Monsters.csv
  └─ "ai" 列 = "ai_missile" / "ai_melee" / "ai_insect" …
       │
       ▼
  Parser.ParseMonster()
  └─ 查 AiTable["ai_xxx"] → 取 AiNodeTemplate
       │
       ▼
  AiBindings.Create(template, abilitySlots)
  └─ 创建 MonsterAiTree 实例（含绑定能力代码映射）
       │
       ▼
  Controller_Ai.Awake()
  └─ 缓存 SteeringAgent2D（已挂 SteeringSettings）
  └─ new MonsterAiTree → 存入 _brain
```

### 3.2 每帧运行流程

```
Controller_Ai.ScheduledUpdate（每 10 帧触发）
  └─ _brain.Update(context) → 行为树决策
       └─ 设定 _brain.Current（当前活动节点，如 AiNode_Approach）

Controller_Ai.FixedUpdate
  └─ MovementEnabled 为 true 时：
       └─ _brain.Current.UpdateMovement()
            └─ node.steering.MoveTo(targetPos, stoppingDistance)
                 │
                 ▼
           SteeringAgent2D.FixedUpdate
             └─ Steer()
                  └─ CalculateAcceleration()
                       遍历 Behaviours[] → 每个 Evaluate() 返回 Vector2
                       加权叠加，受 MaxAcceleration 上限限制
                  └─ _velocity += acceleration * dt
             └─ Rigidbody2D.MovePosition(_velocity)
```

---

## 4. CSV / 配置字段速查

### 4.1 Monsters.csv 关键列

| 列名 | 说明 | 示例值 |
|------|------|-------|
| `entity` | 预制体资源路径 | `Entities/Monsters/SkeletonArcher/SkeletonArcher_Fab` |
| `variant` | 视觉变体 | `default` |
| `ai` | AI 模板代码，指向 Ai.ini 里的节点名 | `ai_missile` / `ai_melee` / `ai_insect` |
| `ais_1..8` | AI 节点代码槽（最多 8 个） | `miss`、`move`、`cast` … |
| `ability_1..8` | 与 `ais_N` 对应的能力绑定代码 | `Archer_Missile` |

示例行：
```
entity=Entities/Monsters/SkeletonArcher/SkeletonArcher_Fab, variant=default,
ai=ai_missile, ais_1=miss, ability_1=Archer_Missile
```

### 4.2 配置文件总览

| 文件 | 格式 | 作用 |
|------|------|------|
| `Monsters.csv` | CSV | 每种怪物的实体路径、AI 模板、能力槽 |
| `MonAbilities.csv` | CSV | 怪物能力参数（伤害、射程、冷却等） |
| `Ai.ini` | TOML | AI 行为树模板定义（`[ai_xxx]` 节点块） |
| `SteeringSettings_*.asset` | ScriptableObject | 各 AI 模板对应的转向行为组合 + 权重 |

---

## 5. 映射 Babel — 借鉴优先级与实施建议

### 5.1 现状对比

| 维度 | DMD | Babel（当前） |
|------|-----|-------------|
| 移动架构 | 双层（AI 行为树 + Steering 加权叠加） | 硬编码状态机（`Enemy.cs`：MovingToBuildPoint→Building→…） |
| 配置方式 | `Ai.ini` + CSV `ai` 列 | 无，完全硬编码 |
| 群聚行为 | `Steering_Cohesion` + `Steering_Separation` 组合 | 无 |
| 运动维度 | 全 2D 自由移动（俯视角） | 横版分层路径移动，主要是水平 + 爬楼 |

### 5.2 不该照搬的部分（过重）

DMD 的完整双层架构（28 种 Ai.ini 节点 + 11 种 Steering 行为 + ScriptableObject 权重配置）为自由移动的俯视角 RPG 设计。Babel 是 **2D 横版分层塔防**：

- 移动主轴是水平方向沿固定 Path 路径
- 垂直方向只有"爬楼"一种特殊动作
- 敌人本就沿路径排成一列，重叠问题轻微

引入完整 Steering 系统会带来不必要的物理开销和复杂度，违背 Babel「数据全在 CSV、逻辑尽量简单」的设计原则。

### 5.3 该借鉴的核心思路

#### 群聚跟随（支援兵 SupportMovement）

借鉴自 `Steering_Cohesion`，实施要点：

```
每帧（或每 N 帧）：
  Physics2D.OverlapCircleNonAlloc(self.position, perceptionRadius,
                                   buffer, enemyLayer)
  → 过滤掉自身，计算存活队友质心 centerOfMass
  → 朝 centerOfMass.x 方向水平移动（忽略 y，保持分层路径约束）
  → 若质心距离 < 停止阈值，停止（已在堆中）
```

关键参数设计建议：

| 参数 | 说明 | 建议值 |
|------|------|-------|
| `perceptionRadius` | 感知半径（找队友范围） | 比光环半径大约 2x，如光环 3 → 感知 6~8 |
| `stopThreshold` | 到达质心的停止距离 | 约 0.5~1.0 单位 |
| 配置方式 | 在 `enemies.csv` 新增列（如 `perception_radius`） | 保持 Babel「数据全在 CSV」约定 |

> 注意：感知半径必须**大于**光环半径，否则当自身已身处队友堆时，质心 ≈ 自身位置，支援兵会原地不动，无法自动靠拢。

#### Separation（可选，先不做）

Babel 敌人沿路径水平排布，重叠不严重。若后续出现支援兵与队友完全重叠的观感问题，再补一个最小斥力（参考 `Steering_Separation` 的 Linear 模式即可）。

### 5.4 Babel 重构路线对应

Babel 正在将移动逻辑重构为 `IEnemyMovement` 策略接口（对称于已有的 `IEnemyAbility`），CSV 用 `moveMode` 列配置：

| `moveMode` 值 | 对应策略类 | 说明 |
|--------------|-----------|------|
| （空） | `BuilderMovement` | 原有建造者逻辑，沿 Path 占点建造 |
| `support` | `SupportMovement` | 新增支援兵，借鉴 Cohesion 思路跟随队友群体 |

DMD 的 Cohesion 思路直接落地为 `SupportMovement.Update()` 的主体逻辑，是两套架构之间最清晰的一对一映射。

---

## 6. 证据表

| 问题 | 答案 | 来源文件:类/方法 |
|------|------|----------------|
| 每帧移动入口在哪？ | `Controller_Ai.FixedUpdate` 调 `_brain.Current.UpdateMovement()` | `<S>\Death.Run.Behaviours.AI\Controller_Ai.cs` : `FixedUpdate` |
| AI 决策频率？ | 每 10 帧一次（`ScheduledUpdate`） | `<S>\Death.Run.Behaviours.AI\Controller_Ai.cs` : `ScheduledUpdate` |
| Steering 加速度如何合成？ | `CalculateAcceleration()` 遍历 `Behaviours[]` 加权叠加，受 `MaxAcceleration` 限制 | `<S>\Death.Steering\SteeringAgent2D.cs` : `CalculateAcceleration` |
| 转向参数在哪里配置？ | `SteeringSettings` ScriptableObject，含 `MaxAcceleration` + `Behaviours[]` | `<S>\Death.Steering\SteeringSettings.cs` |
| Cohesion 怎么找队友？ | `Physics2D.OverlapCircleNonAlloc(pos, _detectionRange, buffer, _targetLayers)` | `<S>\Death.Steering.Behaviours\Steering_Cohesion.cs` : `Evaluate` |
| Cohesion 目标点怎么算？ | 范围内所有命中单位坐标求平均 = 质心 `centerOfMass = sum / count` | `<S>\Death.Steering.Behaviours\Steering_Cohesion.cs` : `Evaluate` |
| Separation 斥力公式？ | InverseSquare: `accel = coeff / dist²`；Linear: `accel = acc * (range-dist)/range` | `<S>\Death.Steering.Behaviours\Steering_Separation.cs` : `Evaluate` |
| AI 模板从哪里解析？ | `Ai.ini`（TOML）；解析器 `TomlAi.cs` 的 `_parserLookup` 字典（约 28 种节点类型） | `<S>\Death.Run.Behaviours.AI\TomlAi.cs` : `_parserLookup` |
| 怪物 CSV 的 ai 列如何关联到行为树？ | `Parser.ParseMonster` 查 `AiTable[ai_xxx]` → `AiBindings.Create()` → `MonsterAiTree` | `<S>\Death.Run.Behaviours.AI\AiBindings.cs` : `Create` |
| 昆虫移动节点参数？ | `ApproachConeDegrees`、`MoveDistance.Min/Max` | `<S>\Death.Run.Behaviours.AI\AiNode_InsectMove.cs` |

---

*文档建立于 2026-06-04。基于 DMD 反编译源码查证，路径均已验证。*
