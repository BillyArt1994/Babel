# 斥候兵种 + 选点策略抽象 实现计划

> **For agentic workers:** 用 TDD 逐任务实现。所有改代码用 subagent + sonnet 模型。步骤用 checkbox（`- [ ]`）跟踪。每个任务后用 Unity Test Runner（`mcp__UnityMCP__run_tests` assembly=`Babel.EditModeTests`）验证，逻辑行为用 console log 验证，绝不用截图代替逻辑验证。

**Goal:** 新增"斥候"兵种——可靠抢建 gateway 抢先打通上楼通道；并把敌人选点逻辑抽象成可插拔的 TargetSelector 策略，工人改为全候选随机。

**Architecture:** CSV 加 `targetMode` 列，`Enemy.Init` 按 targetMode switch 成 `ITargetSelector` 策略对象（对齐现有 `abilityType` → 能力对象的模式）。selector 只负责"从候选 index 列表中挑一个"，筛选/占用/返回仍由 `Path.ReserveBuildPoint` 持有。gateway 建好后作为"公共梯子"，工人在本层无可预约点时走过去爬。

**Tech Stack:** Unity 2022.3 / C# / QFramework / NUnit (Unity Test Framework EditMode) / MCP for Unity。

---

## 背景事实（实现者必读）

1. **命名空间** 全部为 `Babel`。日志前缀 `[BABEL][SystemName]`，用 `Debug.LogWarning`。
2. **asmdef 现状**：`Assets/Scripts/Babel.asmdef` 已存在，把全部游戏脚本编入名为 `Babel` 的程序集（不再是 `Assembly-CSharp`）。测试程序集 `Babel.EditModeTests.asmdef` 已 references `["Babel", "QFramework.CoreKit"]`。
3. **当前测试基线是红的**（68 个测试，约 25 个失败），原因全部相同：8 个老测试文件的 `RequireType` 辅助方法硬编码 `Type.GetType($"{fullName}, Assembly-CSharp")`，但类型现在在 `Babel` 程序集里 → 返回 null。**这是 asmdef 迁移的遗留破坏，与本功能无关，但必须先修，否则无法在绿色基线上工作。** 这是 Phase 0。
4. **选点现状**（`Path.ReserveBuildPoint`，Path.cs:42-72）：
   - 筛掉"已建完 `IsBuildCompleted` / 已占用 `IsOccupied`"的点 → 候选列表 `_reserveCandidates`
   - 按距离**降序**排序
   - `GetFarthestSelectionCount` = `max(1, count/2)`，从"最远一半"里随机抽
   - `SetOccupied(true)` 占住，返回 index
5. **状态机现状**（`Enemy.cs`）：`MovingToBuildPoint → Building → MovingToPassage → ClimbingPassage → Finished`。`UpdateMovingToBuildPoint`（239行）里：若 `_targetBuildPointIndex < 0` 且 `currentPath.IsCompleted` → `StartMovingToPassage()`。`UpdateBuilding`（265行）建完一个点后 `buildCharges--`，归零进 `Finished`。
6. **gateway** 是 `Path.wayPointList` 里一个普通成员，`isGateway==true`。`Path.GetGatewayIndex()` 返回它的下标（无则返回 0）。`StartMovingToPassage`（306行）走向 gateway、爬到 `nextLayerPath`；若 `nextLayerPath==null`（顶层）→ `GameSession.EndGame(Defeat)`。

## 已确认设计决策（不可偏离）

| # | 决策 |
|---|------|
| D1 | 工人选点：从"最远一半随机"改为**全候选随机**（所有未建未占点等概率） |
| D2 | 删除 `GetFarthestSelectionCount`、降序排序、`BuildPointCandidateDistanceComparer`（全候选随机后无用） |
| D3 | 选点抽象为 `ITargetSelector`：`int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos)`，只挑选不占用 |
| D4 | `DefaultBuildSelector`（工人）= 全候选随机；`GatewayFirstSelector`（斥候）= 候选含 gateway 则选之，否则委托 default |
| D5 | CSV 加 `targetMode` 列。空/`build` → DefaultBuildSelector；`scout` → GatewayFirstSelector。`Enemy.Init` 按 targetMode switch |
| D6 | 占用机制 `IsOccupied` **保留** |
| D7 | gateway **计入** `IsCompleted`（代码不动，只修 CLAUDE.md 过时文字） |
| D8 | 建 gateway 的人**不爬**（建完按 charge 正常走）；爬楼只发生于：预约不到任何点（返回-1）且本层 gateway 已建且有上层 → 走过去爬。爬楼不耗 charge |
| D9 | 斥候 gateway 可抢则抢（占用保证唯一），否则（已建/已占/顶层无）退化为普通工人 |

## 文件清单

- **改** `Babel_Client/Assets/Tests/EditMode/*.cs`（8 个文件的 RequireType 辅助）— Phase 0
- **新增** `Babel_Client/Assets/Scripts/Spawning/Targeting/ITargetSelector.cs`
- **新增** `Babel_Client/Assets/Scripts/Spawning/Targeting/DefaultBuildSelector.cs`
- **新增** `Babel_Client/Assets/Scripts/Spawning/Targeting/GatewayFirstSelector.cs`
- **改** `Babel_Client/Assets/Scripts/Game/Path.cs`（ReserveBuildPoint 接 selector；删距离排序；加 `IsGatewayBuilt` 查询）
- **改** `Babel_Client/Assets/Scripts/Spawning/EnemyData.cs`（加 `TargetMode` 字段）
- **改** `Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs`（解析 `targetMode` 列）
- **改** `Babel_Client/Assets/Scripts/Game/Enemy.cs`（Init 建 selector；预约用 selector；爬梯分支）
- **改** `Babel_Client/Assets/Data/Enemies/enemies.csv`（加 `targetMode` 列 + 斥候行）
- **改** `Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs`（旧"最远一半"断言改为"全候选随机"）
- **新增** `Babel_Client/Assets/Tests/EditMode/TargetSelectorTests.cs`
- **改** `H:/Babel/CLAUDE.md`（gateway 完成语义）
- **斥候 prefab**：暂复用 Worker prefab 路径或新建 Resources/Enemies/Scout.prefab（见 Task 9）

---

## Phase 0：修复测试基线（前置，非功能）

### Task 0: 让反射 RequireType 同时支持 Babel 与 Assembly-CSharp 程序集

**Files:**
- Modify: `Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs:155-160`
- Modify: `Babel_Client/Assets/Tests/EditMode/GameEndLifecycleTests.cs:360-363`
- Modify: `Babel_Client/Assets/Tests/EditMode/DebugStatusBarTests.cs:339-342`
- Modify: `Babel_Client/Assets/Tests/EditMode/MainMenuTests.cs:127-130`
- Modify: `Babel_Client/Assets/Tests/EditMode/SkillCooldownHudTests.cs:219-222`
- Modify: `Babel_Client/Assets/Tests/EditMode/SkillSystemStartupTests.cs:64-67`
- Modify: `Babel_Client/Assets/Tests/EditMode/TransientEnemyPoolTests.cs:99-102`
- Modify: `Babel_Client/Assets/Tests/EditMode/UISkillHudTests.cs:398-401`
- Modify: `Babel_Client/Assets/Tests/EditMode/UpgradeSystemTests.cs:357-360`
- Modify: `Babel_Client/Assets/Tests/EditMode/PortraitUILayoutTests.cs:270`

- [ ] **Step 1: 确认基线红**

Run（MCP）: `run_tests` assembly=`Babel.EditModeTests`，poll `get_test_job`。
Expected: 多个失败，消息均为 `... should exist in Assembly-CSharp. Expected: not null But was: null`。

- [ ] **Step 2: 替换每个 RequireType 的类型解析为跨程序集查找**

把每个文件里这种写法：
```csharp
private static Type RequireType(string fullName)
{
    Type type = Type.GetType($"{fullName}, Assembly-CSharp");
    Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
    return type;
}
```
替换为（优先 Babel 程序集，回退 Assembly-CSharp，再回退全程序集扫描）：
```csharp
private static Type RequireType(string fullName)
{
    Type type = Type.GetType($"{fullName}, Babel")
              ?? Type.GetType($"{fullName}, Assembly-CSharp")
              ?? Type.GetType(fullName);
    if (type == null)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType(fullName);
            if (type != null) break;
        }
    }
    Assert.That(type, Is.Not.Null, $"{fullName} should exist in a loaded assembly.");
    return type;
}
```

对 `PortraitUILayoutTests.cs:270` 的内联写法 `Type.GetType("Babel.UIGamePanel, Assembly-CSharp")`，替换为：
```csharp
Type panelType = Type.GetType("Babel.UIGamePanel, Babel")
              ?? Type.GetType("Babel.UIGamePanel, Assembly-CSharp");
```

注意：每个文件改前先 `Read` 确认该文件确实有此 helper（行号可能因文件微调而漂移，用 Grep 定位 `Assembly-CSharp`）。

- [ ] **Step 3: 编译并重跑全部 EditMode 测试**

Run（MCP）: `refresh_unity` compile=request，等 `editor_state.compilation.is_compiling==false`，读 `read_console` types=["error"] 确认 0 编译错误。再 `run_tests` assembly=`Babel.EditModeTests`。
Expected: 之前因 `Assembly-CSharp` null 而失败的 ~25 个测试全部转绿。（若仍有非该原因的失败，记录但不在本任务处理。）

- [ ] **Step 4: 提交**

```bash
git add Babel_Client/Assets/Tests/EditMode/*.cs Babel_Client/Assets/Scripts/Babel.asmdef.meta
git commit -m "test: 反射 RequireType 跨程序集查找，修复 asmdef 迁移导致的测试基线破坏"
```
（一并把未跟踪的 `Babel.asmdef.meta`、`XpSystem.cs.meta`、`XpSystemTests.cs` 等正当产物纳入；临时脚本 `tools/fix_skills_csv*.py`、`tools/skills_new.csv` 不要提交。）

---

## Phase 1：选点策略抽象 + 工人全候选随机

### Task 1: 定义 ITargetSelector 接口 + DefaultBuildSelector（全候选随机）

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/Targeting/ITargetSelector.cs`
- Create: `Babel_Client/Assets/Scripts/Spawning/Targeting/DefaultBuildSelector.cs`
- Test: `Babel_Client/Assets/Tests/EditMode/TargetSelectorTests.cs`

- [ ] **Step 1: 写失败测试（DefaultBuildSelector 返回候选之一）**

创建 `TargetSelectorTests.cs`：
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class TargetSelectorTests
    {
        [Test]
        public void DefaultBuildSelector_ReturnsOneOfTheCandidates()
        {
            Random.InitState(1);
            var selector = new DefaultBuildSelector();
            var candidates = new List<int> { 2, 5, 7 };

            int chosen = selector.Select(candidates, null, Vector3.zero);

            Assert.That(candidates, Does.Contain(chosen));
        }

        [Test]
        public void DefaultBuildSelector_WithEmptyCandidates_ReturnsMinusOne()
        {
            var selector = new DefaultBuildSelector();
            int chosen = selector.Select(new List<int>(), null, Vector3.zero);
            Assert.That(chosen, Is.EqualTo(-1));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败（类型不存在）**

Run（MCP）: `run_tests` assembly=`Babel.EditModeTests` test_names=["Babel.Tests.TargetSelectorTests.DefaultBuildSelector_ReturnsOneOfTheCandidates"]。
Expected: 编译失败 / FAIL（`DefaultBuildSelector` / `ITargetSelector` 未定义）。

- [ ] **Step 3: 写接口与实现**

`ITargetSelector.cs`：
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 敌人选点策略：从候选建造点下标列表中挑一个。
    /// 只负责"挑选"，不负责筛选/占用（那些由 Path.ReserveBuildPoint 持有）。
    /// 返回 -1 表示无可选。
    /// </summary>
    public interface ITargetSelector
    {
        int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos);
    }
}
```

`DefaultBuildSelector.cs`：
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>工人默认策略：从全部候选中等概率随机一个。</summary>
    public class DefaultBuildSelector : ITargetSelector
    {
        public int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos)
        {
            if (candidateIndices == null || candidateIndices.Count == 0) return -1;
            int pick = Random.Range(0, candidateIndices.Count);
            return candidateIndices[pick];
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run（MCP）: `refresh_unity` compile=request → 等编译完 → `run_tests` test_names=两个 DefaultBuildSelector 测试。
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add Babel_Client/Assets/Scripts/Spawning/Targeting/ITargetSelector.cs* Babel_Client/Assets/Scripts/Spawning/Targeting/DefaultBuildSelector.cs* Babel_Client/Assets/Tests/EditMode/TargetSelectorTests.cs*
git commit -m "feat(targeting): ITargetSelector 接口 + DefaultBuildSelector 全候选随机"
```

### Task 2: GatewayFirstSelector（斥候策略）

**Files:**
- Create: `Babel_Client/Assets/Scripts/Spawning/Targeting/GatewayFirstSelector.cs`
- Test: `Babel_Client/Assets/Tests/EditMode/TargetSelectorTests.cs`（追加）

- [ ] **Step 1: 写失败测试（候选含 gateway 时必选 gateway；不含时退化）**

在 `TargetSelectorTests.cs` 追加。需要一个真实 `Path` + `BuildPoint`（gateway 标记）。复用 PathTargetSelectionTests 的建场方式但直接引用类型（asmdef 已通）：
```csharp
        [Test]
        public void GatewayFirstSelector_WhenGatewayInCandidates_SelectsGatewayIndex()
        {
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
            }
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var selector = new GatewayFirstSelector();
            int chosen = selector.Select(new List<int> { 0, 1, 2 }, path, Vector3.zero);

            Assert.That(chosen, Is.EqualTo(1));

            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        [Test]
        public void GatewayFirstSelector_WhenNoGatewayInCandidates_FallsBackToCandidate()
        {
            Random.InitState(1);
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
            }
            // gateway 是下标1，但候选里不含1
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var selector = new GatewayFirstSelector();
            var candidates = new List<int> { 0, 2 };
            int chosen = selector.Select(candidates, path, Vector3.zero);

            Assert.That(candidates, Does.Contain(chosen));

            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }
```

- [ ] **Step 2: 跑测试确认失败**

Run（MCP）: `run_tests` test_names=两个 GatewayFirstSelector 测试。
Expected: 编译失败（`GatewayFirstSelector` 未定义）。

- [ ] **Step 3: 实现**

`GatewayFirstSelector.cs`：
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 斥候策略：候选里若含 gateway（未建未占已由 Path 预筛）则强制选它；
    /// 否则委托默认策略（全候选随机），即退化为普通工人。
    /// </summary>
    public class GatewayFirstSelector : ITargetSelector
    {
        private readonly DefaultBuildSelector _fallback = new DefaultBuildSelector();

        public int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos)
        {
            if (candidateIndices == null || candidateIndices.Count == 0) return -1;
            if (path != null && path.wayPointList != null)
            {
                for (int i = 0; i < candidateIndices.Count; i++)
                {
                    int idx = candidateIndices[i];
                    if (idx >= 0 && idx < path.wayPointList.Length)
                    {
                        var bp = path.wayPointList[idx];
                        if (bp != null && bp.isGateway) return idx;
                    }
                }
            }
            return _fallback.Select(candidateIndices, path, fromPos);
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run（MCP）: `refresh_unity` compile=request → `run_tests` test_names=两个 GatewayFirstSelector 测试。
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add Babel_Client/Assets/Scripts/Spawning/Targeting/GatewayFirstSelector.cs* Babel_Client/Assets/Tests/EditMode/TargetSelectorTests.cs
git commit -m "feat(targeting): GatewayFirstSelector 斥候优先建 gateway 策略"
```

### Task 3: Path.ReserveBuildPoint 接入 selector + 改全候选随机

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Path.cs`
- Modify: `Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs`

- [ ] **Step 1: 更新旧测试以反映全候选随机（D1）+ 删除最远一半断言（D2）**

`PathTargetSelectionTests.cs` 现有断言基于"最远一半"。改写：
- 删除 `GetFarthestSelectionCount_UsesFloorHalfWithMinimumOne` 整个测试（D2 删了该方法）。
- 删除 `ReserveBuildPoint_WithThreeCandidates_ReservesFarthestPoint`（断言固定选最远 idx=2，与全候选随机冲突）。
- 删除 `ReserveBuildPoint_WithFourCandidates_ReservesOnlyFromFarthestHalf`（断言只在最远一半，冲突）。
- 保留并改写 `ReserveBuildPoint_ExcludesCompletedAndOccupiedBeforeChoosingFarthestHalf` → 改名 `ReserveBuildPoint_ExcludesCompletedAndOccupied`，断言改为：完成 idx=2、占用 idx=3 后，预约结果 ∈ {0,1} 且被占用：
```csharp
        [Test]
        public void ReserveBuildPoint_ExcludesCompletedAndOccupied()
        {
            CreatePathWithPointXs(1f, 10f, 20f, 30f);
            CompleteBuildPoint(2);
            SetOccupied(3, true);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex == 0 || reservedIndex == 1, Is.True);
            Assert.That(IsOccupied(reservedIndex), Is.True);
        }
```
- 新增一个测试：所有点都可选时，预约结果是合法下标且被占用：
```csharp
        [Test]
        public void ReserveBuildPoint_WithAllAvailable_ReservesSomeValidPoint()
        {
            UnityEngine.Random.InitState(7);
            CreatePathWithPointXs(1f, 2f, 3f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.InRange(0, 2));
            Assert.That(IsOccupied(reservedIndex), Is.True);
        }
```
- 删除 helper `GetFarthestSelectionCount`（测试侧）。

- [ ] **Step 2: 跑测试确认失败**

Run（MCP）: `run_tests` assembly=`Babel.EditModeTests`。
Expected: 新/改写的 PathTargetSelection 测试因 Path 仍是旧"最远一半"逻辑而可能失败（或 ReserveBuildPoint 行为不符）。记录失败。

- [ ] **Step 3: 改 Path.cs**

- 保留 `ReserveBuildPoint(Vector3 fromPos)` 公共签名（旧测试和 Enemy 都用它）。让它内部用 `DefaultBuildSelector` 以保持向后兼容。
- 新增重载 `ReserveBuildPoint(Vector3 fromPos, ITargetSelector selector)`。
- 候选收集逻辑不变（筛掉已建/已占），但**不再排序、不再取最远一半**——把候选下标交给 selector。
- 删除 `GetFarthestSelectionCount`、`_reserveCandidates` 的距离结构、`BuildPointCandidate`、`BuildPointCandidateDistanceComparer`。

替换 `ReserveBuildPoint` 及相关私有成员为：
```csharp
        private static readonly DefaultBuildSelector DefaultSelector = new DefaultBuildSelector();
        private readonly List<int> _candidateIndices = new List<int>(16);

        public int ReserveBuildPoint(Vector3 fromPos)
        {
            return ReserveBuildPoint(fromPos, DefaultSelector);
        }

        public int ReserveBuildPoint(Vector3 fromPos, ITargetSelector selector)
        {
            _candidateIndices.Clear();
            if (wayPointList == null) return -1;

            for (int i = 0; i < wayPointList.Length; i++)
            {
                BuildPoint point = wayPointList[i];
                if (point == null) continue;
                if (point.IsBuildCompleted) continue;
                if (point.IsOccupied) continue;
                _candidateIndices.Add(i);
            }

            if (_candidateIndices.Count == 0) return -1;

            ITargetSelector chooser = selector ?? DefaultSelector;
            int selectedBuildPointIndex = chooser.Select(_candidateIndices, this, fromPos);
            if (selectedBuildPointIndex < 0 || selectedBuildPointIndex >= wayPointList.Length)
                return -1;

            wayPointList[selectedBuildPointIndex].SetOccupied(true);
            return selectedBuildPointIndex;
        }
```
同时删除文件中 `GetFarthestSelectionCount`、`BuildPointCandidate` struct、`BuildPointCandidateDistanceComparer` class、`_reserveCandidates` 字段、`CandidateDistanceComparer` 静态字段（OnDrawGizmos 不依赖它们，保留）。

- [ ] **Step 4: 跑全部 EditMode 测试确认通过**

Run（MCP）: `refresh_unity` compile=request → 读 console 确认 0 错误 → `run_tests` assembly=`Babel.EditModeTests`。
Expected: PathTargetSelection 全绿，其余测试保持绿。

- [ ] **Step 5: 提交**

```bash
git add Babel_Client/Assets/Scripts/Game/Path.cs Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs
git commit -m "feat(path): ReserveBuildPoint 接入 ITargetSelector，工人改全候选随机，删除最远一半逻辑"
```

### Task 4: Path 新增 IsGatewayBuilt 查询（爬梯判断用）

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Path.cs`
- Test: `Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs`（追加）

- [ ] **Step 1: 写失败测试**

```csharp
        [Test]
        public void IsGatewayBuilt_TrueOnlyAfterGatewayPointCompleted()
        {
            CreatePathWithPointXs(1f, 2f);
            SetGateway(1, true);

            Assert.That(IsGatewayBuilt(), Is.False);

            CompleteBuildPoint(1);

            Assert.That(IsGatewayBuilt(), Is.True);
        }
```
辅助方法（追加到该测试类）：
```csharp
        private void SetGateway(int index, bool value)
        {
            FieldInfo f = _buildPointType.GetField("isGateway", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(f, Is.Not.Null);
            f.SetValue(_buildPoints[index], value);
        }

        private bool IsGatewayBuilt()
        {
            MethodInfo m = _pathType.GetMethod("IsGatewayBuilt", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(m, Is.Not.Null, "Path.IsGatewayBuilt should be public.");
            return (bool)m.Invoke(_path, null);
        }
```
（注：此测试类用反射风格，与文件现有风格一致。）

- [ ] **Step 2: 跑测试确认失败**

Run（MCP）: `run_tests` test_names=["Babel.Tests.PathTargetSelectionTests.IsGatewayBuilt_TrueOnlyAfterGatewayPointCompleted"]。
Expected: FAIL（`Path.IsGatewayBuilt` 不存在）。

- [ ] **Step 3: 实现 Path.IsGatewayBuilt**

在 `Path.cs` 加：
```csharp
        /// <summary>本层 gateway 是否已建完（可作为公共梯子）。</summary>
        public bool IsGatewayBuilt()
        {
            if (wayPointList == null) return false;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                var bp = wayPointList[i];
                if (bp != null && bp.isGateway)
                    return bp.IsBuildCompleted;
            }
            return false;
        }
```

- [ ] **Step 4: 跑测试确认通过**

Run（MCP）: `refresh_unity` compile=request → `run_tests` test_names=该测试。
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add Babel_Client/Assets/Scripts/Game/Path.cs Babel_Client/Assets/Tests/EditMode/PathTargetSelectionTests.cs
git commit -m "feat(path): 新增 IsGatewayBuilt 查询，供爬梯判断"
```

---

## Phase 2：数据层 targetMode

### Task 5: EnemyData.TargetMode 字段 + EnemyParser 解析

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Spawning/EnemyData.cs`
- Modify: `Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs`
- Test: 复用现有 EnemyParser 测试位置（若无则新建 `EnemyParserTests.cs`）

- [ ] **Step 1: 查是否已有 EnemyParser 测试**

Run: Grep `EnemyParser` in `Babel_Client/Assets/Tests/EditMode/`。若有，追加测试；若无，新建 `EnemyParserTests.cs`。

- [ ] **Step 2: 写失败测试**

新建/追加 `EnemyParserTests.cs`：
```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Babel.Tests
{
    public class EnemyParserTests
    {
        [Test]
        public void Parse_ReadsTargetModeColumn_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,targetMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,scout"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].TargetMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_DefaultsTargetModeToEmpty_WhenColumnMissing()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].TargetMode, Is.EqualTo(""));
        }
    }
}
```

- [ ] **Step 3: 跑测试确认失败**

Run（MCP）: `run_tests` test_names=两个 EnemyParser 测试。
Expected: 编译失败（`EnemyData.TargetMode` 不存在）。

- [ ] **Step 4: 实现**

`EnemyData.cs` 加字段（放在 BuildTime 后）：
```csharp
        public string TargetMode = "";
```

`EnemyParser.cs` 在可选列解析区（BuildTime 解析之后，line 63 附近）加：
```csharp
                    if (colMap.TryGetValue("targetmode", out int tmIdx) && tmIdx < fields.Length)
                        data.TargetMode = fields[tmIdx].Trim();
```

- [ ] **Step 5: 跑测试确认通过**

Run（MCP）: `refresh_unity` compile=request → `run_tests` test_names=两个 EnemyParser 测试。
Expected: PASS。

- [ ] **Step 6: 提交**

```bash
git add Babel_Client/Assets/Scripts/Spawning/EnemyData.cs Babel_Client/Assets/Scripts/Spawning/EnemyParser.cs Babel_Client/Assets/Tests/EditMode/EnemyParserTests.cs*
git commit -m "feat(enemy): EnemyData.TargetMode 字段 + Parser 解析 targetMode 列"
```

### Task 6: enemies.csv 加 targetMode 列 + 斥候行

**Files:**
- Modify: `Babel_Client/Assets/Data/Enemies/enemies.csv`

- [ ] **Step 1: 改 CSV（UTF-8，禁用 Excel 另存为 GBK）**

表头追加 `,targetMode`；每行末尾补对应值（普通敌人留空，斥候填 `scout`）。注意现有列顺序：`enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,abilityType,abilityRadius,abilityValue,abilityCooldown,buildTime`。新增列加在末尾：
```csv
enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,abilityType,abilityRadius,abilityValue,abilityCooldown,buildTime,targetMode
worker,工人,30,1,25,1,1,Enemies/Worker,,,,,2,
elite,精英,120,3,25,1,5,Enemies/Elite,,,,,1.5,
priest,祭司,60,1.5,25,1,3,Enemies/Priest,heal_aura,3,10,2,2.5,
engineer,工程师,60,2,50,2,3,Enemies/Engineer,,,,,1,
zealot,狂信者,20,4.5,25,1,2,Enemies/Zealot,speed_aura,4,1.5,0,2,
scout,斥候,25,5,25,1,2,Enemies/Scout,,,,,1.2,scout
```
用 Write 整文件覆盖。写完用 `read` 确认无 `�` 乱码（中文须正常）。斥候数值：HP 25（脆但比狂信者耐一点）、移速 5（快，介于狂信者4.5与精英3之间偏快）、buildContribution 25、charge 1、exp 2、buildTime 1.2（建 gateway 较快）。

- [ ] **Step 2: 验证 CSV 能被解析（Play 模式 console 验证）**

Run（MCP）: `manage_editor` play → `execute_code`：
```csharp
var data = Babel.EnemyDatabase.GetById("scout");
if (data == null) return "scout NULL — 未解析";
return $"scout: hp={data.Hp} speed={data.MoveSpeed} targetMode={data.TargetMode} name={data.EnemyName}";
```
Expected: `scout: hp=25 speed=5 targetMode=scout name=斥候`（中文正常、targetMode=scout）。然后 `manage_editor` stop。
（若 EnemyDatabase 未在 Play 时初始化，改为直接 `EnemyParser.Parse(System.IO.File...)` 或读 TextAsset；以实际可用方式确认。）

- [ ] **Step 3: 提交**

```bash
git add Babel_Client/Assets/Data/Enemies/enemies.csv
git commit -m "feat(data): enemies.csv 加 targetMode 列 + 斥候(scout)行"
```

---

## Phase 3：Enemy 接入 selector + 爬梯闭环

### Task 7: Enemy.Init 按 targetMode 建 selector，预约用 selector

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Enemy.cs`

- [ ] **Step 1: 写失败测试（斥候预约时优先占用 gateway）**

新建 `Babel_Client/Assets/Tests/EditMode/ScoutTargetingTests.cs`。构造一个 Path（含 gateway）+ 一个挂了 Enemy 的 GameObject，Init 成 scout，调用其预约路径，断言 gateway 被占用。

Enemy 的预约是私有的 `ReserveNextTarget()`，但其效果可观察：`currentPath.wayPointList[gatewayIdx].IsOccupied` 变 true。用反射调 `Init` 后，反射调 `ReserveNextTarget` 或直接断言 Init 内已预约（Init 末尾调了 ReserveNextTarget）。
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
            // Path + 3 点，gateway=idx1
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
            var data = new EnemyData { Hp = 25, MoveSpeed = 5, BuildContribution = 25, BuildCharges = 1, TargetMode = "scout", BuildTime = 1.2f };

            enemy.Init(path, data, -1);

            Assert.That(bps[1].IsOccupied, Is.True, "斥候应优先预约 gateway");

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }
    }
}
```
（直接引用类型，asmdef 已通。Enemy.Init 是 public。）

- [ ] **Step 2: 跑测试确认失败**

Run（MCP）: `run_tests` test_names=["Babel.Tests.ScoutTargetingTests.ScoutEnemy_OnInit_ReservesGatewayWhenAvailable"]。
Expected: FAIL（当前 Init 用无 selector 的 ReserveBuildPoint，斥候不会偏好 gateway；占用的是随机点，断言 gateway 占用大概率 false）。

- [ ] **Step 3: 改 Enemy.cs**

加字段（在 `_ability` 附近）：
```csharp
        private ITargetSelector _targetSelector;
```
在 `Init`（174行的 ability switch 附近）加 selector 构建：
```csharp
            _targetSelector = data.TargetMode switch
            {
                "scout" => new GatewayFirstSelector(),
                _ => new DefaultBuildSelector()
            };
```
注意：`Init` 末尾在构建 ability **之前**就调了 `ReserveNextTarget()`（170行）。必须把 selector 构建移到 `ReserveNextTarget()` **之前**，否则首次预约时 selector 还是 null。调整顺序：先建 selector，再 `ReserveNextTarget()`，再建 ability。

改 `ReserveNextTarget()`（359行）用 selector：
```csharp
        private void ReserveNextTarget()
        {
            if (currentPath == null)
            {
                _targetBuildPointIndex = -1;
                return;
            }
            _targetBuildPointIndex = currentPath.ReserveBuildPoint(transform.position, _targetSelector);
        }
```

- [ ] **Step 4: 跑测试确认通过**

Run（MCP）: `refresh_unity` compile=request → 读 console 0 错误 → `run_tests` test_names=该测试 + 全部 EditMode 回归。
Expected: PASS，且无回归。

- [ ] **Step 5: 提交**

```bash
git add Babel_Client/Assets/Scripts/Game/Enemy.cs Babel_Client/Assets/Tests/EditMode/ScoutTargetingTests.cs*
git commit -m "feat(enemy): Init 按 targetMode 建 selector，预约改用 selector"
```

### Task 8: 爬梯闭环——预约不到点且 gateway 已建则爬（D8）

**Files:**
- Modify: `Babel_Client/Assets/Scripts/Game/Enemy.cs`

- [ ] **Step 1: 理解现有爬梯入口**

现有 `UpdateMovingToBuildPoint`（239行）：`_targetBuildPointIndex < 0` 且 `currentPath.IsCompleted` → `StartMovingToPassage()`。问题：D8 要求"预约不到点 **且 gateway 已建 且 有上层**"也能爬，不必等整层 `IsCompleted`。`StartMovingToPassage`（306行）已含"无 nextLayerPath → EndGame(Defeat)"与走向 gateway 逻辑。

- [ ] **Step 2: 写失败测试（行为级：预约不到且 gateway 已建 → 进入 MovingToPassage）**

在 `ScoutTargetingTests.cs` 追加。构造：一层 2 点，gateway=idx0 已建完，idx1 也已建完（→ 预约返回 -1），但 IsCompleted 也为 true（两点都完成）。为了隔离"未 IsCompleted 但 gateway 已建"的新路径，构造：3 点，gateway=idx0 已建完，idx1 已建完，idx2 **被占用**（非本敌人）→ 预约返回 -1，但 IsCompleted=false（idx2 未完成）。断言敌人进入 MovingToPassage（即 `_moveState`）。

`_moveState` 是私有，用反射读。需要 nextLayerPath 非空（否则会 EndGame）。
```csharp
        [Test]
        public void Enemy_WhenNoReservableButGatewayBuilt_StartsClimbing()
        {
            var nextGo = new GameObject("NextLayer");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBp = new GameObject("NextBP");
            var nbp = nextBp.AddComponent<BuildPoint>();
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
            // gateway(0) + idx1 建完；idx2 被占（外部）→ 预约 -1，IsCompleted=false
            bps[0].AddBuildProgress(99999);
            bps[1].AddBuildProgress(99999);
            bps[2].SetOccupied(true);

            var enemyGo = new GameObject("W");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25, BuildCharges = 1, TargetMode = "", BuildTime = 1f };
            enemy.Init(path, data, -1);
            // Init 已 ReserveNextTarget()，此时 _targetBuildPointIndex 应为 -1

            // 驱动一次状态机 Update（用反射调私有 Update 或调 UpdateMovingToBuildPoint）
            InvokePrivate(enemy, "UpdateMovingToBuildPoint");

            string state = GetState(enemy);
            Assert.That(state, Is.EqualTo("MovingToPassage").Or.EqualTo("ClimbingPassage"));

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBp);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        private static void InvokePrivate(object obj, string method)
        {
            MethodInfo m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{method} should exist.");
            m.Invoke(obj, null);
        }

        private static string GetState(object enemy)
        {
            FieldInfo f = enemy.GetType().GetField("_moveState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null);
            return f.GetValue(enemy).ToString();
        }
```

- [ ] **Step 3: 跑测试确认失败**

Run（MCP）: `run_tests` test_names=["Babel.Tests.ScoutTargetingTests.Enemy_WhenNoReservableButGatewayBuilt_StartsClimbing"]。
Expected: FAIL（现有逻辑要求 `IsCompleted` 才爬，但本例 IsCompleted=false → 敌人停在 MovingToBuildPoint）。

- [ ] **Step 4: 改 UpdateMovingToBuildPoint**

把（Enemy.cs:241-248）：
```csharp
            if (_targetBuildPointIndex < 0)
            {
                if (currentPath.IsCompleted)
                {
                    StartMovingToPassage();
                }
                return;
            }
```
改为：
```csharp
            if (_targetBuildPointIndex < 0)
            {
                // 本层无可预约点：整层完成 或 gateway 已建好(公共梯子) 且有上层 → 爬梯
                bool canClimb = currentPath.nextLayerPath != null
                    && (currentPath.IsCompleted || currentPath.IsGatewayBuilt());
                if (canClimb)
                {
                    StartMovingToPassage();
                }
                return;
            }
```
同样检查 `UpdateBuilding` 末尾（296行）的 `else if (currentPath.IsCompleted) StartMovingToPassage();` 分支——为一致性，也改为 `else if (currentPath.nextLayerPath != null && (currentPath.IsCompleted || currentPath.IsGatewayBuilt())) StartMovingToPassage();`。注意此分支仅在 `buildCharges > 0` 时到达（charge 归零已在前面 return），符合 D8"爬楼不耗 charge、建 gateway 的人不强制爬"。

- [ ] **Step 5: 跑测试确认通过 + 全回归**

Run（MCP）: `refresh_unity` compile=request → console 0 错误 → `run_tests` assembly=`Babel.EditModeTests`（全部）。
Expected: 新测试 PASS，无回归。

- [ ] **Step 6: 提交**

```bash
git add Babel_Client/Assets/Scripts/Game/Enemy.cs Babel_Client/Assets/Tests/EditMode/ScoutTargetingTests.cs
git commit -m "feat(enemy): 爬梯闭环——预约不到点且gateway已建则爬上层"
```

---

## Phase 4：斥候资源 + 场景集成验证

### Task 9: 斥候 prefab（Resources/Enemies/Scout.prefab）

**Files:**
- Create: `Babel_Client/Assets/Resources/Enemies/Scout.prefab`（经 Unity，非手写）

- [ ] **Step 1: 确认敌人 prefab 缺失时的回退**

CLAUDE.md 记载：prefab 缺失会回退为彩色程序化 sprite。先确认 `TransientEnemyPool` 行为（Grep `Resources.Load` in Spawning）。若缺 Scout.prefab 不致命（回退占位），则 prefab 可作为可选增强。

- [ ] **Step 2: 用 MCP 复制 Worker.prefab 为 Scout.prefab 并改色（区分视觉）**

用 `manage_asset` action=duplicate：`Resources/Enemies/Worker.prefab` → `Resources/Enemies/Scout.prefab`。然后用 `manage_prefabs` 给 Scout 的 SpriteRenderer 设一个区别色（如青色，呼应"快"）。
（若 duplicate/改色在当前 MCP 能力下不顺，退而求其次：CSV 的 prefab 列指向 `Enemies/Worker` 让斥候暂用工人外观，prefab 任务标记为后续美术补，先保证逻辑可玩。本步以"斥候能在场景中生成且不报错"为完成标准。）

- [ ] **Step 3: 提交（若生成了 prefab）**

```bash
git add Babel_Client/Assets/Resources/Enemies/Scout.prefab*
git commit -m "feat(art): 斥候占位 prefab（复用 Worker 改色）"
```

### Task 10: Play 模式集成验证（console 逻辑验证，非截图）

**Files:** 无（验证任务）

- [ ] **Step 1: 把斥候加入某个波次以便实地观察**

查 `Babel_Client/Assets/Data/Waves/waves.csv`，在一个早期波次的 enemyPool 里加入 `scout`（小权重即可）。用 Write 改 CSV（UTF-8）。

- [ ] **Step 2: Play 模式 + console 验证斥候优先建 gateway**

Run（MCP）: `manage_editor` play。等待斥候生成。`execute_code` 查询：找到场景中 targetMode=scout 的 Enemy，读其 `_targetBuildPointIndex` 对应的 BuildPoint 是否 `isGateway`。例如遍历所有 Enemy，反射读其私有 `_targetSelector` 类型名 == "GatewayFirstSelector" 且 `_targetBuildPointIndex` 指向 gateway。
Expected: 斥候的预约目标是 gateway（或在 gateway 已被占时退化）。日志打印确认。

- [ ] **Step 3: Play 模式验证爬梯闭环**

`execute_code` 手动构造或观察：当某层 gateway 建好后，后续敌人在无可预约点时进入 MovingToPassage/ClimbingPassage。可用日志在 `StartMovingToPassage` 临时加 `Debug.LogWarning("[BABEL][Enemy] 开始爬梯 layer=...")`（验证后移除），或直接读敌人状态分布。
Expected: 有敌人成功爬到上层（console 可见层切换或上层出现敌人）。`manage_editor` stop。

- [ ] **Step 4: 全量 EditMode 测试最终回归**

Run（MCP）: `run_tests` assembly=`Babel.EditModeTests`。
Expected: 全绿（或仅剩与本功能无关、Phase 0 之外的已知失败——若有需如实报告）。

- [ ] **Step 5: 改 CLAUDE.md 修正 gateway 完成语义（D7）**

CLAUDE.md 当前写"When all non-gateway BuildPoints are completed, `BuildEvents.RaiseLayerCompleted` fires."。实际代码 `IsCompleted` 计入全部点（含 gateway）。改为：
"When all BuildPoints (including the gateway) are completed, `Path.IsCompleted` becomes true and `BuildEvents.RaiseLayerCompleted` fires. A built gateway also acts as a public ladder: enemies with no reservable point on the current layer will climb if `Path.IsGatewayBuilt()` and a `nextLayerPath` exists."

- [ ] **Step 6: 最终提交 + 推送**

```bash
git add Babel_Client/Assets/Data/Waves/waves.csv CLAUDE.md
git commit -m "feat(wave): 斥候编入波次 + 修正 CLAUDE.md gateway 完成语义"
git push origin master
```

---

## 验收标准（全部满足）

1. EditMode 全量测试绿（Phase 0 修复 + 所有新测试通过）。
2. `enemies.csv` 含 scout 行，中文无乱码，`EnemyDatabase.GetById("scout").TargetMode == "scout"`。
3. 斥候在场景中优先预约/占用本层 gateway；gateway 被占或已建或顶层无 gateway 时退化为普通工人。
4. 工人选点为全候选随机（无"最远一半"行为）。
5. 任意敌人在本层无可预约点、但 gateway 已建且有上层时，会爬到上层。
6. 建 gateway 的敌人不强制爬（按 charge 正常走）。
7. CLAUDE.md gateway 语义已更正。
8. 已推送至 origin/master。

## 不做的事（YAGNI）
- ❌ 不加"预约失败重抽"机制（候选预筛后不会失败）。
- ❌ 不改 charge 在爬楼时的消耗规则（爬楼本就不碰 charge）。
- ❌ 不引入层间"放行/触发上层波次"联动（超出本次范围）。
- ❌ 不把 Skill/Enemy/Wave 数据迁出 CSV。
