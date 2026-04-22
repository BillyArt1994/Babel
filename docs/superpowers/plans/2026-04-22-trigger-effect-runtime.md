# Trigger/Effect 运行时管线 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现技能系统的运行时执行管线——从 Trigger 触发到 Effect 执行到伤害落地的完整链路。

**Architecture:** Trigger(何时触发) + Effect(做什么) 双层分离，TriggerContext 固定 struct 传递轻量数据，Effect 内部自治查询 EffectManager 获取战斗倍率。SkillFactory switch-case 组装，EffectManager 作为 SkillSystem 内部成员管理 Buff/DoT 生命周期。

**Tech Stack:** Unity 2022.3 / C# / QFramework (ViewController, UIKit, BindableProperty) / Physics2D

**Spec:** `docs/superpowers/specs/2026-04-22-trigger-effect-runtime-design.md`

---

## File Map

### New Files (under `Babel_Client/Assets/Scripts/Skill/`)

| File | Responsibility |
|------|---------------|
| `TriggerContext.cs` | TriggerContext struct + AttackResult struct |
| `IDamageable.cs` | IDamageable interface |
| `IEffect.cs` | IEffect interface |
| `TriggerBase.cs` | Abstract base class for all triggers |
| `Skill.cs` | Runtime Skill class (Config + Trigger + Effect) |
| `Triggers/OnClickTrigger.cs` | 点击蓄力触发器 |
| `Triggers/OnHitTrigger.cs` | 命中触发器 |
| `Triggers/OnTimerTrigger.cs` | 定时触发器 |
| `Triggers/OnKillTrigger.cs` | 击杀触发器 |
| `Effects/DamageEffect.cs` | 即时伤害效果 |
| `Effects/DotEffect.cs` | 持续伤害效果 |
| `Effects/BuffEffect.cs` | 属性增益效果 |
| `Effects/CompositeEffect.cs` | 多效果包装器 |
| `SkillFactory.cs` | switch-case 工厂 |
| `EffectManager.cs` | Buff/DoT 生命周期管理 |
| `EnemyEvents.cs` | 敌人死亡事件静态类 |

### Modified Files

| File | Change |
|------|--------|
| `Scripts/Skill/ISkillTrigger.cs` | 删除旧的 `SkillTrigger` 抽象类（被 `TriggerBase.cs` 替代） |
| `Scripts/Game/Enemy.cs` | 实现 `IDamageable` 接口，广播死亡事件 |
| `Scripts/Game/SkillSystem.cs` | 填充完整的 Skill 管理 + Update 逻辑 |
| `Scripts/Game/ClickAttackSystem.cs` | 填充 OnAttackExecuted 静态事件 |

---

### Task 1: Core Interfaces & Data Structures

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/TriggerContext.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/IDamageable.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/IEffect.cs`

- [ ] **Step 1: Create TriggerContext and AttackResult structs**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/TriggerContext.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// Trigger 触发时传递给 Effect 的轻量数据包。
    /// Trigger 只填它知道的字段，Effect 自己补齐战斗数据。
    /// </summary>
    public struct TriggerContext
    {
        /// <summary>触发位置（所有 Trigger 都填）。</summary>
        public Vector2 WorldPos;

        /// <summary>蓄力比：OnClick 填 0~1，其他 Trigger 填 1.0。</summary>
        public float ChargeRatio;

        /// <summary>命中目标：OnHit 填，其他 Trigger 填 null。</summary>
        public IDamageable Target;

        /// <summary>是否为被动触发（阻断 OnHitTrigger 递归）。</summary>
        public bool IsPassive;
    }

    /// <summary>
    /// DamageEffect 命中后广播的攻击结果。
    /// </summary>
    public struct AttackResult
    {
        /// <summary>攻击位置。</summary>
        public Vector2 WorldPos;

        /// <summary>命中的主要目标（AOE 时为 null）。</summary>
        public IDamageable Target;

        /// <summary>是否为被动攻击（OnHitTrigger 检查此标记）。</summary>
        public bool IsPassive;

        /// <summary>本次攻击命中数量。</summary>
        public int HitCount;
    }
}
```

- [ ] **Step 2: Create IDamageable interface**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/IDamageable.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 可受击实体的统一接口。Enemy 实现此接口。
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, bool isCrit);
        Vector2 Position { get; }
        bool IsAlive { get; }
    }
}
```

- [ ] **Step 3: Create IEffect interface**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/IEffect.cs
namespace Babel
{
    /// <summary>
    /// 技能效果接口。每种效果自行决定如何执行。
    /// </summary>
    public interface IEffect
    {
        void Execute(TriggerContext context);
    }
}
```

- [ ] **Step 4: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/TriggerContext.cs Babel_Client/Assets/Scripts/Skill/IDamageable.cs Babel_Client/Assets/Scripts/Skill/IEffect.cs
git commit -m "feat: add TriggerContext, IDamageable, IEffect core interfaces"
```

---

### Task 2: TriggerBase + Skill Class

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/TriggerBase.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Skill.cs`
- Delete content of: `Babel_Client/Assets/Scripts/Skill/ISkillTrigger.cs`

- [ ] **Step 1: Create TriggerBase abstract class**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/TriggerBase.cs
using System;

namespace Babel
{
    /// <summary>
    /// 所有 Trigger 的抽象基类。只存回调 + 提供 Fire()。
    /// 冷却、概率等由各子类自行管理。
    /// </summary>
    public abstract class TriggerBase
    {
        private Action<TriggerContext> _callback;

        /// <summary>
        /// 绑定触发时的回调（通常是 Effect.Execute）。
        /// </summary>
        public void Bind(Action<TriggerContext> callback) => _callback = callback;

        /// <summary>启用触发器，开始响应事件。</summary>
        public virtual void Enable() { }

        /// <summary>禁用触发器，停止响应事件并重置内部状态。</summary>
        public virtual void Disable() { }

        /// <summary>每帧更新，供计时类 Trigger 使用。</summary>
        public virtual void Tick(float deltaTime) { }

        /// <summary>
        /// 触发回调，将 TriggerContext 传给绑定的 Effect。
        /// </summary>
        protected void Fire(TriggerContext ctx)
        {
            _callback?.Invoke(ctx);
        }
    }
}
```

- [ ] **Step 2: Create Skill runtime class**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Skill.cs
namespace Babel
{
    /// <summary>
    /// 运行时技能实例 = 配置 + 触发器 + 效果。
    /// 由 SkillFactory 组装，由 SkillSystem 管理生命周期。
    /// </summary>
    public class Skill
    {
        public SkillConfig Config { get; }
        public TriggerBase Trigger { get; }
        public IEffect Effect { get; }

        public Skill(SkillConfig config, TriggerBase trigger, IEffect effect)
        {
            Config = config;
            Trigger = trigger;
            Effect = effect;
        }
    }
}
```

- [ ] **Step 3: Replace old SkillTrigger with redirect comment**

Replace the entire content of `Babel_Client/Assets/Scripts/Skill/ISkillTrigger.cs` with:

```csharp
// File: Babel_Client/Assets/Scripts/Skill/ISkillTrigger.cs
// 旧的 SkillTrigger 抽象类已废弃，由 TriggerBase.cs 替代。
// 保留此文件避免 Unity .meta 引用断裂，后续清理时删除。
```

- [ ] **Step 4: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/TriggerBase.cs Babel_Client/Assets/Scripts/Skill/Skill.cs Babel_Client/Assets/Scripts/Skill/ISkillTrigger.cs
git commit -m "feat: add TriggerBase abstract class and Skill runtime class"
```

---

### Task 3: EnemyEvents + Enemy implements IDamageable

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/EnemyEvents.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/Enemy.cs`

- [ ] **Step 1: Create EnemyEvents static class**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/EnemyEvents.cs
using System;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 敌人相关的全局静态事件。
    /// </summary>
    public static class EnemyEvents
    {
        /// <summary>
        /// 敌人死亡时广播，参数为死亡位置。
        /// </summary>
        public static event Action<Vector2> OnEnemyDied;

        public static void RaiseEnemyDied(Vector2 deathPos)
        {
            OnEnemyDied?.Invoke(deathPos);
        }
    }
}
```

- [ ] **Step 2: Make Enemy implement IDamageable and broadcast death**

Modify `Babel_Client/Assets/Scripts/Game/Enemy.cs`. The full file after changes:

```csharp
// File: Babel_Client/Assets/Scripts/Game/Enemy.cs
using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class Enemy : ViewController, IDamageable
    {
        public float HP = 15;
        public float MovementSpeed = 2.0f;
        public int buildAbility = 25;
        public Babel.Path path;

        // ── IDamageable ──

        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive) return;
            HP -= damage;
        }

        // ── Update ──

        private void Update()
        {
            if (HP <= 0)
            {
                EnemyEvents.RaiseEnemyDied(Position);
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
                return;
            }

            if (path.IsCurrentLayerBuildCompleted() != true)
            {
                var curWayPointIndex = 0;
                var nearestDistance = float.MaxValue;
                for (int i = 0; i < path.wayPointList.Length; i++)
                {
                    var distance = Vector3.Distance(path.wayPointList[i].transform.position, transform.position);
                    if (distance <= nearestDistance && path.wayPointList[i].IsBilding == false && path.wayPointList[i].IsBuildCompleted == false)
                    {
                        nearestDistance = distance;
                        curWayPointIndex = i;
                    }
                }

                var targetPos = new Vector3(path.wayPointList[curWayPointIndex].transform.position.x, path.wayPointList[curWayPointIndex].transform.position.y - 0.5f, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);

                if ((transform.position - targetPos).magnitude <= 0.01)
                {
                    path.wayPointList[curWayPointIndex].AddBuildProgress(buildAbility);
                    path.wayPointList[curWayPointIndex].IsBilding = false;
                    this.DestroyGameObjGracefully();
                }
            }
            else
            {
                var targetPos = new Vector3(path.wayPointList[path.getGatewayIndex()].transform.position.x, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);
                if ((transform.position - targetPos).magnitude <= 0.01)
                {
                    if (path.nextLayerPath == null)
                    {
                        UIKit.OpenPanel<UIGameOverPanel>();
                    }
                    else
                    {
                        path = path.nextLayerPath;
                    }
                }
            }
        }
    }
}
```

Key changes:
- Add `: IDamageable` to class declaration
- Add `Position`, `IsAlive` properties
- Add `TakeDamage(float, bool)` method with alive guard
- Add `EnemyEvents.RaiseEnemyDied(Position)` before destroy in death check
- Add `return` after death handling to skip movement logic

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/EnemyEvents.cs Babel_Client/Assets/Scripts/Game/Enemy.cs
git commit -m "feat: Enemy implements IDamageable, add EnemyEvents death broadcast"
```

---

### Task 4: EffectManager (Buff/DoT lifecycle)

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/EffectManager.cs`

- [ ] **Step 1: Create EffectManager**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/EffectManager.cs
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 管理所有持续型效果（Buff/DoT）的生命周期和属性汇总查询。
    /// 作为 SkillSystem 的内部成员，非全局单例。
    /// </summary>
    public class EffectManager
    {
        // ── Buff ──

        private struct ActiveBuff
        {
            public string StatName;
            public float Value;
            public float Duration;   // -1 = permanent
            public float Remaining;
        }

        private readonly List<ActiveBuff> _activeBuffs = new();

        /// <summary>
        /// 添加或叠加属性增益。同名 Buff 叠加 value 并刷新持续时间。
        /// </summary>
        public void AddBuff(string statName, float value, float duration)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].StatName == statName)
                {
                    var existing = _activeBuffs[i];
                    existing.Value += value;
                    if (duration >= 0 && existing.Duration >= 0)
                    {
                        existing.Remaining = Mathf.Max(existing.Remaining, duration);
                    }
                    _activeBuffs[i] = existing;
                    return;
                }
            }

            _activeBuffs.Add(new ActiveBuff
            {
                StatName = statName,
                Value = value,
                Duration = duration,
                Remaining = duration
            });
        }

        /// <summary>
        /// 查询某属性的当前汇总值（基础 1.0 + 所有同名 Buff 的 value）。
        /// </summary>
        public float GetStatValue(string statName)
        {
            float total = 1.0f;
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].StatName == statName)
                {
                    total += _activeBuffs[i].Value;
                }
            }
            return total;
        }

        // ── DoT ──

        private struct ActiveDot
        {
            public Vector2 WorldPos;
            public float Radius;
            public float Dps;
            public float Remaining;
        }

        private readonly List<ActiveDot> _activeDots = new();

        /// <summary>
        /// 注册一个持续伤害区域。
        /// </summary>
        public void AddDot(Vector2 worldPos, float radius, float dps, float duration)
        {
            _activeDots.Add(new ActiveDot
            {
                WorldPos = worldPos,
                Radius = radius,
                Dps = dps,
                Remaining = duration
            });
        }

        // ── Tick ──

        private static readonly Collider2D[] _dotHitBuffer = new Collider2D[64];

        /// <summary>
        /// 每帧更新：递减 Buff/DoT 剩余时间，DoT 造成伤害，过期自动移除。
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Tick Buffs
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                if (buff.Duration < 0) continue; // permanent
                buff.Remaining -= deltaTime;
                if (buff.Remaining <= 0)
                {
                    _activeBuffs.RemoveAt(i);
                }
                else
                {
                    _activeBuffs[i] = buff;
                }
            }

            // Tick DoTs
            for (int i = _activeDots.Count - 1; i >= 0; i--)
            {
                var dot = _activeDots[i];
                dot.Remaining -= deltaTime;

                // Apply damage to enemies in radius
                float damage = dot.Dps * deltaTime;
                int hitCount = Physics2D.OverlapCircleNonAlloc(dot.WorldPos, dot.Radius, _dotHitBuffer);
                for (int j = 0; j < hitCount; j++)
                {
                    if (_dotHitBuffer[j].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(damage, false);
                    }
                }

                if (dot.Remaining <= 0)
                {
                    _activeDots.RemoveAt(i);
                }
                else
                {
                    _activeDots[i] = dot;
                }
            }
        }

        /// <summary>
        /// 清除所有活跃效果（Victory/Defeat 时调用）。
        /// </summary>
        public void ClearAll()
        {
            _activeBuffs.Clear();
            _activeDots.Clear();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/EffectManager.cs
git commit -m "feat: add EffectManager for Buff/DoT lifecycle management"
```

---

### Task 5: CompositeEffect + BuffEffect + DotEffect

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/Effects/CompositeEffect.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Effects/BuffEffect.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Effects/DotEffect.cs`

- [ ] **Step 1: Create CompositeEffect**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Effects/CompositeEffect.cs
namespace Babel
{
    /// <summary>
    /// 包装 2~3 个 IEffect，触发时依次执行所有子效果。
    /// 所有子效果共享同一个不可变 TriggerContext 快照。
    /// </summary>
    public class CompositeEffect : IEffect
    {
        private readonly IEffect[] _effects;

        public CompositeEffect(IEffect[] effects)
        {
            _effects = effects;
        }

        public void Execute(TriggerContext context)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i].Execute(context);
            }
        }
    }
}
```

- [ ] **Step 2: Create BuffEffect**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Effects/BuffEffect.cs
namespace Babel
{
    /// <summary>
    /// 属性增益效果。Execute 时注册到 EffectManager。
    /// </summary>
    public class BuffEffect : IEffect
    {
        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;

        public BuffEffect(EffectConfig config, EffectManager effectManager)
        {
            _config = config;
            _effectManager = effectManager;
        }

        public void Execute(TriggerContext context)
        {
            _effectManager.AddBuff(_config.StatName, _config.StatValue, _config.Duration);
        }
    }
}
```

- [ ] **Step 3: Create DotEffect**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Effects/DotEffect.cs
namespace Babel
{
    /// <summary>
    /// 持续伤害效果。Execute 时注册到 EffectManager，由其每帧 Tick。
    /// </summary>
    public class DotEffect : IEffect
    {
        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;

        public DotEffect(EffectConfig config, EffectManager effectManager)
        {
            _config = config;
            _effectManager = effectManager;
        }

        public void Execute(TriggerContext context)
        {
            _effectManager.AddDot(context.WorldPos, _config.Radius, _config.Dps, _config.Duration);
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/Effects/
git commit -m "feat: add CompositeEffect, BuffEffect, DotEffect"
```

---

### Task 6: DamageEffect + ClickAttackSystem events

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/Effects/DamageEffect.cs`
- Modify: `Babel_Client/Assets/Scripts/Game/ClickAttackSystem.cs`

- [ ] **Step 1: Add OnAttackExecuted event to ClickAttackSystem**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/ClickAttackSystem.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/ClickAttackSystem.cs
using System;
using UnityEngine;
using QFramework;

namespace Babel
{
    /// <summary>
    /// 攻击事件广播。DamageEffect 命中后通过此系统广播 AttackResult。
    /// </summary>
    public partial class ClickAttackSystem : ViewController
    {
        /// <summary>
        /// 攻击命中后广播，OnHitTrigger 订阅此事件。
        /// </summary>
        public static event Action<AttackResult> OnAttackExecuted;

        public static void RaiseAttackExecuted(AttackResult result)
        {
            OnAttackExecuted?.Invoke(result);
        }
    }
}
```

- [ ] **Step 2: Create DamageEffect**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Effects/DamageEffect.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 即时伤害效果。内部完成所有战斗计算（倍率查询、暴击判定、物理检测）。
    /// </summary>
    public class DamageEffect : IEffect
    {
        private const float CHARGE_MULT = 1.5f;
        private const float CRIT_MULT = 2.0f;

        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;
        private readonly bool _isAoe;

        private static readonly Collider2D[] _hitBuffer = new Collider2D[64];

        public DamageEffect(EffectConfig config, EffectManager effectManager, bool isAoe)
        {
            _config = config;
            _effectManager = effectManager;
            _isAoe = isAoe;
        }

        public void Execute(TriggerContext context)
        {
            // 1. Query buff multipliers
            float damageMult = _effectManager.GetStatValue("damageMult");
            float critChance = _effectManager.GetStatValue("critChance") - 1.0f; // GetStatValue returns 1.0 + sum
            bool isCrit = Random.value < critChance;

            // 2. Calculate final damage
            float baseDamage = _config.Damage;
            if (baseDamage <= 0 && _config.DamageRatio > 0)
            {
                // ratio-based damage (e.g. aftershock): use a base reference
                // For now, use DamageRatio as a flat multiplier on a reference value
                baseDamage = _config.DamageRatio * 100f;
            }

            float finalDamage = baseDamage
                * Mathf.Lerp(1.0f, CHARGE_MULT, context.ChargeRatio)
                * damageMult
                * (isCrit ? CRIT_MULT : 1.0f);

            // 3. Physics detection
            int hitCount = 0;
            IDamageable firstTarget = null;

            if (_isAoe && _config.Radius > 0)
            {
                int count = Physics2D.OverlapCircleNonAlloc(context.WorldPos, _config.Radius, _hitBuffer);
                for (int i = 0; i < count; i++)
                {
                    if (_hitBuffer[i].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(finalDamage, isCrit);
                        hitCount++;
                        if (firstTarget == null) firstTarget = target;
                    }
                }
            }
            else
            {
                // Single target: overlap point
                int count = Physics2D.OverlapPointNonAlloc(context.WorldPos, _hitBuffer);
                for (int i = 0; i < count; i++)
                {
                    if (_hitBuffer[i].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(finalDamage, isCrit);
                        hitCount++;
                        firstTarget = target;
                        break; // single target: only hit first
                    }
                }
            }

            // 4. Broadcast attack result (for OnHitTrigger)
            if (hitCount > 0)
            {
                ClickAttackSystem.RaiseAttackExecuted(new AttackResult
                {
                    WorldPos = context.WorldPos,
                    Target = firstTarget,
                    IsPassive = context.IsPassive,
                    HitCount = hitCount
                });
            }
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/Effects/DamageEffect.cs Babel_Client/Assets/Scripts/Game/ClickAttackSystem.cs
git commit -m "feat: add DamageEffect with physics detection and attack event broadcast"
```

---

### Task 7: Four Trigger Implementations

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/Triggers/OnClickTrigger.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Triggers/OnHitTrigger.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Triggers/OnTimerTrigger.cs`
- Create: `Babel_Client/Assets/Scripts/Skill/Triggers/OnKillTrigger.cs`

- [ ] **Step 1: Create OnClickTrigger**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Triggers/OnClickTrigger.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 点击蓄力触发器。订阅 InputEvents，管理蓄力和冷却。
    /// 注意：InputSystem 已计算 ChargeRatio，但 OnClickTrigger 使用自己的
    /// chargeTime 配置重新计算（因为不同技能可能有不同蓄力时间）。
    /// </summary>
    public class OnClickTrigger : TriggerBase
    {
        private readonly float _cooldown;
        private readonly float _chargeTime;

        private float _cooldownTimer;
        private float _holdDuration;
        private bool _isCharging;
        private bool _enabled;
        private Vector2 _lastWorldPos;

        public OnClickTrigger(float cooldown, float chargeTime)
        {
            _cooldown = cooldown;
            _chargeTime = chargeTime;
        }

        /// <summary>
        /// 冷却进度 [0, 1]。0 = 可用，1 = 刚进入冷却。
        /// </summary>
        public float CooldownProgress
        {
            get
            {
                if (_cooldown <= 0) return 0f;
                return Mathf.Clamp01(_cooldownTimer / _cooldown);
            }
        }

        public override void Enable()
        {
            _enabled = true;
            InputEvents.OnPointerDown += HandlePointerDown;
            InputEvents.OnPointerHold += HandlePointerHold;
            InputEvents.OnPointerUp += HandlePointerUp;
            InputEvents.OnPointerCancel += HandlePointerCancel;
        }

        public override void Disable()
        {
            _enabled = false;
            InputEvents.OnPointerDown -= HandlePointerDown;
            InputEvents.OnPointerHold -= HandlePointerHold;
            InputEvents.OnPointerUp -= HandlePointerUp;
            InputEvents.OnPointerCancel -= HandlePointerCancel;

            if (_isCharging)
            {
                _isCharging = false;
                _holdDuration = 0f;
            }
            _cooldownTimer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= deltaTime;
            }
        }

        private void HandlePointerDown(PointerInputContext ctx)
        {
            if (!_enabled) return;
            _isCharging = true;
            _holdDuration = 0f;
            _lastWorldPos = ctx.WorldPosition;
        }

        private void HandlePointerHold(PointerInputContext ctx)
        {
            if (!_enabled || !_isCharging) return;
            _holdDuration = ctx.HoldDuration;
            _lastWorldPos = ctx.WorldPosition;
        }

        private void HandlePointerUp(PointerInputContext ctx)
        {
            if (!_enabled || !_isCharging) return;

            _isCharging = false;
            _lastWorldPos = ctx.WorldPosition;

            // Cooldown check: silently discard if on cooldown
            if (_cooldownTimer > 0) return;

            // Calculate charge ratio
            float chargeRatio = _chargeTime > 0
                ? Mathf.Clamp01(_holdDuration / _chargeTime)
                : 1.0f;

            Fire(new TriggerContext
            {
                WorldPos = _lastWorldPos,
                ChargeRatio = chargeRatio,
                Target = null,
                IsPassive = false
            });

            _cooldownTimer = _cooldown;
        }

        private void HandlePointerCancel(PointerInputContext ctx)
        {
            _isCharging = false;
            _holdDuration = 0f;
        }
    }
}
```

- [ ] **Step 2: Create OnHitTrigger**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Triggers/OnHitTrigger.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 命中触发器。订阅 OnAttackExecuted，只响应主动攻击命中（防递归）。
    /// </summary>
    public class OnHitTrigger : TriggerBase
    {
        private readonly float _chance;
        private bool _enabled;

        public OnHitTrigger(float chance)
        {
            _chance = chance;
        }

        public override void Enable()
        {
            _enabled = true;
            ClickAttackSystem.OnAttackExecuted += HandleAttackExecuted;
        }

        public override void Disable()
        {
            _enabled = false;
            ClickAttackSystem.OnAttackExecuted -= HandleAttackExecuted;
        }

        private void HandleAttackExecuted(AttackResult result)
        {
            if (!_enabled) return;

            // Only respond to active attacks (prevent recursion)
            if (result.IsPassive) return;

            // Chance roll
            if (Random.value > _chance) return;

            Fire(new TriggerContext
            {
                WorldPos = result.WorldPos,
                ChargeRatio = 1.0f,
                Target = result.Target,
                IsPassive = true
            });
        }
    }
}
```

- [ ] **Step 3: Create OnTimerTrigger**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Triggers/OnTimerTrigger.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 定时触发器。靠 Tick 计时，每隔 interval 秒触发一次。
    /// </summary>
    public class OnTimerTrigger : TriggerBase
    {
        private readonly float _interval;
        private float _timer;
        private bool _enabled;

        /// <summary>
        /// 获取塔基/玩家固定位置的委托。由 SkillFactory 注入。
        /// </summary>
        public System.Func<Vector2> GetBasePosition;

        public OnTimerTrigger(float interval)
        {
            _interval = interval;
        }

        public override void Enable()
        {
            _enabled = true;
            _timer = _interval; // First trigger after one full interval
        }

        public override void Disable()
        {
            _enabled = false;
            _timer = 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (!_enabled) return;

            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _timer = _interval;

                Vector2 basePos = GetBasePosition != null
                    ? GetBasePosition()
                    : Vector2.zero;

                Fire(new TriggerContext
                {
                    WorldPos = basePos,
                    ChargeRatio = 1.0f,
                    Target = null,
                    IsPassive = true
                });
            }
        }
    }
}
```

- [ ] **Step 4: Create OnKillTrigger**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/Triggers/OnKillTrigger.cs
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 击杀触发器。订阅 EnemyEvents.OnEnemyDied，按概率触发。
    /// </summary>
    public class OnKillTrigger : TriggerBase
    {
        private readonly float _chance;
        private bool _enabled;

        public OnKillTrigger(float chance)
        {
            _chance = chance;
        }

        public override void Enable()
        {
            _enabled = true;
            EnemyEvents.OnEnemyDied += HandleEnemyDied;
        }

        public override void Disable()
        {
            _enabled = false;
            EnemyEvents.OnEnemyDied -= HandleEnemyDied;
        }

        private void HandleEnemyDied(Vector2 deathPos)
        {
            if (!_enabled) return;
            if (Random.value > _chance) return;

            Fire(new TriggerContext
            {
                WorldPos = deathPos,
                ChargeRatio = 1.0f,
                Target = null,
                IsPassive = true
            });
        }
    }
}
```

- [ ] **Step 5: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/Triggers/
git commit -m "feat: add 4 trigger implementations (OnClick/OnHit/OnTimer/OnKill)"
```

---

### Task 8: SkillFactory

**Files:**
- Create: `Babel_Client/Assets/Scripts/Skill/SkillFactory.cs`

- [ ] **Step 1: Create SkillFactory with switch-case assembly**

```csharp
// File: Babel_Client/Assets/Scripts/Skill/SkillFactory.cs
using System;
using System.Linq;

namespace Babel
{
    /// <summary>
    /// 从 SkillConfig 组装运行时 Skill 实例。switch-case 工厂。
    /// </summary>
    public static class SkillFactory
    {
        /// <summary>
        /// 组装一个 Skill：创建 Trigger + Effect → Bind → 返回。
        /// </summary>
        /// <param name="config">技能配置。</param>
        /// <param name="effectManager">EffectManager 引用（注入给 Effect）。</param>
        /// <param name="getBasePosition">获取塔基位置的委托（注入给 OnTimerTrigger）。</param>
        public static Skill Create(SkillConfig config, EffectManager effectManager, Func<UnityEngine.Vector2> getBasePosition)
        {
            var trigger = CreateTrigger(config, getBasePosition);
            var effect = CreateEffect(config, effectManager);
            trigger.Bind(effect.Execute);
            return new Skill(config, trigger, effect);
        }

        private static TriggerBase CreateTrigger(SkillConfig config, Func<UnityEngine.Vector2> getBasePosition)
        {
            return config.TriggerType switch
            {
                "OnClick" => new OnClickTrigger(config.Cooldown, config.ChargeTime),
                "OnHit" => new OnHitTrigger(config.Chance),
                "OnTimer" => CreateTimerTrigger(config, getBasePosition),
                "OnKill" => new OnKillTrigger(config.Chance),
                _ => throw new ArgumentException($"[BABEL][SkillFactory] Unknown trigger type: '{config.TriggerType}' in skill '{config.SkillId}'")
            };
        }

        private static OnTimerTrigger CreateTimerTrigger(SkillConfig config, Func<UnityEngine.Vector2> getBasePosition)
        {
            var trigger = new OnTimerTrigger(config.Interval);
            trigger.GetBasePosition = getBasePosition;
            return trigger;
        }

        private static IEffect CreateEffect(SkillConfig config, EffectManager effectManager)
        {
            if (config.Effects.Count == 0)
            {
                throw new ArgumentException($"[BABEL][SkillFactory] Skill '{config.SkillId}' has no effects");
            }

            if (config.Effects.Count == 1)
            {
                return CreateSingleEffect(config.Effects[0], effectManager);
            }

            var effects = config.Effects.Select(ec => CreateSingleEffect(ec, effectManager)).ToArray();
            return new CompositeEffect(effects);
        }

        private static IEffect CreateSingleEffect(EffectConfig ec, EffectManager effectManager)
        {
            return ec.EffectType switch
            {
                "hit_single" => new DamageEffect(ec, effectManager, isAoe: false),
                "hit_aoe" => new DamageEffect(ec, effectManager, isAoe: true),
                "dot_aoe" => new DotEffect(ec, effectManager),
                "stat_buff" => new BuffEffect(ec, effectManager),
                _ => throw new ArgumentException($"[BABEL][SkillFactory] Unknown effect type: '{ec.EffectType}'")
            };
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Skill/SkillFactory.cs
git commit -m "feat: add SkillFactory switch-case assembly"
```

---

### Task 9: SkillSystem Runtime Logic

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/SkillSystem.cs`

- [ ] **Step 1: Implement SkillSystem with full lifecycle management**

Replace the entire content of `Babel_Client/Assets/Scripts/Game/SkillSystem.cs`:

```csharp
// File: Babel_Client/Assets/Scripts/Game/SkillSystem.cs
using System.Collections.Generic;
using UnityEngine;
using QFramework;

namespace Babel
{
    /// <summary>
    /// 技能系统中枢。管理 Skill 列表、Trigger 生命周期、EffectManager。
    /// </summary>
    public partial class SkillSystem : ViewController
    {
        private readonly List<Skill> _skills = new();
        private readonly EffectManager _effectManager = new();
        private bool _enabled;

        private void Start()
        {
            _enabled = true;
        }

        private void Update()
        {
            if (!_enabled) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Tick(dt);
            }
            _effectManager.Tick(dt);
        }

        // ── Public API ──

        /// <summary>
        /// 添加技能。创建 Skill 实例并 Enable Trigger。
        /// </summary>
        public void AddSkill(SkillConfig config)
        {
            var skill = SkillFactory.Create(config, _effectManager, GetBasePosition);
            _skills.Add(skill);
            if (_enabled)
            {
                skill.Trigger.Enable();
            }
        }

        /// <summary>
        /// 移除技能。Disable Trigger 并从列表移出。
        /// </summary>
        public void RemoveSkill(string skillId)
        {
            for (int i = _skills.Count - 1; i >= 0; i--)
            {
                if (_skills[i].Config.SkillId == skillId)
                {
                    _skills[i].Trigger.Disable();
                    _skills.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前装备的技能列表。
        /// </summary>
        public IReadOnlyList<Skill> GetEquippedSkills() => _skills;

        /// <summary>
        /// 获取指定技能的冷却进度 [0, 1]。0 = 可用，1 = 刚进入冷却。
        /// 仅 OnClickTrigger 有冷却，其他 Trigger 返回 0。
        /// </summary>
        public float GetCooldownProgress(string skillId)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.SkillId == skillId &&
                    _skills[i].Trigger is OnClickTrigger clickTrigger)
                {
                    return clickTrigger.CooldownProgress;
                }
            }
            return 0f;
        }

        // ── State Control ──

        /// <summary>
        /// 启用所有 Trigger（Playing 状态恢复时调用）。
        /// </summary>
        public void EnableAll()
        {
            _enabled = true;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Enable();
            }
        }

        /// <summary>
        /// 禁用所有 Trigger（Paused/LevelingUp 时调用）。
        /// </summary>
        public void DisableAll()
        {
            _enabled = false;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Disable();
            }
        }

        /// <summary>
        /// 清空所有技能和效果（Victory/Defeat 时调用）。
        /// </summary>
        public void ClearAll()
        {
            DisableAll();
            _skills.Clear();
            _effectManager.ClearAll();
        }

        // ── Helpers ──

        /// <summary>
        /// 获取塔基/玩家固定位置。OnTimerTrigger 用。
        /// </summary>
        private Vector2 GetBasePosition()
        {
            return (Vector2)transform.position;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/SkillSystem.cs
git commit -m "feat: implement SkillSystem runtime lifecycle management"
```

---

### Task 10: Integration Smoke Test (Manual)

**Files:** None (manual verification in Unity Editor)

This task verifies the full pipeline works end-to-end in the Unity Editor.

- [ ] **Step 1: Add a test initialization to SkillSystem.Start()**

Temporarily add divine_finger skill loading to verify the pipeline. Modify `SkillSystem.cs` Start method:

```csharp
private void Start()
{
    _enabled = true;

    // ── Smoke test: load divine_finger ──
    var config = SkillDatabase.GetById("divine_finger");
    if (config != null)
    {
        AddSkill(config);
        Debug.Log($"[BABEL][SkillSystem] Smoke test: added skill '{config.SkillName}'");
    }
    else
    {
        Debug.LogWarning("[BABEL][SkillSystem] Smoke test: divine_finger not found in SkillDatabase. Is CSV loaded?");
    }
}
```

- [ ] **Step 2: Verify in Unity Editor**

1. Open the Unity project
2. Ensure the skills.csv is loaded into SkillDatabase at startup
3. Enter Play mode
4. Check console for `[BABEL][SkillSystem] Smoke test: added skill '神罚之指'`
5. Click on an enemy → verify DamageEffect fires (console log or enemy HP decrease)
6. If enemies have Collider2D, verify Physics2D detection works

- [ ] **Step 3: Remove smoke test code**

Revert the Start method to clean state:

```csharp
private void Start()
{
    _enabled = true;
}
```

- [ ] **Step 4: Final commit**

```bash
cd H:/Babel
git add Babel_Client/Assets/Scripts/Game/SkillSystem.cs
git commit -m "feat: complete Trigger/Effect runtime pipeline implementation"
```
