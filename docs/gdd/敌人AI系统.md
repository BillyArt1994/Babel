# 敌人 AI 系统

> **状态：** 已完成
> **作者：** 独立开发者 + AI
> **最后更新：** 2026-03-28
> **实现支柱：** 支柱2（压力递增的绝望感）、支柱6（倒金字塔的绝望螺旋）

## 概述

敌人 AI 系统控制每个人类单位从出生到死亡的完整生命周期：接收对象池的单位实例，驱动其向通天塔移动，执行各类型专属特殊能力，接受玩家攻击伤害，并在死亡时清理和广播事件。

本系统是实现 `IDamageable` 接口的唯一地方，也是向全局广播 `EnemyEvents.OnEnemyDied` 的责任方。点击攻击系统（#8）和技能系统（#9）依赖这两个接口工作。

## 玩家幻想

服务支柱2（压力递增的绝望感）+ 支柱6（倒金字塔）。

不同敌人类型产生不同的行为压力：Priest 群体让玩家感到"我杀了他们但他们又回血了"的焦虑；Zealot 光环让玩家不得不在"先清最危险的" vs "先灭光环源"之间做战术决策。AI 的复杂性不来自复杂算法，而来自数值差异和特殊能力带来的策略层次。

## 详细设计

### 核心数据结构

```csharp
// 每个人类单位实例挂载的组件，实现 IDamageable 接口
public class EnemyUnit : MonoBehaviour, IDamageable
{
    // --- 初始化数据 ---
    private EnemyData _data;
    private EnemyData Data => _data;

    // --- 生命值 ---
    private float _currentHealth;
    public  bool  IsAlive   => _currentHealth > 0;
    public  Vector2 Position => transform.position;

    // --- 减速状态 ---
    private float _slowPercent;
    private float _slowTimer;

    // --- 神罚印记状态 ---
    private bool _isMarked;

    // --- 祭司治疗计时器 ---
    private float _healTimer;

    // --- 移动目标 ---
    private Vector2 _moveTarget;    // 每帧从 TowerBuildSystem.GetActiveLayerHeight() + TOWER_X 更新

    // --- 当前状态 ---
    private EnemyState _state;
}

public enum EnemyState { Moving, Arrived, Dead }

// 全局静态事件（所有系统共同使用的击杀通知）
public static class EnemyEvents
{
    public static event Action<EnemyData, Vector2> OnEnemyDied;
    public static void Broadcast_EnemyDied(EnemyData data, Vector2 pos)
        => OnEnemyDied?.Invoke(data, pos);
}

// 狂信徒光环注册表（静态，零 GC）
public static class ZealotAuraRegistry
{
    public static readonly List<EnemyUnit> ActiveZealots = new();
}
```

---

### 核心规则

#### A. 单位生命周期

```
对象池 → Initialize(data, spawnPos) → Moving → (到达塔基) → Arrived → 返回对象池
                                           ↓
                                  TakeDamage 致死 → Dead → 返回对象池
```

**`Initialize(EnemyData data, Vector2 spawnPos)` 步骤（OnEnable 时调用）：**
1. `_data = data`
2. `_currentHealth = data.baseHealth`
3. `_slowPercent = 0`，`_slowTimer = 0`，`_isMarked = false`
4. `_healTimer = HEAL_INTERVAL`（祭司专用计时器重置）
5. `_state = EnemyState.Moving`
6. 若 `data.enemyId == "zealot"`：`ZealotAuraRegistry.ActiveZealots.Add(this)`

**`OnDisable`（返回对象池时）：**
- `ZealotAuraRegistry.ActiveZealots.Remove(this)` — 无论是否为 Zealot，Remove 无副作用

---

#### B. 每帧移动（Update）

```csharp
void Update() {
    if (_state != EnemyState.Moving) return;
    if (!GameLoopManager.IsPlaying()) return;

    // 计算有效速度（减速 + 狂信徒光环叠加）
    float speed = _data.moveSpeed;
    if (_slowTimer > 0) {
        speed *= (1f - _slowPercent);
        _slowTimer -= Time.deltaTime;
    }
    speed *= GetZealotAuraMultiplier();   // 1.0 或 1.5

    // 更新目标（层切换时自动跟随）
    _moveTarget = new Vector2(TOWER_X, TowerBuildSystem.GetActiveLayerHeight());

    // 移动
    transform.position = Vector2.MoveTowards(transform.position, _moveTarget, speed * Time.deltaTime);

    // 到达检测
    if (Vector2.Distance(transform.position, _moveTarget) < ARRIVAL_THRESHOLD) {
        Arrive();
        return;
    }

    // 祭司治疗（边移动边治疗）
    if (_data.healPerSecond > 0) {
        RunPriestHeal();
    }
}
```

**狂信徒光环查询（O(k)，k = 场上 Zealot 数量，通常 < 5）：**
```csharp
float GetZealotAuraMultiplier() {
    foreach (var zealot in ZealotAuraRegistry.ActiveZealots) {
        if (zealot == this) continue;              // 自身不受自己光环影响
        if (!zealot.IsAlive) continue;
        float dist = Vector2.Distance(transform.position, zealot.Position);
        if (dist <= zealot.Data.speedAuraRadius) {
            return zealot.Data.speedAuraMultiplier; // 多个 Zealot 取最大值（简化）
        }
    }
    return 1.0f;
}
```

---

#### C. 伤害接受（IDamageable 实现）

```csharp
public void TakeDamage(float damage, float slowPercent, float slowDuration, bool hasExecute = false)
{
    if (!IsAlive) return;

    // 处决检查（血量低于阈值时伤害翻倍）
    if (hasExecute && (_currentHealth / _data.baseHealth) < EXECUTE_THRESHOLD) {
        damage *= EXECUTE_DAMAGE_MULT;   // 默认 2.0
    }

    // 神罚印记加成
    if (_isMarked) {
        damage *= MARK_DAMAGE_MULT;      // 默认 1.3
    }

    _currentHealth -= damage;

    // 减速效果
    if (slowDuration > 0 && slowPercent > _slowPercent) {  // 高覆盖低
        _slowPercent = slowPercent;
        _slowTimer   = slowDuration;
    }

    if (_currentHealth <= 0) Die();
}
```

**接口签名（更新自 #8 点击攻击系统）：**
```csharp
public interface IDamageable {
    void    TakeDamage(float damage, float slowPercent, float slowDuration, bool hasExecute = false);
    bool    IsAlive  { get; }
    Vector2 Position { get; }
    void    AddMark();
    void    RemoveMark();
}
```

> ⚠️ **接口更新说明**：`TakeDamage` 新增 `hasExecute` 可选参数；新增 `AddMark()` / `RemoveMark()`。需同步更新点击攻击系统（#8）GDD 中的 IDamageable 定义，以及技能系统（#9）中 AttackRequest 的 `hasExecute` 字段。

---

#### D. 到达塔基（Arrive）

```csharp
void Arrive() {
    _state = EnemyState.Arrived;
    TowerBuildSystem.ReportArrival(_data);
    // 对象池回收由 TowerBuildSystem.ReportArrival 内部触发
    // （TowerBuildSystem 负责调用 ObjectPool.Return(_data, gameObject)）
}
```

---

#### E. 死亡（Die）

```csharp
void Die() {
    if (_state == EnemyState.Dead) return;  // 防重复死亡
    _state = EnemyState.Dead;
    _currentHealth = 0;

    EnemyEvents.Broadcast_EnemyDied(_data, transform.position);
    // 统计系统、技能系统（瘟疫/狂怒）订阅此事件

    ObjectPool.Return(_data, gameObject);
    // OnDisable 会自动清理 ZealotAuraRegistry
}
```

---

#### F. 特殊能力

**祭司（Priest）— 行走中周期治疗：**
```csharp
void RunPriestHeal() {
    _healTimer -= Time.deltaTime;
    if (_healTimer > 0) return;
    _healTimer = HEAL_INTERVAL;   // 默认 1.0 秒

    Collider2D[] nearby = Physics2D.OverlapCircleAll(
        transform.position, _data.healRadius, ENEMY_LAYER_MASK);

    foreach (var col in nearby) {
        var unit = col.GetComponent<EnemyUnit>();
        if (unit != null && unit.IsAlive && unit != this) {
            unit.Heal(_data.healPerSecond * HEAL_INTERVAL);
        }
    }
}

public void Heal(float amount) {
    if (!IsAlive) return;
    _currentHealth = Mathf.Min(_currentHealth + amount, _data.baseHealth);
}
```

**狂信徒（Zealot）— 速度光环：**  
被动效果，由 `GetZealotAuraMultiplier()` 在其他单位的移动计算中应用。Zealot 自身不受自己光环影响，自身速度由 `EnemyData.moveSpeed` 决定（参考值 4.5）。

> ⚠️ **EnemyData 字段更新**：Zealot 的 `deathExplosionRadius` 和 `deathExplosionForce` 字段废弃，替换为：
> - `speedAuraRadius: float`（光环范围，参考值 4.0 单位）
> - `speedAuraMultiplier: float`（速度倍率，参考值 1.5 = +50%）
>
> 敌人数据库（#3）GDD 需同步更新此变更。

---

### 状态与转换

| 状态 | 描述 | 进入条件 | 退出条件 |
|------|------|---------|---------|
| Moving | 向塔移动，特殊能力运行中 | Initialize() 调用后 | 到达目标点（→ Arrived），TakeDamage 致死（→ Dead） |
| Arrived | 已贡献建造，待回收 | 到达 ARRIVAL_THRESHOLD | 立即回到对象池（同帧） |
| Dead | 已死亡，待回收 | TakeDamage 后 HP ≤ 0 | 立即回到对象池（同帧） |

**游戏状态响应：**

| GameState | 单位行为 |
|-----------|---------|
| Playing | 正常移动 + 特殊能力 |
| Paused | Update() 内 `IsPlaying()` 检查返回 false，停止所有逻辑 |
| LevelingUp | 同 Paused |
| Victory / Defeat | 同 Paused（单位原地停止） |

---

### 与其他系统的交互

| 方向 | 系统 | 接口 |
|------|------|------|
| 查询 | 游戏循环管理器（#1） | `IsPlaying()` — 每帧决定是否执行移动逻辑 |
| 调用 | 塔建造系统（#6） | `ReportArrival(EnemyData)` — 到达时通报 |
| 查询 | 塔建造系统（#6） | `GetActiveLayerHeight()` — 每帧更新移动目标 Y 坐标 |
| 订阅 | 塔建造系统（#6） | `OnActiveLayerChanged(layer, height)` — 层切换时更新目标 |
| 被调用 | 点击攻击系统（#8） | `IDamageable.TakeDamage()` — 受到攻击 |
| 被调用 | 技能系统（#9） | `IDamageable.AddMark()` / `RemoveMark()` — 神罚印记 |
| 广播 | 技能系统（#9） | `EnemyEvents.OnEnemyDied` — 触发击杀被动 |
| 广播 | 统计模块（#16 依赖） | `EnemyEvents.OnEnemyDied` — 计数击杀总数 |
| 通知 | 对象池系统（#5） | `ObjectPool.Return(_data, go)` — 死亡/到达后回收 |

---

## 公式

**1. 有效移动速度**
```
baseSpeed     = enemyData.moveSpeed
slowedSpeed   = baseSpeed × (1 - slowPercent)   [slowTimer > 0 时]
effectiveSpeed = slowedSpeed × zealotAuraMult     [附近有 Zealot 时]
zealotAuraMult = max(1.0, nearestZealot.speedAuraMultiplier)   [默认 1.5]
```

**2. 实际受到伤害**
```
finalDamage = baseDamage
            × (hasExecute && hp/maxHp < EXECUTE_THRESHOLD ? EXECUTE_DAMAGE_MULT : 1.0)
            × (_isMarked ? MARK_DAMAGE_MULT : 1.0)
```

**3. 治疗量（Priest，每次触发）**
```
healAmount = healPerSecond × HEAL_INTERVAL
currentHealth = clamp(currentHealth + healAmount, 0, baseHealth)
```

**4. 到达判断**
```
isArrived = Vector2.Distance(position, moveTarget) < ARRIVAL_THRESHOLD
```

## 边界情况

| 情况 | 处理方式 |
|------|---------|
| 同帧两次 TakeDamage 均致死 | `_state == Dead` 检查防重复执行，`OnEnemyDied` 只广播一次 |
| Priest 治疗已死亡单位 | `Heal()` 内 `IsAlive` 检查，不回血已死单位 |
| 层切换后 `_moveTarget` 更新延迟（下一帧才更新） | 最多延迟 1 帧，肉眼不可见；不需要订阅 `OnActiveLayerChanged`（每帧从 `GetActiveLayerHeight()` 取实时值即可） |
| 多个 Zealot 光环重叠 | 取最大倍率（简化版：遍历取第一个在范围内的），不叠加 |
| 单位受到 slowPercent = 1.0（完全停止） | TakeDamage 的 slowPercent 被技能数据库 `OnValidate()` 钳制到 0.99，不会完全停止 |
| 对象池回收后 TakeDamage 被调用（延迟的碰撞回调） | `IsAlive` 检查立即返回（`_currentHealth = 0`），安全无副作用 |
| Zealot 被 AOE 秒杀，同帧多个单位在光环范围内 | `OnDisable` 立即从 Registry 移除，同帧其他单位的 `GetZealotAuraMultiplier()` 若在 OnDisable 之后调用则正确返回 1.0；若在之前调用则还有一帧仍享受光环，可接受的 1 帧误差 |
| 游戏胜利/失败后场上有存活单位 | `IsPlaying() = false` 使 Update 停止，单位冻结；游戏循环管理器的 `OnVictory/OnDefeat` 可选择调用 `ReturnAllToPool()` 清场 |

## 依赖关系

| 类型 | 系统 | 性质 |
|------|------|------|
| 硬依赖 | 游戏循环管理器（#1） | `IsPlaying()` |
| 硬依赖 | 塔建造系统（#6） | `GetActiveLayerHeight()`、`ReportArrival()` |
| 被调用 | 点击攻击系统（#8） | IDamageable 接口实现 |
| 被调用 | 技能系统（#9） | AddMark/RemoveMark |
| 广播 | 技能系统（#9）、统计模块 | EnemyEvents.OnEnemyDied |
| 调用 | 对象池系统（#5） | ObjectPool.Return |

## 调节旋钮

| 旋钮 | 默认值 | 安全范围 | 极端行为 |
|------|--------|---------|---------|
| `ARRIVAL_THRESHOLD` | 0.1 单位 | 0.05 – 0.5 | 过大：单位未走完就算到达；过小：浮点精度问题 |
| `HEAL_INTERVAL` | 1.0 秒 | 0.5 – 3.0 | < 0.5：Physics2D 调用过于频繁 |
| `EXECUTE_THRESHOLD` | 0.25（25% HP） | 0.1 – 0.5 | > 0.5：几乎所有攻击都触发处决，失去意义 |
| `EXECUTE_DAMAGE_MULT` | 2.0 | 1.5 – 4.0 | > 3.0：单次点击秒杀高血量单位，破坏技能平衡 |
| `MARK_DAMAGE_MULT` | 1.3 | 1.1 – 2.0 | > 2.0：印记太强，必选神罚印记 |
| `TOWER_X` | 0（世界空间中心） | — | 由关卡设计决定，硬编码为场景常量 |

*注：Zealot 的 `speedAuraRadius`（4.0）和 `speedAuraMultiplier`（1.5）存储在 EnemyData，不在此系统中调整。*

## 视觉/音效需求

以下事件供粒子特效系统（#12）和音效系统（#20）订阅：

- `EnemyEvents.OnEnemyDied(data, pos)` — 触发死亡特效（爆血、消散等）
- 减速效果的视觉（如单位变蓝/变慢）由粒子特效系统订阅 `OnAttackExecuted`（slowPercent > 0 时）来决定
- Zealot 光环视觉（如周围单位发光）：技术上需粒子特效系统轮询 `ZealotAuraRegistry.ActiveZealots`，或 Zealot 自身广播 `OnZealotAuraChanged` 事件（MVP 可暂不实现，后续再加）

## UI 需求

本系统无直接 UI 需求。血条显示（若有）应由独立的敌人血条 UI 组件订阅 `EnemyUnit` 的 `OnHealthChanged` 事件实现，而非由本系统直接控制。MVP 阶段可暂无血条。

## 验收标准

1. **生命周期**：单位从对象池获取后正确 Initialize，死亡后正确 Return，无内存泄漏
2. **移动**：单位从屏幕边缘直线移动至塔基目标，到达后消失（TowerBuildSystem.ReportArrival 被调用）
3. **层切换**：`OnActiveLayerChanged` 后，在途单位在下一帧更新目标（无需订阅，每帧取实时值）
4. **减速**：`TakeDamage(slowPercent=0.5, slowDuration=2)` → 单位速度减半 2 秒后恢复
5. **处决**：血量 < 25% 时，`hasExecute=true` 的攻击造成双倍伤害
6. **神罚印记**：`AddMark()` 后，下次受到伤害增加 30%；死亡后印记自然消失
7. **祭司治疗**：每 1 秒治疗 `healRadius` 内所有活体单位，治疗量正确
8. **狂信徒光环**：Zealot 存活时，4 单位内其他人类速度 ×1.5；Zealot 死亡后光环立即消失（下帧生效）
9. **击杀事件**：死亡时 `EnemyEvents.OnEnemyDied` 广播一次且仅一次（防重复死亡测试）
10. **游戏暂停**：Paused 状态下所有单位停止移动和治疗，但保持在场上

## 开放问题

| # | 问题 | 优先级 | 备注 |
|---|------|--------|------|
| 1 | 处决（execute）阈值是否存储在 SkillData 还是作为系统常量？ | 中 | 当前设计：`EXECUTE_THRESHOLD = 0.25f` 作为 EnemyUnit 的 SerializeField 旋钮。也可放到 SkillData（灵活但复杂）。MVP 建议保持常量。 |
| 2 | 神罚印记是否有持续时间（如 10 秒后自动消失）？ | 低 | 当前设计：永久标记至死亡。若要加持续时间，需要在 EnemyUnit 维护 `_markTimer`，日后迭代。 |
| 3 | 多个 Zealot 光环是否叠加（多个 Zealot 时速度超过 1.5×）？ | 低 | 当前设计：取最大值（不叠加）。叠加可能使人类移速过快，建议保持不叠加。 |
| 4 | 层宽度是否影响人类横向移动分布（塔建造系统开放问题 #1）？ | 低 | 与敌人生成系统（#7）和塔建造系统（#6）协商，当前 AI 设计中 `TOWER_X` 固定，所有单位目标 X 相同，无横向分布逻辑。若需分散，改为 `targetX = TOWER_X ± random(layerWidth/2)`。 |
| 5 | EnemyData 字段更新：`deathExplosionRadius` / `deathExplosionForce` → `speedAuraRadius` / `speedAuraMultiplier`。 | 高 | 需在下一轮设计回顾时同步更新敌人数据库（#4）GDD 中 Zealot 的字段定义和参考数值。 |
