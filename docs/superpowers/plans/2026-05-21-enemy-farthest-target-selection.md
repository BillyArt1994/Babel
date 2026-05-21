# Enemy Farthest Target Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change enemy build target reservation so enemies randomly choose from the farthest half of eligible build points instead of always choosing the nearest point.

**Architecture:** Keep the change inside `Path.ReserveBuildPoint(Vector3 fromPos)`. `Enemy` continues to request a target from the current `Path`; `Path` filters available `BuildPoint` entries, sorts them by distance from farthest to nearest, picks from `max(1, floor(candidateCount / 2))`, marks the chosen point occupied, and returns its index.

**Tech Stack:** Unity 2022.3 C#, NUnit EditMode tests, Unity MCP test runner, `dotnet build` for C# compile validation.

---

## File Structure

- Create: `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs`
  - Focused EditMode tests for `Path.ReserveBuildPoint` target selection.
  - Uses reflection, matching the existing test style in `TransientEnemyPoolTests.cs`, because the EditMode asmdef does not directly reference `Assembly-CSharp`.
- Modify: `Babel_Client\Assets\Scripts\Game\Path.cs`
  - Replace nearest-target selection with farthest-half random selection.
  - Add a private static helper `GetFarthestSelectionCount(int candidateCount)`.
  - Add a private candidate struct and comparer so the selection logic remains local to `Path`.

Do not stage or revert unrelated current working tree changes:

- `Babel_Client\Assets\Scripts\UI\UIGamePanel.cs`
- `Babel_Client\Assets\Tests\EditMode\TransientEnemyPoolTests.cs`
- `Babel_Client\Packages\packages-lock.json`

---

### Task 1: Add failing tests for farthest-half selection

**Files:**
- Create: `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs`
- Modify: `Babel_Client\Assets\Scripts\Game\Path.cs`

- [ ] **Step 1: Write the failing test file**

Create `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs` with this exact content:

```csharp
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class PathTargetSelectionTests
    {
        private Type _pathType;
        private Type _buildPointType;
        private Component _path;
        private Component[] _buildPoints = Array.Empty<Component>();
        private GameObject _pathObject;
        private GameObject[] _buildPointObjects = Array.Empty<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_pathObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_pathObject);
            }

            for (int i = 0; i < _buildPointObjects.Length; i++)
            {
                if (_buildPointObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_buildPointObjects[i]);
                }
            }

            _path = null;
            _pathObject = null;
            _buildPoints = Array.Empty<Component>();
            _buildPointObjects = Array.Empty<GameObject>();
        }

        [Test]
        public void GetFarthestSelectionCount_UsesFloorHalfWithMinimumOne()
        {
            _pathType = RequireType("Babel.Path");

            Assert.That(GetFarthestSelectionCount(0), Is.EqualTo(0));
            Assert.That(GetFarthestSelectionCount(1), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(2), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(3), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(4), Is.EqualTo(2));
            Assert.That(GetFarthestSelectionCount(5), Is.EqualTo(2));
            Assert.That(GetFarthestSelectionCount(6), Is.EqualTo(3));
        }

        [Test]
        public void ReserveBuildPoint_WithThreeCandidates_ReservesFarthestPoint()
        {
            CreatePathWithPointXs(1f, 2f, 3f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.EqualTo(2));
            Assert.That(IsOccupied(2), Is.True);
            Assert.That(IsOccupied(0), Is.False);
            Assert.That(IsOccupied(1), Is.False);
        }

        [Test]
        public void ReserveBuildPoint_WithFourCandidates_ReservesOnlyFromFarthestHalf()
        {
            UnityEngine.Random.InitState(12345);
            CreatePathWithPointXs(1f, 2f, 3f, 4f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.AnyOf(2, 3));
            Assert.That(IsOccupied(reservedIndex), Is.True);
            Assert.That(IsOccupied(0), Is.False);
            Assert.That(IsOccupied(1), Is.False);
        }

        [Test]
        public void ReserveBuildPoint_ExcludesCompletedAndOccupiedBeforeChoosingFarthestHalf()
        {
            CreatePathWithPointXs(1f, 10f, 20f, 30f);
            CompleteBuildPoint(2);
            SetOccupied(3, true);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.EqualTo(1));
            Assert.That(IsOccupied(1), Is.True);
            Assert.That(IsOccupied(0), Is.False);
        }

        private void CreatePathWithPointXs(params float[] xPositions)
        {
            _pathType = RequireType("Babel.Path");
            _buildPointType = RequireType("Babel.BuildPoint");
            _pathObject = new GameObject("PathTargetSelectionTest_Path");
            _path = _pathObject.AddComponent(_pathType);
            _buildPoints = new Component[xPositions.Length];
            _buildPointObjects = new GameObject[xPositions.Length];

            Array wayPoints = Array.CreateInstance(_buildPointType, xPositions.Length);
            for (int i = 0; i < xPositions.Length; i++)
            {
                _buildPointObjects[i] = new GameObject($"PathTargetSelectionTest_BuildPoint_{i}");
                _buildPointObjects[i].transform.position = new Vector3(xPositions[i], 0f, 0f);
                Component buildPoint = _buildPointObjects[i].AddComponent(_buildPointType);
                _buildPoints[i] = buildPoint;
                wayPoints.SetValue(buildPoint, i);
            }

            FieldInfo wayPointListField = _pathType.GetField("wayPointList", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(wayPointListField, Is.Not.Null, "Path.wayPointList should remain a public field.");
            wayPointListField.SetValue(_path, wayPoints);
        }

        private int ReserveFrom(Vector3 fromPosition)
        {
            MethodInfo method = _pathType.GetMethod("ReserveBuildPoint", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Path.ReserveBuildPoint should remain public.");
            return (int)method.Invoke(_path, new object[] { fromPosition });
        }

        private int GetFarthestSelectionCount(int candidateCount)
        {
            MethodInfo method = _pathType.GetMethod(
                "GetFarthestSelectionCount",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Path should expose a private testable farthest-half count helper.");
            return (int)method.Invoke(null, new object[] { candidateCount });
        }

        private bool IsOccupied(int index)
        {
            PropertyInfo property = _buildPointType.GetProperty("IsOccupied", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "BuildPoint.IsOccupied should remain public.");
            return (bool)property.GetValue(_buildPoints[index]);
        }

        private void SetOccupied(int index, bool occupied)
        {
            MethodInfo method = _buildPointType.GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "BuildPoint.SetOccupied should remain public.");
            method.Invoke(_buildPoints[index], new object[] { occupied });
        }

        private void CompleteBuildPoint(int index)
        {
            MethodInfo method = _buildPointType.GetMethod("AddBuildProgress", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "BuildPoint.AddBuildProgress should remain public.");
            method.Invoke(_buildPoints[index], new object[] { 9999 });
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
            return type;
        }
    }
}
```

- [ ] **Step 2: Refresh Unity and run the new test to verify RED**

Use Unity MCP:

```text
unityMCP-refresh_unity:
  mode: force
  scope: scripts
  compile: request
  wait_for_ready: true

unityMCP-run_tests:
  mode: EditMode
  test_names:
    - Babel.Tests.PathTargetSelectionTests.GetFarthestSelectionCount_UsesFloorHalfWithMinimumOne
  include_failed_tests: true
  include_details: true
```

Expected result: FAIL with `Path should expose a private testable farthest-half count helper.`

Then run the behavior test:

```text
unityMCP-run_tests:
  mode: EditMode
  test_names:
    - Babel.Tests.PathTargetSelectionTests.ReserveBuildPoint_WithThreeCandidates_ReservesFarthestPoint
  include_failed_tests: true
  include_details: true
```

Expected result: FAIL because current `Path.ReserveBuildPoint` returns index `0` instead of farthest index `2`.

---

### Task 2: Implement farthest-half target reservation in Path

**Files:**
- Modify: `Babel_Client\Assets\Scripts\Game\Path.cs`
- Test: `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs`

- [ ] **Step 1: Add collection support at the top of Path.cs**

Change the first line of `Babel_Client\Assets\Scripts\Game\Path.cs` from:

```csharp
using UnityEngine;
```

to:

```csharp
using System.Collections.Generic;
using UnityEngine;
```

- [ ] **Step 2: Add candidate storage fields inside `public class Path`**

Insert these fields after the existing `_completedCount` field:

```csharp
        private readonly List<BuildPointCandidate> _reserveCandidates = new List<BuildPointCandidate>(16);
        private static readonly BuildPointCandidateDistanceComparer CandidateDistanceComparer =
            new BuildPointCandidateDistanceComparer();
```

- [ ] **Step 3: Replace `ReserveBuildPoint` with farthest-half random selection**

Replace the existing `ReserveBuildPoint(Vector3 fromPos)` method with:

```csharp
        public int ReserveBuildPoint(Vector3 fromPos)
        {
            _reserveCandidates.Clear();
            for (int i = 0; i < wayPointList.Length; i++)
            {
                BuildPoint point = wayPointList[i];
                if (point.IsBuildCompleted) continue;
                if (point.IsOccupied) continue;

                float distance = Vector3.Distance(point.transform.position, fromPos);
                _reserveCandidates.Add(new BuildPointCandidate(i, distance));
            }

            if (_reserveCandidates.Count <= 0)
            {
                return -1;
            }

            _reserveCandidates.Sort(CandidateDistanceComparer);
            int selectableCount = GetFarthestSelectionCount(_reserveCandidates.Count);
            int selectedCandidateIndex = UnityEngine.Random.Range(0, selectableCount);
            int selectedBuildPointIndex = _reserveCandidates[selectedCandidateIndex].Index;
            wayPointList[selectedBuildPointIndex].SetOccupied(true);
            return selectedBuildPointIndex;
        }
```

- [ ] **Step 4: Add the helper types before `OnDrawGizmos`**

Insert this code immediately before `private void OnDrawGizmos()`:

```csharp
        private static int GetFarthestSelectionCount(int candidateCount)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, candidateCount / 2);
        }

        private readonly struct BuildPointCandidate
        {
            public BuildPointCandidate(int index, float distance)
            {
                Index = index;
                Distance = distance;
            }

            public readonly int Index;
            public readonly float Distance;
        }

        private sealed class BuildPointCandidateDistanceComparer : IComparer<BuildPointCandidate>
        {
            public int Compare(BuildPointCandidate left, BuildPointCandidate right)
            {
                return right.Distance.CompareTo(left.Distance);
            }
        }
```

- [ ] **Step 5: Refresh Unity and run the targeted tests to verify GREEN**

Use Unity MCP:

```text
unityMCP-refresh_unity:
  mode: force
  scope: scripts
  compile: request
  wait_for_ready: true

unityMCP-run_tests:
  mode: EditMode
  test_names:
    - Babel.Tests.PathTargetSelectionTests.GetFarthestSelectionCount_UsesFloorHalfWithMinimumOne
    - Babel.Tests.PathTargetSelectionTests.ReserveBuildPoint_WithThreeCandidates_ReservesFarthestPoint
    - Babel.Tests.PathTargetSelectionTests.ReserveBuildPoint_WithFourCandidates_ReservesOnlyFromFarthestHalf
    - Babel.Tests.PathTargetSelectionTests.ReserveBuildPoint_ExcludesCompletedAndOccupiedBeforeChoosingFarthestHalf
  include_failed_tests: true
  include_details: true
```

Expected result: all four targeted tests PASS.

---

### Task 3: Validate full project behavior and commit

**Files:**
- Modify: `Babel_Client\Assets\Scripts\Game\Path.cs`
- Create: `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs`

- [ ] **Step 1: Run the full EditMode suite**

Use Unity MCP:

```text
unityMCP-run_tests:
  mode: EditMode
  include_failed_tests: true
  include_details: false
```

Expected result: all EditMode tests PASS. The current suite should include the new `PathTargetSelectionTests`.

- [ ] **Step 2: Build the C# project**

Run from repository root:

```powershell
dotnet build .\Babel_Client\Assembly-CSharp.csproj --nologo --verbosity minimal
```

Expected result: command exits with code `0`.

- [ ] **Step 3: Validate Play Mode target selection**

Use Unity MCP to enter Play Mode:

```text
unityMCP-manage_editor:
  action: play
```

Wait until enemies spawn. Then execute this code through Unity MCP:

```csharp
var enemies = UnityEngine.Object.FindObjectsOfType<Babel.Enemy>();
return "enemy_count=" + enemies.Length + ", timeScale=" + UnityEngine.Time.timeScale;
```

Expected result: `enemy_count` is greater than `0`. Visually, enemies should spend more time walking toward farther build points before beginning construction.

Exit Play Mode:

```text
unityMCP-manage_editor:
  action: stop
```

- [ ] **Step 4: Review and stage only target-selection files**

Run:

```powershell
git --no-pager diff -- Babel_Client\Assets\Scripts\Game\Path.cs Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs
git --no-pager status --short
```

Expected result:

- The `Path.cs` diff only changes target reservation internals.
- The test diff only creates `PathTargetSelectionTests.cs`.
- Existing unrelated changes in `UIGamePanel.cs`, `TransientEnemyPoolTests.cs`, and `packages-lock.json` remain unstaged.

Stage only the target-selection files:

```powershell
git add Babel_Client\Assets\Scripts\Game\Path.cs Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs
```

If Unity created `Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs.meta`, stage it too:

```powershell
if (Test-Path .\Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs.meta) { git add .\Babel_Client\Assets\Tests\EditMode\PathTargetSelectionTests.cs.meta }
```

- [ ] **Step 5: Commit the implementation**

Run:

```powershell
git commit -m "feat: prefer farthest build targets" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

Expected result: one commit containing only `Path.cs`, `PathTargetSelectionTests.cs`, and the new test `.meta` file if Unity generated it.

---

## Self-Review

- Spec coverage: Task 1 covers candidate count, three-candidate farthest selection, four-candidate farthest-half selection, and exclusion of completed/occupied points. Task 2 implements the behavior inside `Path` only. Task 3 validates tests, build, Play Mode, and staging boundaries.
- Red-flag scan: No vague tasks, missing commands, or deferred edge cases remain.
- Type consistency: The plan uses existing `Path.ReserveBuildPoint(Vector3)`, `BuildPoint.IsBuildCompleted`, `BuildPoint.IsOccupied`, `BuildPoint.SetOccupied(bool)`, and `BuildPoint.AddBuildProgress(int)` members exactly as they exist today.
