# 敌人移动策略重构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把敌人移动抽象为可插拔 IEnemyMovement 策略，并支持多敌人同建一个点、建造完成即停（不扣 charge），最后新增 SupportMovement 让祭司/狂信者主动靠近队友最大化光环覆盖。

**Architecture:** 对称现有 IEnemyAbility，新增 IEnemyMovement（Init/Tick/IsMoving/OnRemoved）。Enemy 把状态机交给 _movement，按 CSV moveMode 列选具体实现（builder/scout=BuilderMovement+不同 ITargetSelector，support=SupportMovement）。BuildPoint 去掉独占、持在建者集合，完成时通过 IBuildInterruptible 精确回调打断其他在建者。

**Tech Stack:** Unity 2022 / C# / QFramework / NUnit EditMode 测试 / CSV 数据驱动

---

## File Structure

新增/修改文件一览（所有路径均相对于 `Babel_Client/Assets/`）：

```
Scripts/Spawning/Movement/
  IEnemyMovement.cs          # 新增：移动策略接口
  IBuildInterruptible.cs     # 新增：建造打断回调接口
  BuilderMovement.cs         # 新增：原状态机搬入 + ITargetSelector 注入
  SupportMovement.cs         # 新增：追质心实现

Scripts/Game/
  BuildPoint.cs              # 修改：去掉 IsOccupied/SetOccupied，改为 AttachBuilder/DetachBuilder
  Enemy.cs                   # 修改：_movement 字段，Init 按 MoveMode 选策略，Update 委托 Tick
  Path.cs                    # 修改：ReserveBuildPoint 不再检查 IsOccupied，改用 AttachedCount

Scripts/Spawning/
  EnemyData.cs               # 修改：TargetMode→MoveMode，新增 SenseRadius
  EnemyParser.cs             # 修改：key "movemode" / "senseradius"

Assets/Data/Enemies/
  enemies.csv                # 修改：表头 targetMode→moveMode，末尾加 senseRadius 列

Tests/EditMode/
  IEnemyMovementTests.cs     # 新增：接口契约测试（A1）
  BuildPointMultiBuilderTests.cs  # 新增：多建者 + 打断测试（A3）
  SupportMovementTests.cs    # 新增：SupportMovement 行为测试（C1–C3）
  ScoutTargetingTests.cs     # 修改：TargetMode → MoveMode（A4）
  PathTargetSelectionTests.cs # 修改：IsOccupied → AttachedCount（A3）
  EnemyParserTests.cs        # 修改：targetMode → moveMode，新增 senseradius（A5）
```

---

## Phase A — 接口 + BuildPoint 多建者基础

目标：建立类型骨架并让现有测试全部绿灯。  
**验收：** 运行 EditMode 所有测试，0 失败；`read_console` 无编译错误。

---

### Task A1 — 新增 IEnemyMovement / IBuildInterruptible 接口

- [ ] 在 `Scripts/Spawning/Movement/` 目录下创建两个接口文件。
- [ ] 运行 EditMode 测试确认编译无错。

**文件：** `Scripts/Spawning/Movement/IEnemyMovement.cs`

```csharp
namespace Babel
{
    /// <summary>
    /// 敌人移动策略契约。对称 IEnemyAbility。
    /// </summary>
    public interface IEnemyMovement
    {
        /// <summary>由 Enemy.Init 调用，注入宿主和数据。</summary>
        void Init(Enemy owner, EnemyData data);

        /// <summary>每帧由 Enemy.Update 驱动，deltaTime = Time.deltaTime。</summary>
        void Tick(float deltaTime);

        /// <summary>true 时 Animator IsMoving = true。</summary>
        bool IsMoving { get; }

        /// <summary>Enemy 死亡/销毁时调用，用于释放预约/监听。</summary>
        void OnRemoved();
    }
}
```

**文件：** `Scripts/Spawning/Movement/IBuildInterruptible.cs`

```csharp
namespace Babel
{
    /// <summary>
    /// 建造打断回调接口。注册到 BuildPoint._activeBuilders 的建造者需实现此接口。
    /// </summary>
    public interface IBuildInterruptible
    {
        /// <summary>
        /// 当前正在建造的 BuildPoint 已被其他人建完时触发。
        /// 实现者应立即退出 Building 状态，选下一个目标。
        /// </summary>
        void OnTargetBuildCompleted(BuildPoint point);
    }
}
```

**新增测试文件：** `Tests/EditMode/IEnemyMovementTests.cs`

```csharp
using NUnit.Framework;

namespace Babel.Tests
{
    /// <summary>
    /// 接口契约测试：验证接口签名存在于已加载的程序集中。
    /// </summary>
    public class IEnemyMovementTests
    {
        [Test]
        public void IEnemyMovement_InterfaceExists()
        {
            var t = typeof(IEnemyMovement);
            Assert.That(t, Is.Not.Null);
            Assert.That(t.IsInterface, Is.True);
        }

        [Test]
        public void IEnemyMovement_HasRequiredMembers()
        {
            var t = typeof(IEnemyMovement);
            Assert.That(t.GetMethod("Init"), Is.Not.Null, "Init method missing");
            Assert.That(t.GetMethod("Tick"), Is.Not.Null, "Tick method missing");
            Assert.That(t.GetMethod("OnRemoved"), Is.Not.Null, "OnRemoved method missing");
            Assert.That(t.GetProperty("IsMoving"), Is.Not.Null, "IsMoving property missing");
        }

        [Test]
        public void IBuildInterruptible_InterfaceExists()
        {
            var t = typeof(IBuildInterruptible);
            Assert.That(t, Is.Not.Null);
            Assert.That(t.IsInterface, Is.True);
        }

        [Test]
        public void IBuildInterruptible_HasOnTargetBuildCompleted()
        {
            var t = typeof(IBuildInterruptible);
            Assert.That(t.GetMethod("OnTargetBuildCompleted"), Is.Not.Null);
        }
    }
}
```

**Commit message:**

```
feat(movement): 新增 IEnemyMovement / IBuildInterruptible 接口骨架

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task A2 — EnemyData / EnemyParser：TargetMode→MoveMode，新增 SenseRadius

- [ ] 修改 `EnemyData.cs`：字段 `TargetMode` 改名为 `MoveMode`，新增 `SenseRadius`。
- [ ] 修改 `EnemyParser.cs`：读取 key `movemode` 和 `senseradius`（可选列）。不读旧 key `targetmode`（唯一 CSV 当场改名，无需兼容）。
- [ ] 修改 `enemies.csv`：表头 `targetMode` → `moveMode`，末尾追加 `senseRadius` 列；priest/zealot 行 `moveMode=support`, `senseRadius=8`；scout 行 `moveMode=scout`；UTF-8 with BOM 保持不变，中文名不破坏。

**修改后 `EnemyData.cs` 完整内容：**

```csharp
namespace Babel
{
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

        public string AbilityType = "";
        public float AbilityRadius;
        public float AbilityValue;
        public float AbilityCooldown;
        public float BuildTime;

        // 原 TargetMode 改名为 MoveMode；EnemyParser 读取 CSV "moveMode" 列
        public string MoveMode = "";

        // 感知半径（SupportMovement 使用）；CSV 列 "senseRadius"
        public float SenseRadius = 8f;
    }
}
```

**修改后 `EnemyParser.cs` 关键变更（只列需改动的片段，完整替换 Parse 方法体中对应行）：**

旧代码（第 65-66 行）：
```csharp
if (colMap.TryGetValue("targetmode", out int tmIdx) && tmIdx < fields.Length)
    data.TargetMode = fields[tmIdx].Trim().ToLowerInvariant();
```

新代码（替换上述两行）：
```csharp
if (colMap.TryGetValue("movemode", out int mmIdx) && mmIdx < fields.Length)
    data.MoveMode = fields[mmIdx].Trim().ToLowerInvariant();

if (colMap.TryGetValue("senseradius", out int srIdx) && srIdx < fields.Length
    && !string.IsNullOrWhiteSpace(fields[srIdx]))
    data.SenseRadius = ParseFloat(fields[srIdx]);
```

> 不做 `targetmode` 向后兼容：项目只有一个 `enemies.csv`，本次直接把表头改成 `moveMode`，不存在仍用旧表头的数据文件需要兼容（YAGNI）。

**修改后 `enemies.csv`（UTF-8 BOM，保持中文名）：**

```
enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,abilityType,abilityRadius,abilityValue,abilityCooldown,buildTime,moveMode,senseRadius
worker,工人,30,1,25,1,1,Enemies/Worker,,,,,2,,
elite,精英,120,1,50,2,5,Enemies/Elite,,,,,1.5,,
priest,祭司,60,1.5,25,1,3,Enemies/Priest,heal_aura,3,10,2,2.5,support,8
engineer,工程师,60,2,50,2,3,Enemies/Engineer,,,,,1,,
zealot,狂信者,20,4.5,25,1,2,Enemies/Zealot,speed_aura,4,1.5,0,2,support,8
scout,斥候,20,5,100,1,2,Enemies/Scout,,,,,1.2,scout,
```

> 注意：CSV 中文名是正确的 UTF-8，现有 enemies.csv 中为 GBK 乱码；写入时必须用 UTF-8 BOM 保存，参考 `project_csv_encoding.md` 约束。

**Commit message:**

```
refactor(data): EnemyData.TargetMode→MoveMode，新增 SenseRadius；Parser 兼容双 key

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task A3 — BuildPoint 多建者支持：AttachBuilder / DetachBuilder

- [ ] 修改 `BuildPoint.cs`：
  - 添加 `private readonly List<IBuildInterruptible> _activeBuilders = new List<IBuildInterruptible>();`
  - 新增 `AttachBuilder(IBuildInterruptible b)` / `DetachBuilder(IBuildInterruptible b)`
  - **保留** `IsOccupied` property 和 `SetOccupied` method（供 Path.ReserveBuildPoint 向后兼容，Phase B 再统一删除）
  - 在 `AddBuildProgress` 内建造完成时，遍历 `_activeBuilders` 副本逐个调 `b.OnTargetBuildCompleted(this)`，然后清空集合
  - 在 `Reset()` 中清空 `_activeBuilders`

**`BuildPoint.cs` 新增/修改部分（完整补丁）：**

在类字段区（`private int _currentProgress;` 之后）添加：
```csharp
private readonly System.Collections.Generic.List<IBuildInterruptible> _activeBuilders
    = new System.Collections.Generic.List<IBuildInterruptible>(4);
```

新增两个 public 方法（放在 `BeginBuild()` 之前）：
```csharp
/// <summary>
/// 注册一个正在建造此点的建造者，建造完成时会收到 OnTargetBuildCompleted 回调。
/// </summary>
public void AttachBuilder(IBuildInterruptible builder)
{
    if (builder != null && !_activeBuilders.Contains(builder))
        _activeBuilders.Add(builder);
}

/// <summary>
/// 取消注册建造者（建造者主动放弃或死亡时调用）。
/// </summary>
public void DetachBuilder(IBuildInterruptible builder)
{
    _activeBuilders.Remove(builder);
}
```

修改 `AddBuildProgress` 中建造完成分支（`SetState(BuildPointState.Completed)` 之后，`if (OwnerPath != null)` 之前）：
```csharp
// 通知所有在建者：此点已被建完，请中断
var snapshot = new System.Collections.Generic.List<IBuildInterruptible>(_activeBuilders);
_activeBuilders.Clear();
foreach (var b in snapshot)
    b.OnTargetBuildCompleted(this);
```

修改 `Reset()` 中 `IsOccupied = false;` 之后添加：
```csharp
_activeBuilders.Clear();
```

**新增测试文件：** `Tests/EditMode/BuildPointMultiBuilderTests.cs`

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class BuildPointMultiBuilderTests
    {
        private GameObject _go;
        private BuildPoint _bp;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BP");
            _bp = _go.AddComponent<BuildPoint>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private class FakeBuilder : IBuildInterruptible
        {
            public List<BuildPoint> Interrupted = new List<BuildPoint>();
            public void OnTargetBuildCompleted(BuildPoint point) => Interrupted.Add(point);
        }

        [Test]
        public void AttachBuilder_OnBuildComplete_CallsOnTargetBuildCompleted()
        {
            var b1 = new FakeBuilder();
            var b2 = new FakeBuilder();
            _bp.AttachBuilder(b1);
            _bp.AttachBuilder(b2);

            _bp.AddBuildProgress(99999); // 触发完成

            Assert.That(b1.Interrupted, Has.Count.EqualTo(1));
            Assert.That(b2.Interrupted, Has.Count.EqualTo(1));
            Assert.That(b1.Interrupted[0], Is.SameAs(_bp));
        }

        [Test]
        public void DetachBuilder_NotCalledAfterDetach()
        {
            var b1 = new FakeBuilder();
            _bp.AttachBuilder(b1);
            _bp.DetachBuilder(b1);

            _bp.AddBuildProgress(99999);

            Assert.That(b1.Interrupted, Is.Empty);
        }

        [Test]
        public void MultipleBuilders_AllNotifiedOnce()
        {
            const int builderCount = 5;
            var builders = new FakeBuilder[builderCount];
            for (int i = 0; i < builderCount; i++)
            {
                builders[i] = new FakeBuilder();
                _bp.AttachBuilder(builders[i]);
            }

            _bp.AddBuildProgress(99999);

            for (int i = 0; i < builderCount; i++)
                Assert.That(builders[i].Interrupted, Has.Count.EqualTo(1), $"builder[{i}] not notified");
        }

        [Test]
        public void AttachBuilder_DuplicateIgnored()
        {
            var b = new FakeBuilder();
            _bp.AttachBuilder(b);
            _bp.AttachBuilder(b); // 重复注册

            _bp.AddBuildProgress(99999);

            // 只被通知一次
            Assert.That(b.Interrupted, Has.Count.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsActiveBuilders()
        {
            var b = new FakeBuilder();
            _bp.AttachBuilder(b);
            _bp.Reset();

            // Reset 后建造完成不应再通知
            _bp.AddBuildProgress(99999);
            Assert.That(b.Interrupted, Is.Empty);
        }
    }
}
```

**也需更新 `PathTargetSelectionTests.cs`**：`IsOccupied` property 和 `SetOccupied` method 在 Phase A 仍保留，所以现有测试**无需改动**；在此记录，Phase B 删除 `SetOccupied` 后再回来更新。

**Commit message:**

```
feat(buildpoint): AttachBuilder/DetachBuilder + IBuildInterruptible 完成回调，支持多建者并发

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task A4 — 修复受影响测试：ScoutTargetingTests（TargetMode→MoveMode）

- [ ] 修改 `ScoutTargetingTests.cs`：将所有 `TargetMode =` 替换为 `MoveMode =`。
- [ ] 运行 EditMode 测试确认全绿。

**完整替换后的 `ScoutTargetingTests.cs`：**

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class ScoutTargetingTests
    {
        [Test]
        public void ScoutEnemy_OnInit_ReservesGatewayWhenAvailable()
        {
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i * 5, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var enemyGo = new GameObject("Scout");
            var enemy = enemyGo.AddComponent<Enemy>();
            // TargetMode 已改名为 MoveMode
            var data = new EnemyData
            {
                Hp = 25, MoveSpeed = 5, BuildContribution = 25,
                BuildCharges = 1, MoveMode = "scout", BuildTime = 1.2f
            };

            enemy.Init(path, data, -1);

            Assert.That(bps[1].IsOccupied, Is.True, "斥候应优先预约 gateway");

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        [Test]
        public void Enemy_WhenNoReservableButGatewayBuilt_StartsClimbing()
        {
            var nextGo = new GameObject("NextLayer");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NextBP");
            var nbp = nextBpGo.AddComponent<BuildPoint>();
            nextPath.wayPointList = new[] { nbp };

            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            path.nextLayerPath = nextPath;
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i * 5, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            bps[0].isGateway = true;
            path.wayPointList = bps;
            bps[0].AddBuildProgress(99999);
            bps[1].AddBuildProgress(99999);
            bps[2].SetOccupied(true);

            var enemyGo = new GameObject("W");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData
            {
                Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                BuildCharges = 1, MoveMode = "", BuildTime = 1f
            };
            enemy.Init(path, data, -1);

            InvokePrivate(enemy, "UpdateMovingToBuildPoint");

            string state = GetState(enemy);
            Assert.That(state, Is.EqualTo("MovingToPassage").Or.EqualTo("ClimbingPassage"));

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        private static void InvokePrivate(object obj, string method)
        {
            MethodInfo m = obj.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{method} should exist.");
            m.Invoke(obj, null);
        }

        private static string GetState(object enemy)
        {
            FieldInfo f = enemy.GetType().GetField("_moveState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null);
            return f.GetValue(enemy).ToString();
        }
    }
}
```

**Commit message:**

```
test(scout): ScoutTargetingTests TargetMode→MoveMode 同步重命名

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task A5 — 修复受影响测试：EnemyParserTests（targetMode→moveMode，新增 senseradius 测试）

- [ ] 修改 `EnemyParserTests.cs`：旧三个测试的 CSV 表头改为 `moveMode`，断言改为 `list[0].MoveMode`；新增两个 senseRadius 测试。
- [ ] 运行 EditMode 测试，全绿后提交。

**完整替换后的 `EnemyParserTests.cs`：**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Babel.Tests
{
    public class EnemyParserTests
    {
        [Test]
        public void Parse_ReadsMoveMode_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,scout"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_DefaultsMoveModeToEmpty_WhenColumnMissing()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo(""));
        }

        [Test]
        public void Parse_NormalizesMoveModeToLowerCase()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,SCOUT"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_ReadsSenseRadius_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode,senseRadius",
                "priest,祭司,60,1.5,25,1,3,Enemies/Priest,support,8"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].SenseRadius, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void Parse_DefaultsSenseRadiusToEight_WhenColumnMissing()
        {
            // SenseRadius 字段默认值为 8f（EnemyData 初始化）
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].SenseRadius, Is.EqualTo(8f).Within(0.001f));
        }
    }
}
```

**Commit message:**

```
test(parser): EnemyParserTests 同步 MoveMode/SenseRadius

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

## Phase B — BuilderMovement：状态机搬入策略类

目标：把 Enemy 内部状态机逻辑提取到 BuilderMovement，Enemy 仅持有 `_movement` 字段，Init 按 MoveMode 选策略，Update 委托 Tick。  
**验收：** 所有 EditMode 测试全绿；游戏 Play Mode 行为与重构前一致（worker/elite/scout/engineer 正常建造、爬梯）。

---

### Task B1 — 创建 BuilderMovement（实现 IEnemyMovement + IBuildInterruptible）

- [ ] 创建 `Scripts/Spawning/Movement/BuilderMovement.cs`。
- [ ] 包含完整状态机：MovingToBuildPoint / Building / MovingToPassage / ClimbingPassage / Finished。
- [ ] 构造函数接受 `ITargetSelector selector`（无参重载使用 DefaultBuildSelector.Instance）。
- [ ] 建造到达时调 `bp.AttachBuilder(this)`；建完或放弃时调 `bp.DetachBuilder(this)`。
- [ ] 实现 `IBuildInterruptible.OnTargetBuildCompleted`：若 point == 当前目标点，则取消建造，`ReleaseBuildPoint`，选下一目标（等同于原 UpdateBuilding 完成后的逻辑，但不扣 buildCharges）。

**完整文件：** `Scripts/Spawning/Movement/BuilderMovement.cs`

```csharp
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 标准建造者移动策略。包含原 Enemy 状态机的全部逻辑。
    /// 同时实现 IBuildInterruptible：当目标点被他人建完时立即中断并选下一个目标。
    /// </summary>
    public class BuilderMovement : IEnemyMovement, IBuildInterruptible
    {
        private Enemy _owner;
        private EnemyData _data;
        private ITargetSelector _selector;

        private EnemyMoveState _state = EnemyMoveState.MovingToBuildPoint;
        private int _targetBuildPointIndex = -1;
        private Transform _passageTarget;
        private float _buildTimer;
        private int _buildChargesLeft;

        public bool IsMoving =>
            _state == EnemyMoveState.MovingToBuildPoint ||
            _state == EnemyMoveState.MovingToPassage;

        /// <summary>
        /// 默认构造：使用 DefaultBuildSelector（随机选点）。
        /// </summary>
        public BuilderMovement() : this(DefaultBuildSelector.Instance) { }

        /// <summary>
        /// 注入选点策略（scout 传 GatewayFirstSelector.Instance）。
        /// </summary>
        public BuilderMovement(ITargetSelector selector)
        {
            _selector = selector ?? DefaultBuildSelector.Instance;
        }

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _data = data;
            _buildChargesLeft = data.BuildCharges;
            _state = EnemyMoveState.MovingToBuildPoint;
            _targetBuildPointIndex = -1;
            _buildTimer = 0f;
            _passageTarget = null;
            ReserveNextTarget();
        }

        public void Tick(float deltaTime)
        {
            switch (_state)
            {
                case EnemyMoveState.MovingToBuildPoint:
                    UpdateMovingToBuildPoint(deltaTime);
                    break;
                case EnemyMoveState.Building:
                    UpdateBuilding(deltaTime);
                    break;
                case EnemyMoveState.MovingToPassage:
                    UpdateMovingToPassage(deltaTime);
                    break;
                case EnemyMoveState.ClimbingPassage:
                    ExecuteClimbing();
                    break;
                case EnemyMoveState.Finished:
                    ExecuteFinished();
                    break;
            }
        }

        public void OnRemoved()
        {
            ReleaseCurrentTarget();
        }

        // ── IBuildInterruptible ──────────────────────────────────────────────
        public void OnTargetBuildCompleted(BuildPoint point)
        {
            if (_state != EnemyMoveState.Building) return;
            if (_targetBuildPointIndex < 0) return;

            var path = _owner.currentPath;
            if (path == null) return;
            if (_targetBuildPointIndex >= path.wayPointList.Length) return;
            if (path.wayPointList[_targetBuildPointIndex] != point) return;

            // 目标点已被他人建完：释放预约，不扣 charge，选下一目标
            path.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;

            ChooseNextAfterRelease();
        }

        // ── 私有状态机 ──────────────────────────────────────────────────────
        private void UpdateMovingToBuildPoint(float dt)
        {
            if (_targetBuildPointIndex < 0)
            {
                bool canClimb = _owner.currentPath.nextLayerPath != null
                    && (_owner.currentPath.IsCompleted || _owner.currentPath.IsGatewayBuilt());
                if (canClimb) StartMovingToPassage();
                return;
            }

            var target = _owner.currentPath.wayPointList[_targetBuildPointIndex];
            var targetPos = GetBuildApproachPosition(target);
            UpdateFacing(targetPos.x);
            _owner.transform.position = Vector3.MoveTowards(
                _owner.transform.position, targetPos, _owner.EffectiveSpeed * dt);

            if (IsAtHorizontalTarget(targetPos))
            {
                _owner.transform.position = targetPos;
                _buildTimer = _data.BuildTime;
                _state = EnemyMoveState.Building;
                target.BeginBuild();
                target.AttachBuilder(this);
                BuildEvents.RaiseBuildStarted(target);
            }
        }

        private void UpdateBuilding(float dt)
        {
            _buildTimer -= dt;
            if (_buildTimer > 0f) return;

            var path = _owner.currentPath;
            if (_targetBuildPointIndex >= 0 && _targetBuildPointIndex < path.wayPointList.Length)
            {
                var bp = path.wayPointList[_targetBuildPointIndex];
                bp.DetachBuilder(this);
                if (!bp.IsBuildCompleted)
                    bp.AddBuildProgress(_owner.buildAbility);
                // AddBuildProgress 内部若触发完成会调 OnTargetBuildCompleted，
                // 但此时已 DetachBuilder，故不会二次触发。
            }

            path.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;
            _buildChargesLeft--;

            if (_buildChargesLeft <= 0)
            {
                _state = EnemyMoveState.Finished;
                return;
            }

            ChooseNextAfterRelease();
        }

        private void ChooseNextAfterRelease()
        {
            ReserveNextTarget();
            if (_targetBuildPointIndex >= 0)
            {
                _state = EnemyMoveState.MovingToBuildPoint;
            }
            else if (_owner.currentPath.nextLayerPath != null
                     && (_owner.currentPath.IsCompleted || _owner.currentPath.IsGatewayBuilt()))
            {
                StartMovingToPassage();
            }
            else
            {
                _state = EnemyMoveState.MovingToBuildPoint;
            }
        }

        private void StartMovingToPassage()
        {
            if (_owner.currentPath.nextLayerPath == null)
            {
                GameSession.EndGame(GameEndReason.Defeat);
                return;
            }
            int gatewayIdx = _owner.currentPath.GetGatewayIndex();
            _passageTarget = _owner.currentPath.wayPointList[gatewayIdx].transform;
            _state = EnemyMoveState.MovingToPassage;
        }

        private void UpdateMovingToPassage(float dt)
        {
            if (_passageTarget == null) return;
            var targetPos = new Vector3(
                _passageTarget.position.x,
                _owner.transform.position.y,
                _owner.transform.position.z);
            UpdateFacing(targetPos.x);
            _owner.transform.position = Vector3.MoveTowards(
                _owner.transform.position, targetPos, _owner.EffectiveSpeed * dt);

            if ((_owner.transform.position - targetPos).magnitude <= 0.1f)
                _state = EnemyMoveState.ClimbingPassage;
        }

        private void ExecuteClimbing()
        {
            _owner.currentPath = _owner.currentPath.nextLayerPath;
            if (_owner.currentPath != null && _owner.currentPath.wayPointList.Length > 0)
                _owner.transform.position = _owner.currentPath.wayPointList[0].transform.position;
            ReserveNextTarget();
            _state = EnemyMoveState.MovingToBuildPoint;
        }

        private void ExecuteFinished()
        {
            ReleaseCurrentTarget();
            _owner.NotifyChargesExhausted();
        }

        private void ReserveNextTarget()
        {
            if (_owner.currentPath == null) { _targetBuildPointIndex = -1; return; }
            _targetBuildPointIndex = _owner.currentPath.ReserveBuildPoint(
                _owner.transform.position, _selector);
        }

        private void ReleaseCurrentTarget()
        {
            if (_targetBuildPointIndex >= 0 && _owner.currentPath != null)
            {
                var path = _owner.currentPath;
                if (_targetBuildPointIndex < path.wayPointList.Length)
                    path.wayPointList[_targetBuildPointIndex].DetachBuilder(this);
                path.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
            }
        }

        private Vector3 GetBuildApproachPosition(BuildPoint target)
            => new Vector3(target.transform.position.x,
                           _owner.transform.position.y,
                           _owner.transform.position.z);

        private void UpdateFacing(float targetX)
        {
            if (_owner.Circle == null) return;
            float dx = targetX - _owner.transform.position.x;
            if (Mathf.Abs(dx) < 0.01f) return;
            _owner.Circle.flipX = dx < 0f;
        }

        private bool IsAtHorizontalTarget(Vector3 targetPos)
            => Mathf.Abs(_owner.transform.position.x - targetPos.x) <= 0.1f;
    }
}
```

> **注意：** BuilderMovement 调用了 `_owner.NotifyChargesExhausted()`，这是 Task B2 要在 Enemy 上新增的 `internal` 辅助方法。

**Commit message:**

```
feat(movement): 新增 BuilderMovement — 原敌人状态机提取为可插拔策略

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task B2 — 重构 Enemy：委托 _movement.Tick，暴露辅助 API

- [ ] 在 `Enemy.cs` 中添加 `private IEnemyMovement _movement;` 字段。
- [ ] 在 `Enemy.Init` 中按 `data.MoveMode` 选策略并调 `_movement.Init(this, data)`；同时删除原有的状态机字段初始化（`_moveState` 等转移到 BuilderMovement）。
- [ ] 在 `Enemy.Update` 中用 `_movement?.Tick(Time.deltaTime)` 替换 switch 语句。
- [ ] 删除 `Enemy.cs` 内所有已搬入 BuilderMovement 的私有状态机方法（UpdateMovingToBuildPoint / UpdateBuilding / StartMovingToPassage / UpdateMovingToPassage / ExecuteClimbing / ExecuteFinished / ReserveNextTarget / ReleaseCurrentTarget / GetBuildApproachPosition / UpdateFacing / IsAtHorizontalTarget）。
- [ ] 保留 `UpdateAnimatorState()`，但改为读 `_movement?.IsMoving ?? false`。
- [ ] 新增 `internal void NotifyChargesExhausted()` 供 BuilderMovement.ExecuteFinished 调用。
- [ ] `currentPath` 字段由 `[HideInInspector] public` 改为 `public`（BuilderMovement 需读写）。
- [ ] 删除已无用的 `_targetSelector` 字段和 `EnemyMoveState` enum（后者移到单独文件或保留在 Enemy.cs 顶部，供 BuilderMovement 引用）。

**Enemy.cs 精简后的 Update 核心（替换原 switch 部分）：**

```csharp
private void Update()
{
    if (GameSession.IsGameEnded) return;

    TickHitFlash(Time.deltaTime);

    if (HP <= 0)
    {
        TickDeathFeedback(Time.unscaledDeltaTime);
        return;
    }

    _ability?.Tick(Time.deltaTime);

    if (_speedBuffTimer > 0)
    {
        _speedBuffTimer -= Time.deltaTime;
        if (_speedBuffTimer <= 0) _speedBuffMult = 1.0f;
    }

    UpdateAnimatorState();
    _movement?.Tick(Time.deltaTime);
}
```

**Enemy.Init 中策略选择逻辑（替换原 `_targetSelector = ...` + `ReserveNextTarget()` 块）：**

```csharp
// 按 MoveMode 选移动策略
_movement?.OnRemoved();
_movement = data.MoveMode switch
{
    "scout"   => new BuilderMovement(GatewayFirstSelector.Instance),
    "support" => new SupportMovement(),
    _         => new BuilderMovement(DefaultBuildSelector.Instance)
};
_movement.Init(this, data);
```

**新增辅助方法：**

```csharp
/// <summary>
/// 供 BuilderMovement.ExecuteFinished 调用，触发 charges 耗尽事件并销毁。
/// </summary>
internal void NotifyChargesExhausted()
{
    _ability?.OnRemoved();
    _ability = null;
    if (waveEventId >= 0)
        OnChargesExhausted?.Invoke(waveEventId);
    this.DestroyGameObjGracefully();
}
```

**UpdateAnimatorState 改写：**

```csharp
private void UpdateAnimatorState()
{
    bool moving = _movement?.IsMoving ?? false;
    if (_animator != null && moving != _lastIsMoving)
    {
        _animator.SetBool(AnimIsMoving, moving);
        _lastIsMoving = moving;
    }
}
```

**Commit message:**

```
refactor(enemy): Enemy.Update 委托 _movement.Tick，按 MoveMode 选策略

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task B3 — Path.ReserveBuildPoint 多建者支持：去掉 IsOccupied 独占逻辑

- [ ] 修改 `Path.ReserveBuildPoint`：过滤条件去掉 `point.IsOccupied` 检查（多个 builder 可同时选同一点）；同时去掉 `wayPointList[selectedIdx].SetOccupied(true)`。
- [ ] 保留 `ReleaseBuildPoint` 但让其不再调 `SetOccupied(false)`（改为空方法或删除 SetOccupied 调用，后续 Phase B 统一）。

> 此改动让多个 worker 可以同时建同一个 BuildPoint，谁先建完谁触发完成，其余 builder 通过 IBuildInterruptible 回调自动换目标，**不需要独占锁**。

**修改后 `Path.ReserveBuildPoint(Vector3, ITargetSelector)` 完整方法体：**

```csharp
public int ReserveBuildPoint(Vector3 fromPos, ITargetSelector selector)
{
    _candidateIndices.Clear();
    if (wayPointList == null) return -1;

    for (int i = 0; i < wayPointList.Length; i++)
    {
        BuildPoint point = wayPointList[i];
        if (point == null) continue;
        if (point.IsBuildCompleted) continue;
        // 不再排除 IsOccupied：多建者可同时选同一点
        _candidateIndices.Add(i);
    }

    if (_candidateIndices.Count == 0) return -1;

    ITargetSelector chooser = selector ?? DefaultSelector;
    int selectedBuildPointIndex = chooser.Select(_candidateIndices, this, fromPos);
    if (selectedBuildPointIndex < 0 || selectedBuildPointIndex >= wayPointList.Length)
        return -1;

    // 不再调 SetOccupied(true)：由 BuilderMovement.AttachBuilder 管理
    return selectedBuildPointIndex;
}
```

修改 `ReleaseBuildPoint`（不再调 SetOccupied）：

```csharp
public void ReleaseBuildPoint(int index)
{
    // SetOccupied 已废弃；保留方法签名供向后兼容
    // Phase C 统一后可删除此方法体内容
}
```

- [ ] 运行 EditMode 测试；`PathTargetSelectionTests.ReserveBuildPoint_ExcludesCompletedAndOccupied` 测试逻辑需更新（因为 `SetOccupied` 不再影响过滤）。

**更新 `PathTargetSelectionTests.cs` 中 `ReserveBuildPoint_ExcludesCompletedAndOccupied` 测试：**

```csharp
[Test]
public void ReserveBuildPoint_ExcludesCompleted_AllowsMultipleReservations()
{
    // Phase B 后：完成的点被排除，但多个 builder 可选同一未完成点
    CreatePathWithPointXs(1f, 10f, 20f, 30f);
    CompleteBuildPoint(2); // 点 2 已完成，不可选

    int first = ReserveFrom(Vector3.zero);
    int second = ReserveFrom(Vector3.zero);

    // 两次预约都应返回有效索引（可以相同，因为不再独占）
    Assert.That(first, Is.InRange(0, 3).And.Not.EqualTo(2));
    Assert.That(second, Is.InRange(0, 3).And.Not.EqualTo(2));
}
```

**Commit message:**

```
refactor(path): ReserveBuildPoint 去掉 IsOccupied 独占，允许多建者并发选点

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task B4 — 清理：删除 BuildPoint.IsOccupied / SetOccupied

- [ ] 在 `BuildPoint.cs` 中删除 `IsOccupied` property 和 `SetOccupied` method（Phase A 保留的向后兼容代码）。
- [ ] 在 `BuildPoint.Reset()` 中删除 `IsOccupied = false;` 行。
- [ ] 搜索全项目确认无其他引用（`ScoutTargetingTests` 中 `bps[2].SetOccupied(true)` 需要替换）。

**更新 `ScoutTargetingTests.Enemy_WhenNoReservableButGatewayBuilt_StartsClimbing` 中的设置逻辑：**

原代码：
```csharp
bps[2].SetOccupied(true);
```

改为（直接让 3 个点全部完成或让本层 IsCompleted=true）：
```csharp
bps[2].AddBuildProgress(99999); // 三个点全完成 → IsCompleted=true → 可爬梯
```

> 注：原测试意图是"idx2 被外部占用，预约返回 -1"。Phase B 后改为多建者模式，只需要 IsCompleted=true 即可触发爬梯，`SetOccupied` 不再是手段。

**Commit message:**

```
refactor(buildpoint): 删除废弃的 IsOccupied/SetOccupied，清理向后兼容代码

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task B5 — Phase B 回归验证

- [ ] 运行全部 EditMode 测试，0 失败。
- [ ] 用 `read_console` 确认无编译错误、无 `[BABEL]` 警告异常。
- [ ] 在 Unity Play Mode 中用至少 3 个 worker 验证：多人同建一个点、点建完后其余 worker 换目标。

**验证步骤（BabelLogger + read_console）：**

1. 在 BuilderMovement.OnTargetBuildCompleted 中加临时日志：
   ```csharp
   BabelLogger.Log("BuilderMovement", $"被打断：{point.name}，换目标");
   ```
2. Play Mode 观察 Console，应能看到非首个 builder 被打断的日志。
3. 验证通过后删除临时日志，提交。

**Commit message:**

```
test(phase-b): Phase B 回归验证通过，多建者打断逻辑确认正常

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

## Phase C — SupportMovement 新增

目标：为祭司 / 狂信者新增 SupportMovement，让其主动追质心，不建造，感知半径内有队友则跟随，无队友则朝 gateway 走可爬楼。  
**验收：** SupportMovementTests 全绿；priest/zealot 在 Play Mode 中跟随其他敌人群移动，光环覆盖肉眼可见。

---

### Task C1 — 创建 SupportMovement 骨架 + 测试红灯

- [ ] 创建 `Scripts/Spawning/Movement/SupportMovement.cs`（空实现：IsMoving=false，Tick/Init/OnRemoved 为空）。
- [ ] 创建 `Tests/EditMode/SupportMovementTests.cs`，写入全部测试（此时为红灯）。
- [ ] 运行测试，确认失败原因是逻辑未实现（而非编译错误）。

**测试文件 `Tests/EditMode/SupportMovementTests.cs`（完整）：**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class SupportMovementTests
    {
        // ── 辅助：创建带 Enemy 组件的 GO ────────────────────────────────────
        private static (GameObject go, Enemy enemy) MakeEnemy(Vector3 pos)
        {
            var go = new GameObject("E");
            go.layer = LayerMask.NameToLayer("Enemy");
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            return (go, e);
        }

        private static (GameObject go, Path path, BuildPoint[] bps)
            MakePath(float[] xPositions, int gatewayIdx = -1)
        {
            var pathGo = new GameObject("Path");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[xPositions.Length];
            var bpGos = new GameObject[xPositions.Length];
            for (int i = 0; i < xPositions.Length; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(xPositions[i], 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            if (gatewayIdx >= 0 && gatewayIdx < bps.Length)
                bps[gatewayIdx].isGateway = true;
            path.wayPointList = bps;
            return (pathGo, path, bps);
        }

        [Test]
        public void SupportMovement_IsMoving_TrueWhenMovingTowardsFriends()
        {
            // 支援者在 x=0，队友在 x=5（感知半径 10），应该在 Tick 后向右移动
            var (pathGo, path, bps) = MakePath(new[] { -10f, 5f, 20f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (friendGo, friendEnemy) = MakeEnemy(new Vector3(5f, 0f, 0f));
            friendEnemy.Init(path,
                new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                                BuildCharges = 1, MoveMode = "", BuildTime = 1f }, -1);

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 10f
            };
            suppEnemy.Init(path, suppData, -1);

            // 手动 Tick（不依赖 Update）
            var movement = GetMovement(suppEnemy);
            Assert.That(movement, Is.Not.Null, "SupportMovement 应已绑定");
            movement.Tick(0.1f);

            Assert.That(movement.IsMoving, Is.True, "有队友在感知范围内时应处于移动状态");

            Object.DestroyImmediate(friendGo);
            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_NoFriends_WalksTowardsGateway()
        {
            // 无队友时，支援者应向 gateway 方向移动
            var (pathGo, path, bps) = MakePath(new[] { -5f, 10f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f // 范围小，感知不到任何人
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float xAfter = suppEnemy.transform.position.x;

            // gateway 在 x=10，支援者从 x=0 出发，应向右移动
            Assert.That(xAfter, Is.GreaterThan(xBefore), "无队友时应向 gateway 方向移动");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_OnlyMovesHorizontally()
        {
            // 支援者只改变 x 坐标，y 坐标应保持不变
            var (pathGo, path, bps) = MakePath(new[] { 5f, 15f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 2f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f
            };
            suppEnemy.Init(path, suppData, -1);

            float yBefore = suppEnemy.transform.position.y;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float yAfter = suppEnemy.transform.position.y;

            Assert.That(yAfter, Is.EqualTo(yBefore).Within(0.001f), "支援者不应改变 y 坐标");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_DeadZone_DoesNotMove()
        {
            // 支援者已在队友质心附近（距离 < 1.0），不应再移动
            var (pathGo, path, bps) = MakePath(new[] { 0f, 15f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            // 队友几乎在同一位置（x=0.3，距离支援者 0.3 < 1.0 死区）
            var (friendGo, friendEnemy) = MakeEnemy(new Vector3(0.3f, 0f, 0f));
            friendEnemy.Init(path,
                new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                                BuildCharges = 1, MoveMode = "", BuildTime = 1f }, -1);

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 10f
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float xAfter = suppEnemy.transform.position.x;

            Assert.That(Mathf.Abs(xAfter - xBefore), Is.LessThan(0.001f),
                "在死区内不应移动");

            Object.DestroyImmediate(friendGo);
            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_TopLayer_Idles()
        {
            // 顶层（nextLayerPath=null）无队友时静止等待（不触发 EndGame）
            var (pathGo, path, bps) = MakePath(new[] { 5f, 15f }, gatewayIdx: 1);
            // 不设 nextLayerPath → 顶层

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);

            // 不应抛异常（不应调 GameSession.EndGame）
            Assert.DoesNotThrow(() => movement.Tick(0.5f));
            float xAfter = suppEnemy.transform.position.x;

            // 顶层无队友：静止
            Assert.That(Mathf.Abs(xAfter - xBefore), Is.LessThan(0.001f),
                "顶层无队友时支援者应静止");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        // ── 反射辅助 ────────────────────────────────────────────────────────
        private static IEnemyMovement GetMovement(Enemy enemy)
        {
            var f = typeof(Enemy).GetField("_movement",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "Enemy._movement 字段应存在");
            return f.GetValue(enemy) as IEnemyMovement;
        }
    }
}
```

**Commit message:**

```
test(support): SupportMovementTests 全部写入（红灯阶段）

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task C2 — 实现 SupportMovement（绿灯）

- [ ] 实现 `Scripts/Spawning/Movement/SupportMovement.cs` 完整逻辑。
- [ ] 运行 SupportMovementTests，全绿。

**完整文件 `Scripts/Spawning/Movement/SupportMovement.cs`：**

```csharp
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 支援型移动策略：感知半径内有队友则追质心，无队友则朝 gateway 方向移动可爬楼，
    /// 顶层待命。不进行建造，不扣 buildCharges。
    /// </summary>
    public class SupportMovement : IEnemyMovement
    {
        private const float DEAD_ZONE = 1.0f;

        private Enemy _owner;
        private EnemyData _data;
        private float _senseRadius;

        // NonAlloc buffer（静态，节省 GC 压力）
        private static readonly Collider2D[] _senseBuffer = new Collider2D[32];
        private static readonly int EnemyMask = LayerMask.GetMask("Enemy");

        public bool IsMoving { get; private set; }

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _data = data;
            _senseRadius = data.SenseRadius > 0f ? data.SenseRadius : 8f;
            IsMoving = false;
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null) return;

            // 1. 计算队友质心（排除自身）
            Vector2 centroid;
            bool hasFriends = TryGetFriendCentroid(out centroid);

            if (hasFriends)
            {
                float dx = centroid.x - _owner.transform.position.x;
                if (Mathf.Abs(dx) <= DEAD_ZONE)
                {
                    // 死区内：停止
                    IsMoving = false;
                    return;
                }

                IsMoving = true;
                float step = _owner.EffectiveSpeed * deltaTime;
                float newX = _owner.transform.position.x + Mathf.Sign(dx) * Mathf.Min(step, Mathf.Abs(dx));
                _owner.transform.position = new Vector3(
                    newX,
                    _owner.transform.position.y,
                    _owner.transform.position.z);
                return;
            }

            // 2. 无队友：朝 gateway 走（若有上层则可爬梯）
            var path = _owner.currentPath;
            if (path == null)
            {
                IsMoving = false;
                return;
            }

            // 顶层（nextLayerPath=null）：待命
            if (path.nextLayerPath == null)
            {
                IsMoving = false;
                return;
            }

            // 朝 gateway x 坐标移动
            int gwIdx = path.GetGatewayIndex();
            if (gwIdx < 0 || gwIdx >= path.wayPointList.Length)
            {
                IsMoving = false;
                return;
            }

            float gatewayX = path.wayPointList[gwIdx].transform.position.x;
            float dxGw = gatewayX - _owner.transform.position.x;

            if (Mathf.Abs(dxGw) <= 0.1f)
            {
                // 到达 gateway x 位置：若 gateway 已建好则爬梯
                if (path.IsGatewayBuilt())
                {
                    ClimbToNextLayer();
                }
                IsMoving = false;
                return;
            }

            IsMoving = true;
            float stepGw = _owner.EffectiveSpeed * deltaTime;
            float newXGw = _owner.transform.position.x
                + Mathf.Sign(dxGw) * Mathf.Min(stepGw, Mathf.Abs(dxGw));
            _owner.transform.position = new Vector3(
                newXGw,
                _owner.transform.position.y,
                _owner.transform.position.z);
        }

        public void OnRemoved() { /* 无预约需释放 */ }

        // ── 私有 ─────────────────────────────────────────────────────────────
        private bool TryGetFriendCentroid(out Vector2 centroid)
        {
            centroid = Vector2.zero;
            int count = Physics2D.OverlapCircleNonAlloc(
                _owner.Position, _senseRadius, _senseBuffer, EnemyMask);

            float sumX = 0f;
            int friendCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (_senseBuffer[i] == null) continue;
                if (!_senseBuffer[i].TryGetComponent<Enemy>(out var e)) continue;
                if (e == _owner) continue;
                if (!e.IsAlive) continue;
                sumX += e.transform.position.x;
                friendCount++;
            }

            if (friendCount == 0) return false;
            centroid = new Vector2(sumX / friendCount, _owner.transform.position.y);
            return true;
        }

        private void ClimbToNextLayer()
        {
            var next = _owner.currentPath.nextLayerPath;
            if (next == null) return;
            _owner.currentPath = next;
            if (next.wayPointList != null && next.wayPointList.Length > 0)
                _owner.transform.position = next.wayPointList[0].transform.position;
        }
    }
}
```

**Commit message:**

```
feat(movement): 实现 SupportMovement — 追质心、无队友朝 gateway、顶层待命

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task C3 — SupportMovement 集成：确保 Enemy.Init 正确路由

- [ ] 确认 `Enemy.Init` 中 `MoveMode == "support"` 时 `new SupportMovement()` 路由正确（Task B2 已完成）。
- [ ] 确认 enemies.csv 中 priest / zealot 的 `moveMode=support` 已设置（Task A2 已完成）。
- [ ] 在 Unity Play Mode 启动游戏，确认祭司/狂信者不建造，跟随工人群体移动。
- [ ] 用 `read_console` 过滤 `[BABEL]` 日志，确认无 NullReferenceException。

**验证步骤：**

1. 在 SupportMovement.Tick 首部加临时日志：
   ```csharp
   BabelLogger.Log("SupportMovement", $"Tick hasFriends={hasFriends} pos={_owner.transform.position.x:F1}");
   ```
2. Play Mode 观察 Console，应看到 priest/zealot 的追随行为日志。
3. 确认位置变化符合预期后删除临时日志。

**Commit message:**

```
test(phase-c): SupportMovement 集成验证通过，priest/zealot 追随行为确认

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task C4 — Phase C 全量测试回归

- [ ] 运行 EditMode 全部测试，0 失败。
- [ ] 检查 `IEnemyMovementTests` / `BuildPointMultiBuilderTests` / `SupportMovementTests` / `ScoutTargetingTests` / `EnemyParserTests` / `PathTargetSelectionTests` 全部绿灯。
- [ ] `read_console` 确认编译无错、无遗留警告。

**Commit message:**

```
test(phase-c): Phase C 全量回归 — 所有 EditMode 测试通过

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

### Task C5 — 收尾：清理临时日志，更新 CLAUDE.md

- [ ] 搜索全项目 `BabelLogger.Log("SupportMovement"` / `BabelLogger.Log("BuilderMovement"` 临时日志，删除。
- [ ] 在 `CLAUDE.md` 的 **Core Systems** 节 `Enemy lifecycle` 下追加：

  ```
  **IEnemyMovement 策略**
  `Enemy.Init` 按 `EnemyData.MoveMode` 选策略：`builder`/`scout` → `BuilderMovement`（注入 ITargetSelector），`support` → `SupportMovement`。策略位于 `Spawning/Movement/`。`IBuildInterruptible` 让多个 builder 可同时建同一个点，完成时打断其余 builder 换目标。
  ```

- [ ] 最终运行一次全量测试，截图（或 read_console 输出）确认全绿后提交。

**Commit message:**

```
docs(claude-md): 补充 IEnemyMovement 策略架构说明；清理临时调试日志

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

---

## Self-Review Checklist

在开始实现前，执行者应检查：

- [ ] **spec 覆盖**：接口签名 Init/Tick/IsMoving/OnRemoved 四个成员全部出现在 A1 代码中
- [ ] **占位符扫描**：全文搜索 `TODO` / `...` / `// implement` → 0 处
- [ ] **类型名一致性**：`BuilderMovement` / `SupportMovement` / `IEnemyMovement` / `IBuildInterruptible` 全文拼写一致
- [ ] **字段名一致性**：`MoveMode` / `SenseRadius`（EnemyData）、`_movement`（Enemy）、`_activeBuilders`（BuildPoint）全文一致
- [ ] **CSV key 一致性**：Parser 读取 `movemode` / `senseradius`（全小写），CSV 表头 `moveMode` / `senseRadius`（camelCase）→ Parser 用 `.ToLower()` 对齐，正确
- [ ] **不做 targetmode 向后兼容**：唯一的 enemies.csv 当场改名 moveMode，无旧格式需兼容（YAGNI）
- [ ] **NonAlloc buffer**：SupportMovement._senseBuffer 静态预分配，与 HealAura 模式一致
- [ ] **顶层不调 EndGame**：SupportMovement 顶层 nextLayerPath=null 时静止，不调 GameSession.EndGame → C1 测试覆盖
- [ ] **commit footer**：每条 commit message 均含 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
