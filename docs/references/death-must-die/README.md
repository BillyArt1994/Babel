# Death Must Die — 逆向分析知识库

> **这是什么**:对 `H:\Death_Must_Die`(Unity 反编译工程)各系统的逆向分析索引。
> **为什么**:DMD 是 Babel 的核心参考对象(同为幸存者/Roguelite)。分析一次、沉淀成可导航地图,后续参考实现时**直接定位**,不必重新通读源码。
>
> 源码根:`H:\Death_Must_Die\DeathMustDie\Assets\Scripts\Death\`(下文 `<S>\`)
> 数据根:`H:\Death_Must_Die\DeathMustDie_Config\`(CSV/INI 配置表)
> 引擎:Unity 2022 + 自研 Claw 框架(`Claw.Core.*`),代码为 IL 反编译产物(无注释、行号易变,**以类名/方法名定位**)。

---

## 📑 已分析模块

| 系统 | 文档 | 覆盖内容 | 状态 |
|------|------|---------|------|
| **技能 / 升级系统**(Boon) | [`skill-upgrade-system.md`](./skill-upgrade-system.md) | 数据模型、等级化数值、三选一流程、121个Ability效果、CSV加载 | ✅ 完整 |
| **怪物移动 / AI 系统** | [`monster-movement-ai.md`](./monster-movement-ai.md) | 双层架构(AI行为树+Steering)、11种转向行为、Cohesion群聚、CSV+TOML配置 | ✅ 完整 |

### 技能升级系统 — 文档内锚点速查
> 文档:[`skill-upgrade-system.md`](./skill-upgrade-system.md)

| 想查什么 | 跳转锚点 | 核心类 |
|----------|---------|--------|
| Boon 数据结构 / Rarity×Level 双维度 | [§1 数据模型](./skill-upgrade-system.md#1-数据模型boon-的静态与运行时) | `BoonData` / `BoonInstance` |
| 一个技能怎么编码"多等级数值" | [§2 等级化数值](./skill-upgrade-system.md#2-等级化数值levelbasedabilities) | `LevelBasedAbilities` |
| 升级三选一怎么生成/应用 | [§3 升级流程](./skill-upgrade-system.md#3-升级三选一选择流程) | `RewardGenerator` / `System_Rewards` / `BoonManager` |
| 效果如何实现 / 触发器 / 数值计算 | [§4 效果分发](./skill-upgrade-system.md#4-效果分发ability-架构) | `IAbility` / `AbilityTrigger` / `StatHierarchy` |
| CSV 怎么解析成技能数据 | [§5 CSV加载](./skill-upgrade-system.md#5-csv-数据加载) | `Parser` / `BoonTable` / `CsvLine` |
| **Babel 该借鉴什么** | [附:优先级建议](./skill-upgrade-system.md#附babel-参考实现优先级建议) | — |

### 怪物移动/AI — 文档内锚点速查
> 文档：[`monster-movement-ai.md`](./monster-movement-ai.md)

| 想查什么 | 跳转锚点 | 核心类 |
|----------|---------|--------|
| 双层架构总览 / 结论 | [§0 结论先行](./monster-movement-ai.md#0-结论先行) | `Controller_Ai` / `SteeringAgent2D` |
| AI 行为树节点类型（28种） | [§1.2 节点类型枚举](./monster-movement-ai.md#12-aini-节点类型枚举) | `TomlAi` / `AiNodeTemplate` |
| Ai.ini 真实配置示例 | [§1.3 Ai.ini 示例](./monster-movement-ai.md#13-aini-真实示例) | — |
| 11 种 Steering 行为库完整列表 | [§2.2 Steering 行为库](./monster-movement-ai.md#22-11-种内置-steering-行为库) | `SteeringBehaviour` 子类 |
| Cohesion 群聚核心逻辑（代码级） | [§2.3 Cohesion 核心逻辑](./monster-movement-ai.md#23-steering_cohesion-核心逻辑babel-最关心) | `Steering_Cohesion` |
| 每帧数据流（CSV→决策→移动） | [§3 数据流图](./monster-movement-ai.md#3-数据流图) | `Controller_Ai` / `SteeringAgent2D` |
| Monsters.csv 关键列 / 配置文件 | [§4 CSV 配置速查](./monster-movement-ai.md#4-csv--配置字段速查) | `Parser` / `AiBindings` |
| **Babel 该借鉴什么 / SupportMovement 实施建议** | [§5 映射 Babel](./monster-movement-ai.md#5-映射-babel--借鉴优先级与实施建议) | `IEnemyMovement` / `SupportMovement` |
| 所有结论的源码证据 | [§6 证据表](./monster-movement-ai.md#6-证据表) | — |

---

## 🗺️ 待分析模块(DMD 全景索引)

> 按系统归类,标注文件数与关键目录。需要时再深入分析并补 ✅ 文档。
> 文件数粗略反映复杂度(`<S>\` 下各命名空间 .cs 数量)。

### 运行时核心(Run)
| 系统 | 关键目录 | 文件数 | 说明 |
|------|---------|--------|------|
| **效果/能力库** | `Death.Run.Behaviours.Abilities` | 256 | 121个 `Ability_*` + 31个 `Trigger_*` + Condition(技能系统已覆盖其架构,但单个效果细节未逐一展开) |
| 主动技能 | `Death.Run.Behaviours.Abilities.Actives` | 38 | 主动技能的具体行为(冲刺变体、施法等) |
| **实体/单位** | `Death.Run.Behaviours.Entities` | 124 | 玩家/怪物/投射物的行为组件、移动、碰撞、动画驱动 |
| **核心系统** | `Death.Run.Systems` | 53 | BoonManager 同级的各 System_*(Rewards/Spawn/XP/Darkness 等管理器) |
| 怪物 AI | `Death.Run.Behaviours.AI` | 42 | 怪物决策、`Controller_Ai`、行为节点（**已分析见 [monster-movement-ai.md](./monster-movement-ai.md)**） |
| 事件总线 | `Death.Run.Behaviours.Events` | 40 | `Event_*` 全量(BoonGained/LevelUp 等只是其中几个) |
| 状态(buff/debuff) | `Death.Run.Behaviours.Statuses` | 29 | Burn/Stun/Chill 等状态效果实现(配 `Statuses.csv`) |
| 瞬发效果 | `Death.Run.Behaviours.Instants` | 19 | `Instant_*` 一次性效果 |
| 运行时核心数据 | `Death.Run.Core` | 109 | BoonRarity/RarityRules/各种枚举与核心结构 |
| 数值/能力核心 | `Death.Run.Core.Abilities` | 19 | IAbility/LevelBasedAbilities/StatModifier(技能系统已覆盖) |
| 遭遇/刷怪 | `Death.Run.Encounters` | 12 | 波次、刷怪编排(配 `MonSpawn.csv`/`Encounters.ini`) |

### 物品/装备(Items)
| 系统 | 关键目录 | 文件数 | 说明 |
|------|---------|--------|------|
| 装备系统 | `Death.Items` + `Death.Run.UserInterface.Items` | 39+57 | 装备稀有度、词缀(affix)、套装、唯一物品(配 `Items_*.csv`) |

### 元进度(Meta)
| 系统 | 关键目录 | 文件数 | 说明 |
|------|---------|--------|------|
| 时间领域(局外成长) | `Death.TimesRealm` + `.UserInterface.Upgrades` | 19+19 | 局外永久升级树(类似肉鸽的 meta 进度) |
| 黑暗难度 | `Death.Darkness` | 15 | 难度递增机制(配 `Darkness.csv`/`DarknessBuffs.csv`) |
| 成就 | `Death.Run.Achievements` | 29 | `AchievementTracker_*`(配 `Achievements.csv`) |

### 世界/关卡
| 系统 | 关键目录 | 文件数 | 说明 |
|------|---------|--------|------|
| 世界生成 | `Death.WorldGen` | 36 | 地图/关卡程序生成 |
| 转向/移动 | `Death.Steering.Behaviours` | 11 | steering behavior(群体移动)（**已分析见 [monster-movement-ai.md](./monster-movement-ai.md)**） |

### 表现/UI/框架
| 系统 | 关键目录 | 文件数 | 说明 |
|------|---------|--------|------|
| 数据表加载 | `Death.Data.Tables` + `Death.Data.Parsing` | 35+ | 所有 CSV→运行时对象(技能系统已覆盖 Boon 部分) |
| UI 框架 | `Death.UserInterface` + `Death.Run.UserInterface.*` | 55+ | HUD/Boon面板/物品/本地化 |
| 渲染 | `Death.Rendering` | 27 | 后处理、光照、全屏特效 |
| 音频 | `Death.Audio` | 13 | `AudioController` |
| 本地化 | `Death.UserInterface.Localization` | 25 | 多语言(配 `Nar_*.csv` 叙事文本) |
| 工具库 | `Death.Utils` + `.Collections` + `.Csv` | 78+ | CSV读取器、集合、加权随机 `WeighedRandomSet` 等 |

---

## 📂 配置表速查(`DeathMustDie_Config\`)

| CSV | 内容 | 对应系统 |
|-----|------|---------|
| `Boons.csv` / `BoonsAttacks.csv` | 技能定义(多稀有度×多等级) | 技能系统 ✅ |
| `Talents.csv` | 天赋树(被动) | 元进度 |
| `Gifts.csv` | 神祇礼物(条件触发增益) | 技能系统(扩展) |
| `Statuses.csv` | 状态效果(Burn/Stun…) | 状态系统 |
| `Monsters.csv` / `MonAbilities.csv` / `MonSpawn.csv` / `MonGrowth.csv` | 怪物/技能/刷怪/成长 | 怪物+遭遇 |
| `Items_*.csv` | 装备(词缀/原型/唯一/宝物) | 装备系统 |
| `Characters.csv` | 角色(初始槽位 `CountPerBoonSlot` 等) | 技能系统(角色配置) |
| `Experience.csv` | 升级经验曲线 | XP/升级 |
| `Darkness*.csv` | 难度递增 | 黑暗难度 |
| `GlobalStats.csv` | 全局数值(各种概率/权重) | 数值系统 |

---

## 🔧 分析方法备忘

- **定位类**:`grep -rl "class Xxx" <S>` 或 `find <S> -name "Xxx.cs"`
- **效果映射表**:`Parser.cs` 的 `AbilityTypeParsers` 字典(effect_type 字符串 → 工厂)
- **CSV 超大**:用 `head`/`sed`/`awk -F,` 取局部,勿全量 cat
- **`~` 符号**:CSV 里表示"继承本列上一个非~值",由 `CsvLine` 构造时替换(见技能文档 §5.5)
- **新增模块分析后**:在本 README「已分析模块」表加一行 + 锚点速查表,并在记忆 `reference_dmd_skill_system.md` 同步

---

*知识库建立于 2026-06-03。逐模块增量补充。*
