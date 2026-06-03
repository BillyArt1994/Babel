# Death Must Die — 技能/升级系统 Code Map

> **用途**：DMD(`H:\Death_Must_Die`,Unity 反编译工程)技能升级系统的导航地图与核心机制速查。
> 目的是 Babel 参考实现时**直接跳到关键处**,不必重新全盘分析。
>
> **DMD 术语对照**:技能 = **Boon(神祇赐福)**;效果 = **Ability(能力)**;升级 = 三选一奖励(Reward)。
> 一个 Boon 含 1~N 个 Ability;Boon 有两个独立维度:**Rarity(稀有度)** 和 **Level(等级)**。
>
> 源码根:`H:\Death_Must_Die\DeathMustDie\Assets\Scripts\Death\`(下文简称 `<S>\`)
> 数据根:`H:\Death_Must_Die\DeathMustDie_Config\`

---

## 0. 快速导航

| # | 模块 | 一句话 | 核心入口类 | 跳转 |
|---|------|--------|-----------|------|
| 1 | **数据模型** | Boon 静态定义 + 运行时实例,Rarity/Level 双维度 | `BoonData` / `BoonInstance` | [§1](#1-数据模型boon-的静态与运行时) |
| 2 | **等级化数值** | 一个 Boon 内嵌"每等级数值表",升级只换底层 Stats | `LevelBasedAbilities` | [§2](#2-等级化数值levelbasedabilities) |
| 3 | **升级选择流程** | XP 溢出 → 生成三选一 → 应用,含权重/稀有度 roll | `RewardGenerator` / `System_Rewards` | [§3](#3-升级三选一选择流程) |
| 4 | **效果分发** | 121 个 `Ability_*` 组合式效果 + 触发器 + 条件 | `IAbility` / `AbilityTrigger` | [§4](#4-效果分发ability-架构) |
| 5 | **CSV 加载** | 极复杂多级 CSV → BoonData,`~` 继承 + effect_type 工厂 | `Parser` / `BoonTable` / `CsvLine` | [§5](#5-csv-数据加载) |
| 6 | **数值系统** | StatModifier 树形 9-pass 计算,作用域路由 | `StatHierarchy` / `StatModifier` | [§4.4](#44-数值流动stats--statmodifier--stathierarchy) |

**Babel 现状对照**(详见各节"映射 Babel"):Babel 用扁平 `skills.csv` + `UpgradesFrom` 字符串依赖 + OnClick 替换;DMD 用 Rarity×Level 双维度 + 神祇池 + 三种升级分支(Gain/LevelUp/RarityUp)。

---

## 1. 数据模型:Boon 的静态与运行时

### 核心逻辑
- **`BoonData`** = 一个稀有度版本的 Boon 静态定义(只读)。同一 archetype 的不同稀有度是**不同的 `BoonData` 对象**。
- **`BoonInstance`** = 玩家实际拥有的运行时 Boon,持有 `UpgradeLevelIndex`(当前等级)和一组展开的 `IAbility`。
- **Rarity 与 Level 完全正交**:Rarity 是 `BoonData` 的静态字段(换稀有度=换 BoonData 对象);Level 是 `BoonInstance` 的运行时状态(升级=换底层数值,不重建实例)。

### 关键类与持有关系
```
BoonData (静态, per-rarity)
 ├── Rarity : BoonRarity              // Novice<Adept<Expert<Master(可升) | Legend/Demigod(独立)
 ├── Slot : SkillSlot                 // Attack/Defense/Strike/Cast/Power/Summon/None
 ├── GroupId : StatGroupId            // 数值作用域路径,如 Skill.Dash
 ├── PrimaryGod / SecondaryGod        // 神祇归属
 ├── Requirements : TagExpression     // 解锁前提(tag 表达式)
 └── AbilityLevels : LevelBasedAbilities  // ★ 等级化数值表(见 §2)
        ↓ CreateInstance(owner, level)
BoonInstance (运行时, 玩家持有)
 ├── Base : BoonData
 ├── UpgradeLevelIndex : int          // 当前等级(0-based)
 ├── Abilities : IAbility[]           // 按当前等级展开的效果实例
 ├── LevelCanBeUpgraded / RarityCanBeUpgraded / IsMaxLevel
 ├── UpgradeTo(level)                 // 升级:替换每个 ability.Stats.Base,调 OnLevelsGained
 └── OnUpgradedRarityFrom(prev)       // 升稀有度:从旧实例迁移运行时状态(计数器等)
```

### 关键文件
- [`<S>\Death.Run.Core.Boons\BoonData.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Core.Boons/BoonData.cs) — 静态定义 + `CreateInstance()`
- [`<S>\Death.Run.Core.Boons\BoonInstance.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Core.Boons/BoonInstance.cs) — 运行时实例 + `UpgradeTo()` / `OnUpgradedRarityFrom()`
- `<S>\Death.Run.Core.Boons\BoonReference.cs` — (code, rarity) 轻量引用(可序列化)
- `<S>\Death.Run.Core\BoonRarity.cs` — 稀有度枚举
- `<S>\Death.Run.Core\RarityRules.cs` — `CanBeUpgraded(rarity)` = `rarity < Master`

### 映射 Babel
> Babel 当前**没有 Rarity 维度**,也没有"同技能多等级"——靠 `meteor`→`meteor_evolved` 独立行 + `UpgradesFrom` 表达进化。
> 若要借鉴 DMD:可引入 `level` 列真正生效(一个 SkillConfig 内嵌多级数值),把"进化"从"换行"改成"换稀有度/升级"。但 Babel 规模小,**Rarity×Level 双维度可能过重**——建议只借 Level(同技能多级),Rarity 暂不引入。

---

## 2. 等级化数值:LevelBasedAbilities

### 核心逻辑
这是"一个 Boon 如何同时编码多个效果 × 多个等级数值"的关键。把**效果模板**(不变)和**每等级数值快照**(变)解耦:
- `Abilities[]`:N 个 `IAbilityData` 效果模板,定义"做什么",不含数值。
- `Levels[]`:M 个等级,每个 `Level.StatsPerAbility[]` 与 `Abilities[]` **一一对位**,存"这一级的数值"。

### 数据流
```
BoonData.CreateInstance(owner, levelIndex)
  → LevelBasedAbilities.CreateAbilityInstances(parentModifier, owner, levelIndex)
      for i in Abilities:
        Abilities[i].CreateInstance(owner, Levels[levelIndex].StatsPerAbility[i], parentModifier, skill)
          → 每个 IAbility 构造 RuntimeStats{ Base: 该级Stats, Modifier: parentModifier 链 }

升级时(BoonInstance.UpgradeTo):
  不重建实例,仅:  ability.Stats.Base = Levels[newLevel].StatsPerAbility[i]
                  ability.OnLevelsGained()   // 刷新注入 StatModifier 的值
  → 触发器/监听器/状态保持不变,数值平滑切换
```

### 关键文件
- [`<S>\Death.Run.Core.Abilities\LevelBasedAbilities.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Core.Abilities/LevelBasedAbilities.cs) — `Levels[]` / `Abilities[]` / `CreateAbilityInstances()`
- `<S>\Death.Run.Core.Boons\BoonLevel.cs` — `Level` + `Ability` 取数值的 struct 视图

### 映射 Babel
> Babel 的 `SkillConfig` 是扁平字段(damage/radius/...)。要支持升级,可把单值字段改成"按等级数组",解析时类似 `Levels[lv].StatsPerAbility`。
> **核心借鉴点**:升级时**不重建技能对象**,只换底层数值——避免 Babel 现在 `AddOrReplaceSkill` 那种"整个替换"导致触发器/冷却状态丢失。

---

## 3. 升级三选一:选择流程

### 核心逻辑(完整数据流)
```
XP 溢出 → Behaviour_XpTracker.OnLevelsGainedEv
  → System_Rewards.GiveFreeLevels(count) → GiveLevelUpRewardsAsync → GiveBoonRewardAsync
      → RewardGenerator.DrawRewards(tags, BoonsPerLevelUp=3)   ← ★ 生成3个候选
      → _rewardUi.ChooseReward(rewards)                        ← 玩家选
      → rewardChoice.Reward.Accept(System_Rewards as IVisitor) ← 应用
          → BoonManager.Gain / UpgradeLevels / UpgradeRarity
          → Event_BoonGained / Event_BoonLevelUp / Event_BoonRarityUp
```

### 三选一生成细节(`RewardGenerator.DrawRewards`)
1. **选神祇**:每次升级**只锁定一个神祇**,3 个选项都来自它。`WeighedRandomSet<God>` 加权(`Teams.GodWeights`);`GodsPerRun=4` 上限,未满从全部神祇选,满了只在已选中选。
2. **Roll 稀有度**:Master 1% → Expert 3% → Adept 7% → 否则 Novice(概率读 `GlobalStatId.Probability_*`,可被道具改)。
3. **构建 4 个牌堆**(限定该神祇):
   - `_boonDeck`:新 boon(`Rarity==rolled`、槽位有空、未拥有、未封禁、`Requirements.Matches`)
   - `_upgradeDeck`:可升级的已拥有 boon(`LevelCanBeUpgraded`)
   - `_rarityUpgradeDeck`:可升稀有度(目标版本恰高 1 级)
   - `_legendaryDeck`:Legend 稀有度
4. **填 3 个槽**:`WeighedRandomSet<RewardDrawer>` 抽:新 boon(权重0.44)/ 升级(0.56)/ 升稀有度(0.05)。Attack 槽空必插(100%),Defense 槽空 40% 插。
5. **类型在生成时确定**(`GenerateReward`):已拥有 → 比稀有度 → RarityUpgrade;否则等级未满 → LevelUpgrade(一次跳 3 级 `UpgradesOnLevelUpReward=3`);否则 → NewBoon。

### 三种应用分支
| 类型 | System_Rewards.Visit | BoonManager | 效果 |
|------|---------------------|-------------|------|
| NewBoon | `Gain(boon,0)` | `GainImpl` | 注册全部 ability,占 SkillSlot |
| LevelUpgrade | `UpgradeLevels(boon,newLv)` | `BoonInstance.UpgradeTo` | 就地换数值,不重建 |
| RarityUpgrade | `UpgradeRarity(boon)` | `Remove`旧 + `GainImpl`新 + `OnUpgradedRarityFrom` | 换 BoonData,迁移状态 |

### Banish / Reroll / Skip(`FudgeStat` 资源)
- **Banish**:boon code 进 `_banishedBoons`,永久移出池
- **Reroll**:当前神祇放回队列头重 roll
- **Skip**:当前神祇进 `_excludedGods`,换神祇

### 关键文件
- [`<S>\Death.Run.Systems.Rewards\RewardGenerator.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Systems.Rewards/RewardGenerator.cs) — ★ 三选一生成核心
- [`<S>\Death.Run.Systems.Rewards\System_Rewards.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Systems.Rewards/System_Rewards.cs) — 流程编排 + IVisitor 应用
- `<S>\Death.Run.Systems.Rewards\LevelUpRewards.cs` — Reward 类型(Visitor 模式)
- [`<S>\Death.Run.Systems\BoonManager.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Systems/BoonManager.cs) — ★ Gain/UpgradeLevels/UpgradeRarity/槽位
- `<S>\Death.Run.Behaviours.Events\Event_BoonGained.cs` / `Event_BoonLevelUp.cs` / `Event_BoonRarityUp.cs` / `Event_BoonSlotsChanged.cs`

### SkillSlot 机制
枚举:`Attack(0) Defense(1=Dash) Strike(2) Cast(3) Power(4) Summon(5) None(6)`。
`BoonManager._slots` = `BoonInstance[7][]`,初始大小来自 `CharacterData.CountPerBoonSlot`。Attack/Defense 各 1 位,其余可多位。槽满则该 boon 不进池。`Ability_BoonSlotCounts` 可动态扩容。临时 boon 走 `None`,不占槽,`BoonTimer` 到期自动 `Remove`。

### 映射 Babel
> Babel 现在:Exp≥5 → `UpgradeSystem.GenerateOptions(3)` 加权随机 → 选中 `AddOrReplaceSkill`。**没有神祇/稀有度/槽位概念**。
> **强借鉴点**:
> - DMD 把"新技能 / 升级现有 / 升稀有度"**混在同一组三选一里**,用 Visitor 分发——Babel 可借此让升级选项不只是"加新技能",也能是"强化已有技能"。
> - **神祇锁定**(每次升级聚焦一个神祇)是 DMD 构筑深度的来源,Babel 若想要 build 多样性可参考(如"流派"分组)。
> - 槽位上限 = 天然的"技能数量约束 + 流派引导",比 Babel 现在无上限更可控。
> **不必照搬**:4 牌堆 + 多重权重 + Legend 特殊路径对 Babel 太重,先做"3 类型混合 + 简单权重"即可。

---

## 4. 效果分发:Ability 架构

### 核心逻辑
121 个 `Ability_*` 类,全部实现 `IAbility`,共用 `Init/Gain/Lose/Update/OnLevelsGained` 生命周期框架,差异在子类。一个 Boon 可混搭多个 ability(如一个被动 `Ability_StatChange` + 一个主动 `Ability_TriggerEffect`),共享同一等级数值表。

### 4.1 IAbility 生命周期钩子
| 钩子 | 时机 |
|------|------|
| `Init` / `PostInit` | 实例化时构造 RuntimeStats |
| `Gain()` / `Lose()` | 获得/移除 boon:注册/清理监听器与修改器 |
| `Update()` / `FixedUpdate()` | 每帧轮询(条件型用) |
| `OnLevelsGained()` | 升级时刷新数值 |
| `OnUpgradedRarityFrom(prev)` | 升稀有度时迁移状态 |
| `TriggerOnLevelUp()` | 升级瞬间主动触发一次 |

### 4.2 121 个 Ability 的 11 大类
1. **纯属性** `Ability_StatChange` / `GlobalStatModifier` — 向 StatModifier 注入加成,Gain/Lose 对称
2. **条件属性** `Ability_ConditionalStatChange` / `DashAttackBuff` — 每帧 Check 条件,满足才 Apply
3. **伤害加成监听** `Ability_BonusDamageVs` / `BonusDamageBelowHp` / `BonusDamagePerStatus` — 实现 `IDamageModifier` 介入伤害计算
4. **触发型效果** `Ability_TriggerEffect`(★核心) / `TriggerRepeat` — 组合 Trigger + Effect,覆盖"命中时/冲刺时/第N次攻击时"
5. **状态施加** `Ability_StatusChanceOnHit` / `WeaponEnchant` / `HitRefreshStatus`
6. **召唤/创建** `Ability_Summon` / `CreateEntity` / `DashLightning`
7. **生命交互** `Ability_HealPerKill` / `Lifesteal` / `ChanceToRevive` / `DivineShield`
8. **Boon 元操作** `Ability_GainBoon` / `LoseBoon` / `UpgradeBoonPerLevels` / `StatChangePerBoonLevel`
9. **数值互转** `Ability_StatConvert` / `StatTransfer` / `ScalingFlag`
10. **规则改写** `Ability_NoPrimaryAttack` / `NoArmor` / `SetDashVariant` / `BoonSlotCounts` / `GodWeight`
11. **具名复合** `Ability_Shadowdance` / `MultiStrike` / `PathOfFire` / `Necromancy`

### 4.3 触发器(Trigger)与条件(Condition)
- **`AbilityTrigger`**(31 个 `Trigger_*`):事件驱动。`Gain` 时 `Event.AddListener`,回调里 `Trigger(args, chance)` → 查冷却 → `StatRules.LoopProbabilityStat`(支持概率>1多次触发)→ 调 callback。
  覆盖:攻击(ForEachHit/NthHit/CriticalHit)、移动(DashStart/NthDash/DistanceMoved)、受击(GetHit/LostLife)、击杀(NthEnemyDied/NthExecution)、计时(Timer)、进度(GainedLevel/NthXpGained)等。
- **`IAbilityCondition`**(3 个):同步轮询(非事件)。`Ability_ConditionalStatChange.Update()` 每帧 `Check()`,满足 Apply/否则 Remove。`AbilityCondition_IsDashing` / `OwnerBelowHp` / `OwnerAboveHp`。
- **`AbilityTrigger.Template`**:设计期描述符,运行时 `CreateInstance(callback, ability)` 生成实例。

### 4.4 数值流动:Stats → StatModifier → StatHierarchy
```
Levels[n].StatsPerAbility[i] (Stats, base值)
  → RuntimeStats{ Base, Modifier: StatModifier(parent) }
  → Stats.Get(statId) = Modifier.Apply(stat, base, scaling)  // 9-pass 链式
```
**9 个 StatPass**(优先级):`BaseBonus → Flat → BoonMods → ItemMods → FinalBonus → TalentMods → SwingMultiplier → AdditionalItemValue → Darkness`。

**`StatHierarchy`** = 挂在 Entity 上的树形 modifier 路由;**`StatGroupId`** = 路径(如 `Skill.Dash`),决定 boon 数值挂哪个节点 → 实现"仅影响冲刺"这种精确作用域。
```
Entity.StatHierarchy
 └ Root ├ Weapon ├ Skill{Dash,Strike,Cast} └ Status{Burn}
```

### 关键文件
- [`<S>\Death.Run.Core.Abilities\IAbility.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Run.Core.Abilities/IAbility.cs) — 接口 + 生命周期
- `<S>\Death.Run.Behaviours.Abilities\` — 121 个 `Ability_*.cs` + 31 个 `Trigger_*.cs` + `AbilityTrigger.cs` + `AbilityCondition_*.cs`
- `<S>\Death.Run.Behaviours.Abilities\Ability_TriggerEffect.cs` — ★ 最通用的触发型效果框架
- StatModifier / StatHierarchy / RuntimeStats(在 `Death.Run.Core.Abilities` 或 `Claw.Core` 下,用时 grep `class StatModifier`)

### 映射 Babel
> Babel 的效果是 `IEffect`(Damage/Buff/DoT/Composite)+ `TriggerBase`(OnClick/OnHit/OnTimer/OnKill)——**架构思想和 DMD 一致**(组合式 trigger+effect),只是 DMD 粒度细得多(121 vs 4)。
> **借鉴点**:
> - DMD 的 `OnLevelsGained()` 钩子(升级只刷数值不重建)值得 Babel 学,解决替换丢状态问题。
> - DMD `StatGroupId` 作用域路由对 Babel **暂时过度设计**,Babel 技能少,直接算即可。
> - 若 Babel 要扩充效果类型,DMD 的 11 大类是很好的"效果清单"参考(尤其元操作类 GainBoon/UpgradeBoon、条件加伤类)。

---

## 5. CSV 数据加载

### 核心逻辑
DMD CSV(`Boons.txt`,分号分隔,2 行表头)把"效果定义 + 多稀有度 + 每等级 30 列数值"全塞进行。一个 archetype 占 **4 行**(每行一个稀有度),靠 `~` 继承未变列。

### 5.1 列结构
- 前 ~12 列:`code / requirements / tags / archtype / type(稀有度) / slot / god2 / god1 / statgroup / levels / statuses / keywords`
- 中段:**11 个 Effect 槽**,每槽 = `effect_type` + 若干 `eNpM/eNpMv1`(key/val 对,读到 `0` 停)+ stat 行(`N_loc/N_st/N_val/N_sca`,`N_val` 可为 `LVL_X` 标记)
- 尾段:**LVL_0~LVL_7 块**,每块 31 列 = 1 name + 30 个等级值

### 5.2 稀有度 = 多行 + `~`
```
DashCon ; ... ; 1_novi ; ...   (Novice,写全)
~       ; ... ; 2_adep ; ...   (Adept,只改稀有度列+数值列,其余 ~)
~       ; ... ; 3_expe ; ...
~       ; ... ; 4_mast ; ...
```
**`~` = "继承本列最近一个非~值"**(按列索引,非行号)。在 `CsvLine` 构造时**立即原地替换**,Parser 看不到 `~`。`CsvIterationContext._nonRepeatValues` 按列存"上一个非~值"。每行产出一个独立 `BoonData`,key = `(code, rarity)`。

### 5.3 effect_type → Ability 工厂
`Parser.cs` 的 `AbilityTypeParsers : Dictionary<string, AbilityParser>`(~第505行),纯字典分发(无 switch):
```
"stat_change" → ParseStatChange
"statm_perc"  → ParseStatChange(BoonMod)
"statm_base"  → ParseStatChange(Flat)
"dmg_abovehp" → ParseBonusDamageAboveHp
"apply_status"→ ParseApplyStatus
"dash_with_fab"→ ParseDashWithFab
... 共 121 条
```
`ParseAbility(line)`:读 effect_type → 查字典 → `ParseParamMapAndOffset` 读 key=val → 工厂函数构造 `Ability_X.Data`。

### 5.4 解析流程
```
Boons.txt
 → CsvReader(sep=';', header=2, skipDevColumns)  // 行1定位#列, 行2跳过, 行3+ 构造CsvLine(~替换+剥#列)
 → Parser.ParseBoonsAdditive → ParseLine:
     读前12字段
     ParseLevelBasedAbility:
       循环11次 ParseAbilityStatsPair (ParseAbility + 读stat行, LVL_X 记为 LevelBasedStat)
       ParseLevelBasedStats: 对每个 LevelBasedStat, baseOffset=1+LVL_slot*31, 填 30 级数值
       GetResult → LevelBasedAbilities(abilities[], levels[])
     → new BoonData(...) → BoonTable.Add (key=(code,rarity))
```

### 关键文件
- [`<S>\Death.Data.Parsing\Parser.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Data.Parsing/Parser.cs) — ★ `AbilityTypeParsers` 映射表 + `ParseAbility` / `ParseLevelBasedAbility`
- [`<S>\Death.Data.Tables\BoonTable.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Data.Tables/BoonTable.cs) — `Get(code, rarity)` 查表
- [`<S>\Death.Utils.Csv\CsvLine.cs`](../../../../Death_Must_Die/DeathMustDie/Assets/Scripts/Death/Death.Utils.Csv/CsvLine.cs) — ★ `~` 继承实现
- `<S>\Death.Utils.Csv\CsvReader.cs` / `CsvIterationContext.cs`
- 数据样本:`H:\Death_Must_Die\DeathMustDie_Config\Boons.csv`(`head -3` 看表头)、`Talents.csv`、`Statuses.csv`

### 映射 Babel
> DMD 的 CSV **极度复杂**(2行表头 + `~` 继承 + 11 effect 槽 + 8×30 等级表),对 Babel **完全不必照搬**。
> **可借鉴的小点**:
> - `effect_type → 工厂字典`(无 switch)的分发方式,Babel 的 `SkillFactory` 若效果类型变多可采用。
> - "一个 code 多行 = 多稀有度/等级"的思路,Babel 若引入 level 可用更简单的"一行多列数组"代替 `~` 继承。
> **保持 Babel 现状**:CLAUDE.md 规定 CSV 驱动且简单扁平,DMD 这套是 AAA 级数据量才需要的复杂度。

---

## 附:Babel 参考实现优先级建议

按"投入产出比"排序,供后续重构技能系统时取舍:

| 优先级 | 借鉴点 | 来源 | 理由 |
|--------|--------|------|------|
| ★★★ | **升级选项混合"新技能/升级现有"** | §3 三分支 | Babel 现在升级只能加新技能,缺"强化已有"深度 |
| ★★★ | **升级用 `OnLevelsGained` 刷数值,不重建** | §2 / §4.1 | 解决 `AddOrReplaceSkill` 丢冷却/触发器状态 |
| ★★ | **同技能多等级(Level 维度)** | §2 LevelBasedAbilities | 替代 `meteor→meteor_evolved` 独立行 |
| ★★ | **效果类型清单扩充** | §4.2 11大类 | 元操作/条件加伤等给 Babel 技能设计灵感 |
| ★ | 槽位上限/流派分组 | §3 SkillSlot + 神祇 | 引导 build,但需配套 UI,中期再做 |
| ✗ | Rarity 双维度 / `~` CSV / StatHierarchy 树 | §1/§5/§4.4 | 对 Babel 规模过度设计 |

---

*生成于 2026-06-03。基于 DMD 反编译工程静态分析。源码行号可能随版本变动,以类名/方法名定位为准。*
