# Trigger/Effect 运行时执行管线设计

> **状态：** 已批准
> **日期：** 2026-04-22
> **范围：** TriggerContext / TriggerBase / IEffect / 4 种 Trigger + 4 种 Effect / SkillFactory / EffectManager / SkillSystem 运行时逻辑
> **参考：** Death Must Die 技能架构（DMD 为纯抽象类 + 字典式 TriggerArguments，本项目简化为固定 struct）

## 1. 设计决策摘要

| 决策点 | 结论 | 理由 |
|--------|------|------|
| Context 类型 | 固定 struct `TriggerContext` | 4 种 Trigger 规模下类型安全 > 灵活性，零 GC |
| 职责划分 | Trigger 只管"何时触发"，Effect 自理"怎么做" | DMD 同款模式，单一职责，解耦 |
| 暴击/倍率 | Effect 内部查 EffectManager，不在 Trigger | Trigger 不需要知道战斗数据 |
| Trigger 基类 | 纯抽象类 `TriggerBase`（无接口） | 参考 DMD，Trigger 无需 MonoBehaviour 兼容 |
| 基类职责 | 只存回调 + 提供 Fire()，不含冷却/概率 | 只有 OnClick 用冷却，放基类是过度抽象 |
| 工厂模式 | switch-case，不用反射 | 4+4 种 case，规模不需要反射 |
| EffectManager | SkillSystem 内部成员，非全局单例 | 避免全局状态污染 |

## 2. 核心数据结构

### TriggerContext

```csharp
public struct TriggerContext
{
    public Vector2 worldPos;       // 触发位置（所有 Trigger 都填）
    public float chargeRatio;      // 蓄力比：OnClick 填 0~1，其他 = 1.0
    public IDamageable target;     // 命中目标：OnHit 填，其他 = null
    public bool isPassive;         // 是否为被动触发（阻断 OnHitTrigger 递归）
}
```

- OnClickTrigger → `isPassive = false`
- OnHitTrigger / OnTimerTrigger / OnKillTrigger → `isPassive = true`

### IDamageable

```csharp
public interface IDamageable
{
    void TakeDamage(float damage, bool isCrit);
    Vector2 Position { get; }
    bool IsAlive { get; }
}
```

Enemy 实现此接口。

### Skill

```csharp
public class Skill
{
    public SkillConfig Config { get; }
    public TriggerBase Trigger { get; }
    public IEffect Effect { get; }   // 单 Effect 或 CompositeEffect
}
```

## 3. TriggerBase 抽象基类

```csharp
public abstract class TriggerBase
{
    private Action<TriggerContext> _callback;

    public void Bind(Action<TriggerContext> callback) => _callback = callback;

    public virtual void Enable() { }
    public virtual void Disable() { }
    public virtual void Tick(float deltaTime) { }

    protected void Fire(TriggerContext ctx)
    {
        _callback?.Invoke(ctx);
    }
}
```

基类只做一件事：存回调，提供 Fire 方法。冷却、概率等由各子类自行管理。

## 4. Trigger 实现

### OnClickTrigger

订阅 InputEvents 的 OnPointerDown / OnPointerHeld / OnPointerUp。

内部状态：
- `_cooldown` / `_cooldownTimer` — 冷却管理
- `_chargeTime` / `_holdDuration` / `_isCharging` — 蓄力管理

流程：
1. `OnPointerDown` → `_isCharging = true`，记录起始
2. `OnPointerHeld` → 累加 `_holdDuration`
3. `OnPointerUp` → 计算 chargeRatio → 冷却检查 → Fire(ctx)

蓄力比：`chargeTime > 0 ? clamp(holdDuration / chargeTime, 0, 1) : 1.0`

冷却：`_cooldownTimer > 0` 时 OnPointerUp 静默丢弃，不排队。

TriggerContext：`{ worldPos, chargeRatio, target=null, isPassive=false }`

### OnHitTrigger

订阅 `ClickAttackSystem.OnAttackExecuted(AttackResult)`。

触发条件：
1. `result.isPassive == false`（只响应主动攻击命中，防递归）
2. `Random.value < _chance`（概率判定）

TriggerContext：`{ worldPos=result.worldPos, chargeRatio=1.0, target=result.target, isPassive=true }`

### OnTimerTrigger

无事件订阅，靠 Tick 计时。

流程：
1. `Enable()` → `_timer = _interval`（首次延迟一个周期）
2. `Tick(dt)` → `_timer -= dt`；if `_timer <= 0` → Fire(ctx) → `_timer = _interval`

TriggerContext：`{ worldPos=玩家/塔基固定位置（从 SkillSystem 获取）, chargeRatio=1.0, target=null, isPassive=true }`

### OnKillTrigger

订阅 `EnemyEvents.OnEnemyDied(Vector2 deathPos)`。

触发条件：`Random.value < _chance`

TriggerContext：`{ worldPos=deathPos, chargeRatio=1.0, target=null, isPassive=true }`

## 5. IEffect 接口与实现

### IEffect

```csharp
public interface IEffect
{
    void Execute(TriggerContext context);
}
```

### DamageEffect

即时伤害。内部完成所有战斗计算。

DamageEffect 通过构造函数注入 EffectManager 引用（SkillFactory 在 Create 时传入），不使用全局单例。

流程：
1. 从 EffectManager 查询 `damageMultiplier`（Buff 叠加值）
2. 自己 roll 暴击：`Random.value < EffectManager.GetStatValue("critChance")`
3. 计算最终伤害：

```
finalDamage = config.Damage
            * Mathf.Lerp(1.0, CHARGE_MULT, ctx.chargeRatio)
            * damageMultiplier
            * (isCrit ? CRIT_MULT : 1.0)
```

4. 物理检测：
   - `radius > 0` → `Physics2D.OverlapCircleAll(ctx.worldPos, radius)`
   - `radius == 0` → `Physics2D.OverlapPoint(ctx.worldPos)`

5. 对命中目标调用 `IDamageable.TakeDamage(finalDamage, isCrit)`

6. 广播 `OnAttackExecuted(AttackResult)`，其中 `isPassive = ctx.isPassive`

### DotEffect

持续伤害。Execute 时注册到 EffectManager。

`EffectManager.AddDot(worldPos, radius, dps, duration)`

EffectManager 每帧 Tick：对范围内敌人造成 `dps * deltaTime` 伤害，duration 到期自动移除。

### BuffEffect

属性增益。Execute 时注册到 EffectManager。

`EffectManager.AddBuff(statName, statValue, duration)`

叠加规则：同名 Buff 叠加时刷新持续时间，value 相加。`duration = -1` 永久不过期。

### CompositeEffect

```csharp
public class CompositeEffect : IEffect
{
    private readonly IEffect[] _effects;

    public CompositeEffect(IEffect[] effects) => _effects = effects;

    public void Execute(TriggerContext context)
    {
        for (int i = 0; i < _effects.Length; i++)
            _effects[i].Execute(context);
    }
}
```

所有子 Effect 共享同一个不可变 TriggerContext 快照。

## 6. SkillFactory

```csharp
public static class SkillFactory
{
    public static Skill Create(SkillConfig config)
    {
        TriggerBase trigger = CreateTrigger(config);
        IEffect effect = CreateEffect(config);
        trigger.Bind(effect.Execute);
        return new Skill(config, trigger, effect);
    }
}
```

Trigger 工厂：switch-case on `config.TriggerType`（OnClick / OnHit / OnTimer / OnKill）

Effect 工厂：
- 1 个 Effect → `CreateSingleEffect(config.Effects[0])`
- 2~3 个 Effect → `new CompositeEffect(effects[])`

SingleEffect switch-case on `effectConfig.EffectType`（hit_single / hit_aoe / dot_aoe / stat_buff）

## 7. EffectManager

SkillSystem 的内部成员，管理所有持续型效果。

```csharp
public class EffectManager
{
    // Buff 管理
    private List<ActiveBuff> _activeBuffs;
    void AddBuff(string statName, float value, float duration);
    float GetStatValue(string statName);  // 汇总所有同名 Buff 的 value

    // DoT 管理
    private List<ActiveDot> _activeDots;
    void AddDot(Vector2 worldPos, float radius, float dps, float duration);

    // 每帧更新
    void Tick(float deltaTime);
    // 递减所有 Buff/DoT 剩余时间
    // DoT 对范围内敌人造成 dps*dt 伤害
    // 过期自动移除
}
```

## 8. SkillSystem

中枢系统，管理 Skill 列表和 EffectManager。

```csharp
public class SkillSystem
{
    private List<Skill> _skills;
    private EffectManager _effectManager;

    // 对外接口
    void AddSkill(SkillConfig config);    // 创建 Skill → Enable → 加入列表
    void RemoveSkill(string skillId);     // Disable → 移出列表

    // 每帧更新
    void Update(float deltaTime);
    // foreach skill → trigger.Tick(dt)
    // _effectManager.Tick(dt)

    // 查询接口（HUD 用）
    IReadOnlyList<Skill> GetEquippedSkills();
    float GetCooldownProgress(string skillId);  // 0=可用, 1=刚进入冷却

    // 游戏状态响应
    // 订阅 GameLoopManager 事件
    // Paused/LevelingUp → 所有 Trigger.Disable()
    // Playing → 所有 Trigger.Enable()
    // Victory/Defeat → DisableAll + ClearAll
}
```

## 9. AttackResult 事件结构

```csharp
public struct AttackResult
{
    public Vector2 worldPos;
    public IDamageable target;    // 单体时有值，AOE 时为 null
    public bool isPassive;        // OnHitTrigger 检查此标记
    public int hitCount;
}
```

由 DamageEffect 在命中后通过 `ClickAttackSystem.OnAttackExecuted` 广播。

## 10. 事件订阅关系

| Trigger | 订阅 | 事件 |
|---------|------|------|
| OnClickTrigger | InputEvents | OnPointerDown / OnPointerHeld / OnPointerUp |
| OnHitTrigger | ClickAttackSystem | OnAttackExecuted(AttackResult) |
| OnTimerTrigger | 无 | 靠 Tick 计时 |
| OnKillTrigger | EnemyEvents | OnEnemyDied(Vector2 deathPos) |
| SkillSystem | GameLoopManager | OnGamePaused / OnGameResumed / OnLevelUpStart / OnLevelUpComplete / OnVictory / OnDefeat |

## 11. 需要修改的现有代码

| 文件 | 变更 |
|------|------|
| `ISkillTrigger.cs` | 废弃现有 `SkillTrigger` 抽象类，替换为新的 `TriggerBase` |
| `SkillSystem.cs` | 从空壳填充为完整的 Skill 列表管理 + Update 逻辑 |
| `ClickAttackSystem.cs` | 从空壳填充为物理检测 + 事件广播 |
| `Global.cs` | 无需修改（玩家属性由 EffectManager 管理） |

## 12. 新增文件

| 文件 | 内容 |
|------|------|
| `Scripts/Skill/TriggerBase.cs` | 抽象基类 |
| `Scripts/Skill/IEffect.cs` | Effect 接口 |
| `Scripts/Skill/TriggerContext.cs` | TriggerContext struct |
| `Scripts/Skill/IDamageable.cs` | 受击接口 |
| `Scripts/Skill/Triggers/OnClickTrigger.cs` | 点击触发 |
| `Scripts/Skill/Triggers/OnHitTrigger.cs` | 命中触发 |
| `Scripts/Skill/Triggers/OnTimerTrigger.cs` | 计时触发 |
| `Scripts/Skill/Triggers/OnKillTrigger.cs` | 击杀触发 |
| `Scripts/Skill/Effects/DamageEffect.cs` | 即时伤害 |
| `Scripts/Skill/Effects/DotEffect.cs` | 持续伤害 |
| `Scripts/Skill/Effects/BuffEffect.cs` | 属性增益 |
| `Scripts/Skill/Effects/CompositeEffect.cs` | 多效果包装 |
| `Scripts/Skill/SkillFactory.cs` | switch-case 工厂 |
| `Scripts/Skill/EffectManager.cs` | Buff/DoT 生命周期 |
| `Scripts/Skill/Skill.cs` | Skill 运行时类 |
| `Scripts/Skill/AttackResult.cs` | 攻击结果事件结构 |

## 13. 不在本次范围内

- hit_chain（链式）、spawn_projectile（召唤）、apply_status（状态效果）、execute（处决）— 后续迭代
- 升级系统集成（AddSkill/RemoveSkill 调用方）
- 粒子特效系统（OnChargeStarted/Updated 事件消费方）
- HUD 冷却显示（GetCooldownProgress 消费方）
