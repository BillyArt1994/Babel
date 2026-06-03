# Skill Level Upgrade System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让升级三选一界面能同时出现"新技能"和"升级已有技能等级"两种选项，升级时就地刷新数值而不重建技能对象，并支持"零值禁用"——某效果在低等级数值为 0，升级后数值变为非零即解锁。

**Architecture:**
`SkillConfig` 新增 `MaxLevel`(int) 字段供 CSV 读取。`SkillSystem` 新增 `UpgradeSkill(skillId)` 方法，就地替换同技能的 SkillConfig 并刷新已有 Skill 对象的 config 引用（不销毁/重建 Trigger，保留冷却状态）。`UpgradeSystem.BuildEligiblePool` 在"新技能池"之外，再把"可升级的已装备技能"加入候选池，两类选项混在三选一里。`SkillDatabase` 新增 `GetNextLevel(skillId)` 按 SkillId + Level 升序查下一级配置。CSV 每个可升级技能按 level 1/2/3 存多行（skillId 相同，level 不同），`upgradesFrom` 列留空（不同于 meteor_evolved 那种进化）。

**Tech Stack:** C# / Unity 2022.3 / NUnit EditMode Tests / skills.csv

---

## File Map

| 文件 | 变更类型 | 改什么 |
|------|---------|--------|
| `Assets/Scripts/Skill/SkillConfig.cs` | Modify | 新增 `MaxLevel` 字段 |
| `Assets/Scripts/Skill/SkillParser.cs` | Modify | 解析 `maxLevel` 列 |
| `Assets/Scripts/Skill/SkillDatabase.cs` | Modify | 新增 `GetNextLevel(skillId, currentLevel)` |
| `Assets/Scripts/Game/SkillSystem.cs` | Modify | 新增 `UpgradeSkill(skillId)` + `CanUpgradeSkill(skillId)` |
| `Assets/Scripts/Game/UpgradeSystem.cs` | Modify | `BuildEligiblePool` 加入可升级技能；`SelectOption` 处理升级分支 |
| `Assets/Data/Skills/skills.csv` | Modify | meteor 拆成 level1/level2 两行；新增 `maxLevel` 列 |
| `Assets/Tests/EditMode/UpgradeSystemTests.cs` | Modify | 追加多级升级相关测试 |

---

## Task 1：SkillConfig 加 MaxLevel 字段

**Files:**
- Modify: `Assets/Scripts/Skill/SkillConfig.cs`

- [ ] **Step 1: 在 SkillConfig 中添加 MaxLevel 字段**

在 `Level = 1` 字段下方添加：

```csharp
/// <summary>该技能最大等级（默认 1 = 不可升级）</summary>
public int MaxLevel = 1;
```

- [ ] **Step 2: 编译验证**

在 Unity Editor 中等待编译，或在 `UpgradeSystemTests.cs` 中添加最简访问验证后运行：
```
Window > General > Test Runner > EditMode > Run All
```
Expected: 0 errors，现有所有测试 PASS。

- [ ] **Step 3: Commit**

```bash
git add Babel_Client/Assets/Scripts/Skill/SkillConfig.cs
git commit -m "feat(skill): add MaxLevel field to SkillConfig"
```

---

## Task 2：SkillParser 解析 maxLevel 列

**Files:**
- Modify: `Assets/Scripts/Skill/SkillParser.cs`

- [ ] **Step 1: 在 CreateSkillConfig 中读取 maxLevel**

找到 `CreateSkillConfig` 方法（约第 149 行），在 `Level = GetInt(fields, map, "level")` 下一行追加：

```csharp
MaxLevel = GetInt(fields, map, "maxLevel"),
```

完整的 `CreateSkillConfig` 赋值块变为：

```csharp
private static SkillConfig CreateSkillConfig(string[] fields, Dictionary<string, int> map)
{
    var config = new SkillConfig
    {
        SkillId = GetString(fields, map, "skillId"),
        SkillName = GetString(fields, map, "skillName"),
        Description = GetString(fields, map, "description"),
        IconPath = GetString(fields, map, "iconPath"),
        TriggerType = GetString(fields, map, "triggerType"),
        Cooldown = GetFloat(fields, map, "cooldown"),
        ChargeTime = GetFloat(fields, map, "chargeTime"),
        Interval = GetFloat(fields, map, "interval"),
        Chance = GetFloat(fields, map, "chance"),
        Level = GetInt(fields, map, "level"),
        MaxLevel = GetInt(fields, map, "maxLevel"),
        IsStarterSkill = GetBool(fields, map, "isStarterSkill"),
        UpgradesFrom = GetString(fields, map, "upgradesFrom")
    };
    config.Weight = ResolveWeight(fields, map, config.SkillId);
    return config;
}
```

> 注意：`GetInt` 在列不存在时已返回 0；`MaxLevel = 0` 的行为等同于 1（不可升级）——后续 `CanUpgradeSkill` 中用 `maxLevel <= 1` 判断。

- [ ] **Step 2: 编译 + 运行现有测试**

Expected: 0 errors，现有所有测试 PASS。

- [ ] **Step 3: Commit**

```bash
git add Babel_Client/Assets/Scripts/Skill/SkillParser.cs
git commit -m "feat(skill): parse maxLevel column in SkillParser"
```

---

## Task 3：SkillDatabase 加 GetNextLevel

**Files:**
- Modify: `Assets/Scripts/Skill/SkillDatabase.cs`

- [ ] **Step 1: 阅读 SkillDatabase 现有结构**

确认 `_byId` 和 `_allSkills` 字段存在，`GetById(string skillId)` 返回第一个匹配的 SkillConfig。

- [ ] **Step 2: 添加 GetNextLevel 方法**

在 `SkillDatabase` 类末尾（`}` 前）追加：

```csharp
/// <summary>
/// 返回同 skillId 的下一等级配置（level = currentLevel + 1）。
/// 找不到时返回 null（已满级或未配置多级）。
/// </summary>
public static SkillConfig GetNextLevel(string skillId, int currentLevel)
{
    if (!_initialized) return null;
    int targetLevel = currentLevel + 1;
    for (int i = 0; i < _allSkills.Count; i++)
    {
        if (_allSkills[i].SkillId == skillId && _allSkills[i].Level == targetLevel)
            return _allSkills[i];
    }
    return null;
}
```

- [ ] **Step 3: 写测试（新增到 UpgradeSystemTests.cs）**

在 `UpgradeSystemTests` 类末尾添加：

```csharp
[Test]
public void GetNextLevel_WhenNextLevelExists_ReturnsCorrectConfig()
{
    Type dbType = RequireType("Babel.SkillDatabase");
    dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
          .Invoke(null, new object[] { SkillsCsvText });

    // meteor level 1 → level 2（需要 CSV 先有 meteor level 2 行，Task 5 补充后此测试才真正 PASS）
    var getNextLevel = dbType.GetMethod("GetNextLevel", BindingFlags.Public | BindingFlags.Static);
    Assert.That(getNextLevel, Is.Not.Null, "GetNextLevel method should exist");
}

[Test]
public void GetNextLevel_WhenAtMaxLevel_ReturnsNull()
{
    Type dbType = RequireType("Babel.SkillDatabase");
    dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
          .Invoke(null, new object[] { SkillsCsvText });

    var getNextLevel = dbType.GetMethod("GetNextLevel", BindingFlags.Public | BindingFlags.Static);
    // divine_finger 只有一级，传 level=1 → 应返回 null
    object result = getNextLevel.Invoke(null, new object[] { "divine_finger", 1 });
    Assert.That(result, Is.Null, "divine_finger has no level 2, should return null");
}
```

- [ ] **Step 4: 运行测试**

Expected: `GetNextLevel_WhenAtMaxLevel_ReturnsNull` PASS；`GetNextLevel_WhenNextLevelExists_ReturnsCorrectConfig` PASS（仅验证方法存在）。

- [ ] **Step 5: Commit**

```bash
git add Babel_Client/Assets/Scripts/Skill/SkillDatabase.cs \
        Babel_Client/Assets/Tests/EditMode/UpgradeSystemTests.cs
git commit -m "feat(skill): add GetNextLevel to SkillDatabase"
```

---

## Task 4：SkillSystem 加 UpgradeSkill / CanUpgradeSkill

**Files:**
- Modify: `Assets/Scripts/Game/SkillSystem.cs`

- [ ] **Step 1: 添加 CanUpgradeSkill 方法**

在 `HasSkill` 方法后追加：

```csharp
/// <summary>
/// 当前是否可以升级指定技能（已装备且未满级）。
/// </summary>
public bool CanUpgradeSkill(string skillId)
{
    for (int i = 0; i < _skills.Count; i++)
    {
        if (_skills[i].Config.SkillId == skillId)
        {
            SkillConfig cfg = _skills[i].Config;
            int maxLevel = cfg.MaxLevel <= 0 ? 1 : cfg.MaxLevel;
            return cfg.Level < maxLevel;
        }
    }
    return false;
}
```

- [ ] **Step 2: 添加 UpgradeSkill 方法**

在 `CanUpgradeSkill` 方法后追加：

```csharp
/// <summary>
/// 就地升级已装备的指定技能：用下一级 SkillConfig 替换 Config 引用，
/// 不销毁 Trigger，保留冷却状态。
/// </summary>
/// <param name="nextConfig">下一级技能配置（由调用方通过 SkillDatabase.GetNextLevel 获取）。</param>
public void UpgradeSkill(SkillConfig nextConfig)
{
    if (nextConfig == null)
    {
        Debug.LogWarning("[BABEL][SkillSystem] UpgradeSkill: nextConfig is null");
        return;
    }
    for (int i = 0; i < _skills.Count; i++)
    {
        if (_skills[i].Config.SkillId == nextConfig.SkillId)
        {
            _skills[i].UpdateConfig(nextConfig);
            RaiseEquippedSkillsChanged();
            BabelLogger.Log("SkillSystem", $"Upgraded {nextConfig.SkillId} to level {nextConfig.Level}");
            return;
        }
    }
    Debug.LogWarning($"[BABEL][SkillSystem] UpgradeSkill: skill '{nextConfig.SkillId}' not found");
}
```

- [ ] **Step 3: 给 Skill 类添加 UpdateConfig 方法**

打开 `Assets/Scripts/Skill/Skill.cs`，在类中追加：

```csharp
/// <summary>就地替换 Config，Trigger 和 Effect 对象保持不变（冷却状态不丢失）。</summary>
public void UpdateConfig(SkillConfig newConfig)
{
    Config = newConfig;
}
```

同时把 `Config` 属性改为可设置（或直接改为字段）。当前 `Skill.cs` 若 `Config` 是只读属性：

```csharp
// 原来
public SkillConfig Config { get; }
// 改为
public SkillConfig Config { get; private set; }
```

- [ ] **Step 4: 写测试**

在 `UpgradeSystemTests.cs` 末尾追加：

```csharp
[Test]
public void CanUpgradeSkill_WhenSkillAtMaxLevel_ReturnsFalse()
{
    Type skillSystemType = RequireType("Babel.SkillSystem");
    Type dbType = RequireType("Babel.SkillDatabase");
    dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
          .Invoke(null, new object[] { SkillsCsvText });

    var obj = new GameObject("SkillSystemCanUpgradeTest");
    try
    {
        Component system = obj.AddComponent(skillSystemType);
        InvokePrivateStart(system);
        // divine_finger maxLevel=1（默认），装备后应不可升级
        var canUpgrade = skillSystemType.GetMethod("CanUpgradeSkill",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(canUpgrade, Is.Not.Null);
        bool result = (bool)canUpgrade.Invoke(system, new object[] { "divine_finger" });
        Assert.That(result, Is.False, "divine_finger has maxLevel=1, cannot upgrade");
    }
    finally { UnityEngine.Object.DestroyImmediate(obj); }
}
```

- [ ] **Step 5: 运行测试**

Expected: `CanUpgradeSkill_WhenSkillAtMaxLevel_ReturnsFalse` PASS，所有现有测试 PASS。

- [ ] **Step 6: Commit**

```bash
git add Babel_Client/Assets/Scripts/Game/SkillSystem.cs \
        Babel_Client/Assets/Scripts/Skill/Skill.cs \
        Babel_Client/Assets/Tests/EditMode/UpgradeSystemTests.cs
git commit -m "feat(skill): add CanUpgradeSkill and UpgradeSkill to SkillSystem"
```

---

## Task 5：skills.csv — 给 meteor 加 level2，添加 maxLevel 列

**Files:**
- Modify: `Assets/Data/Skills/skills.csv`

> ⚠️ CSV 必须用 **UTF-8 编码**保存（用 VS Code 或记事本另存为 UTF-8）。Excel 默认 GBK 会导致乱码。

- [ ] **Step 1: 在 CSV 标题行加 maxLevel 列**

在 `level` 列之后（`weight` 列之前）加一列 `maxLevel`：

```
skillId,...,level,maxLevel,weight,isStarterSkill,upgradesFrom
```

- [ ] **Step 2: 给所有现有行补充 maxLevel 值**

- `divine_finger`：`maxLevel=1`（不可升级）
- `meteor`：`maxLevel=2`（可升到 2 级）
- `thunder_auto`、`aftershock`、`plague`、`rage`：`maxLevel=1`
- `meteor_evolved`：`maxLevel=1`（进化路线暂时保留，独立行）
- `berserker_pact`：`maxLevel=1`

- [ ] **Step 3: 在 meteor 行下方新增 meteor level2 行**

level1 行（现有）：
```
meteor,天降陨石,陨石从天而降,Icons/meteor,OnClick,2.5,1,,, hit_aoe,150,,3,,,,,,,,,,,,,,,,,,,,,1,2,1,1,TRUE,
```

新增 level2 行（伤害提升、半径扩大，dot_aoe dps=0 占位为未来着火预留）：
```
meteor,天降陨石·强化,强化的陨石，伤害与范围提升,Icons/meteor,OnClick,2.5,1,,,hit_aoe,200,,3.5,,,,,dot_aoe,,,3.5,0,3,,,,,,,,,,,2,2,0.8,0,FALSE,
```

> 关键字段说明：
> - `level=2`，`maxLevel=2`，`weight=0`（已解锁时不再出现在"新技能池"），`isStarterSkill=FALSE`
> - `dot_aoe dps=0`：着火效果占位，数值为 0 相当于禁用，升级后可改为非零"解锁"效果
> - `upgradesFrom` 留空——这不是进化，是同技能升级

- [ ] **Step 4: 运行现有测试（包括 GetNextLevel 测试）**

Expected: `GetNextLevel_WhenNextLevelExists_ReturnsCorrectConfig` 现在应完全 PASS（能找到 meteor level2）。

- [ ] **Step 5: Commit**

```bash
git add Babel_Client/Assets/Data/Skills/skills.csv
git commit -m "feat(skill): add maxLevel column and meteor level2 to skills.csv"
```

---

## Task 6：UpgradeSystem — 三选一混入升级选项

**Files:**
- Modify: `Assets/Scripts/Game/UpgradeSystem.cs`

这是核心改动。`BuildEligiblePool` 分两步：先收集可选新技能，再收集可升级的已装备技能，合并成候选池。`SelectOption` 识别"升级分支"并调用 `SkillSystem.UpgradeSkill`。

- [ ] **Step 1: 定义升级选项包装类型**

在 `UpgradeSystem.cs` 文件顶部（`namespace Babel` 内，`UpgradeSystem` 类外）添加：

```csharp
/// <summary>升级三选一的单个选项：新技能或升级已有技能。</summary>
public class UpgradeOption
{
    public enum OptionType { NewSkill, LevelUpgrade }
    public OptionType Type;
    public SkillConfig Config; // NewSkill → 新技能配置；LevelUpgrade → 下一级配置
}
```

- [ ] **Step 2: 把 _pendingOptions 类型从 SkillConfig 改为 UpgradeOption**

将 `UpgradeSystem` 类中：

```csharp
// 原来
private readonly List<SkillConfig> _pendingOptions = new();
// 改为
private readonly List<UpgradeOption> _pendingOptions = new();
```

- [ ] **Step 3: 更新 GenerateOptions 返回 UpgradeOption**

将 `GenerateOptions` 方法替换为：

```csharp
private IReadOnlyList<UpgradeOption> GenerateOptions(int count)
{
    var pool = BuildEligiblePool();
    var selected = new List<UpgradeOption>(Mathf.Min(count, pool.Count));
    while (selected.Count < count && pool.Count > 0)
    {
        int index = RollWeightedIndex(pool);
        selected.Add(pool[index]);
        pool.RemoveAt(index);
    }
    return selected;
}
```

- [ ] **Step 4: 更新 BuildEligiblePool 返回 UpgradeOption**

将 `BuildEligiblePool` 方法替换为：

```csharp
private List<UpgradeOption> BuildEligiblePool()
{
    var pool = new List<UpgradeOption>();

    // — 新技能 —
    IReadOnlyList<SkillConfig> allSkills = SkillDatabase.GetAll();
    for (int i = 0; i < allSkills.Count; i++)
    {
        if (IsEligibleNewSkill(allSkills[i]))
            pool.Add(new UpgradeOption { Type = UpgradeOption.OptionType.NewSkill, Config = allSkills[i] });
    }

    // — 升级已有技能 —
    if (skillSystem != null)
    {
        IReadOnlyList<Skill> equipped = skillSystem.GetEquippedSkills();
        for (int i = 0; i < equipped.Count; i++)
        {
            SkillConfig current = equipped[i].Config;
            if (!skillSystem.CanUpgradeSkill(current.SkillId)) continue;
            SkillConfig next = SkillDatabase.GetNextLevel(current.SkillId, current.Level);
            if (next != null)
                pool.Add(new UpgradeOption { Type = UpgradeOption.OptionType.LevelUpgrade, Config = next });
        }
    }

    return pool;
}
```

- [ ] **Step 5: 将 IsEligible 重命名为 IsEligibleNewSkill**

将 `IsEligible` 方法名改为 `IsEligibleNewSkill`（只用于新技能判断）：

```csharp
private bool IsEligibleNewSkill(SkillConfig config)
{
    if (config == null || config.Weight <= 0f)
        return false;
    if (skillSystem != null && skillSystem.HasSkill(config.SkillId))
        return false;
    if (!string.IsNullOrEmpty(config.UpgradesFrom) &&
        (skillSystem == null || !skillSystem.HasSkill(config.UpgradesFrom)))
        return false;
    return true;
}
```

- [ ] **Step 6: 更新 RollWeightedIndex 接受 UpgradeOption 列表**

```csharp
private static int RollWeightedIndex(IReadOnlyList<UpgradeOption> pool)
{
    float totalWeight = 0f;
    for (int i = 0; i < pool.Count; i++)
        totalWeight += Mathf.Max(0f, pool[i].Config.Weight);

    float roll = UnityEngine.Random.Range(0f, totalWeight);
    float cumulative = 0f;
    for (int i = 0; i < pool.Count; i++)
    {
        cumulative += Mathf.Max(0f, pool[i].Config.Weight);
        if (roll <= cumulative) return i;
    }
    return pool.Count - 1;
}
```

- [ ] **Step 7: 更新 SelectOption 处理升级分支**

将 `SelectOption` 方法替换为：

```csharp
public void SelectOption(int index)
{
    if (index < 0 || index >= _pendingOptions.Count)
    {
        Debug.LogWarning($"[BABEL][UpgradeSystem] Invalid upgrade option index {index}");
        return;
    }
    if (skillSystem == null)
    {
        Debug.LogWarning("[BABEL][UpgradeSystem] No SkillSystem assigned");
        return;
    }

    UpgradeOption selected = _pendingOptions[index];
    if (selected.Type == UpgradeOption.OptionType.LevelUpgrade)
    {
        skillSystem.UpgradeSkill(selected.Config);
    }
    else
    {
        skillSystem.AddOrReplaceSkill(selected.Config);
    }

    skillSystem.ResetClickCooldowns();
    _pendingOptions.Clear();
    Time.timeScale = 1f;
    UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
}
```

- [ ] **Step 8: 更新 OnExpChanged 中 _pendingOptions.Add 的调用**

`OnExpChanged` 里 `_pendingOptions.Add(options[i])` 的 `options` 类型已是 `UpgradeOption`，无需改动（只需确认编译通过）。

- [ ] **Step 9: 更新测试辅助方法签名**

`UpgradeSystemTests.cs` 中 `SetPendingOptionsForTests` 接受 `IReadOnlyList<SkillConfig>`——类型已变。将方法改为接受 `IReadOnlyList<UpgradeOption>`，或者在测试里包装一下。最简单改法：

```csharp
public void SetPendingOptionsForTests(IReadOnlyList<UpgradeOption> options)
{
    _pendingOptions.Clear();
    if (options == null) return;
    for (int i = 0; i < options.Count; i++)
        _pendingOptions.Add(options[i]);
}
```

同时更新 `PendingOptionCountForTests` 仍返回 `int`，无需改动。

- [ ] **Step 10: 更新 GenerateOptionsForTests 返回类型**

```csharp
public IReadOnlyList<UpgradeOption> GenerateOptionsForTests(int count)
{
    return GenerateOptions(count);
}
```

- [ ] **Step 11: 编译 + 运行所有测试**

Expected: 0 errors，所有现有测试 PASS。

- [ ] **Step 12: Commit**

```bash
git add Babel_Client/Assets/Scripts/Game/UpgradeSystem.cs \
        Babel_Client/Assets/Tests/EditMode/UpgradeSystemTests.cs
git commit -m "feat(upgrade): mix LevelUpgrade options into three-choice pool"
```

---

## Task 7：补充升级集成测试

**Files:**
- Modify: `Assets/Tests/EditMode/UpgradeSystemTests.cs`

- [ ] **Step 1: 添加升级路径集成测试**

在 `UpgradeSystemTests` 末尾追加：

```csharp
[Test]
public void UpgradeSkill_WhenMeteorLevel1Equipped_UpgradesToLevel2WithoutResettingTrigger()
{
    Type dbType = RequireType("Babel.SkillDatabase");
    Type skillSystemType = RequireType("Babel.SkillSystem");
    dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
          .Invoke(null, new object[] { SkillsCsvText });

    var obj = new GameObject("UpgradeIntegrationTest");
    try
    {
        Component system = obj.AddComponent(skillSystemType);
        InvokePrivateStart(system);

        // 1. 装备 meteor level1
        var addOrReplace = skillSystemType.GetMethod("AddOrReplaceSkill", BindingFlags.Public | BindingFlags.Instance);
        var getById = dbType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static);
        addOrReplace.Invoke(system, new[] { getById.Invoke(null, new object[] { "meteor" }) });

        // 2. 获取 level2 配置
        var getNextLevel = dbType.GetMethod("GetNextLevel", BindingFlags.Public | BindingFlags.Static);
        object level2Config = getNextLevel.Invoke(null, new object[] { "meteor", 1 });
        Assert.That(level2Config, Is.Not.Null, "meteor level2 must exist in CSV");

        // 3. 调用 UpgradeSkill
        var upgradeSkill = skillSystemType.GetMethod("UpgradeSkill", BindingFlags.Public | BindingFlags.Instance);
        upgradeSkill.Invoke(system, new[] { level2Config });

        // 4. 验证：HasSkill("meteor") 仍为 true，装备数量不变
        var hasSkill = skillSystemType.GetMethod("HasSkill", BindingFlags.Public | BindingFlags.Instance);
        Assert.That((bool)hasSkill.Invoke(system, new object[] { "meteor" }), Is.True);

        var getEquipped = skillSystemType.GetMethod("GetEquippedSkills", BindingFlags.Public | BindingFlags.Instance);
        var equipped = (System.Collections.IList)getEquipped.Invoke(system, null);
        Assert.That(equipped.Count, Is.EqualTo(2), "Should still have 2 skills: divine_finger + meteor");
    }
    finally { UnityEngine.Object.DestroyImmediate(obj); }
}

[Test]
public void BuildEligiblePool_WhenMeteorLevel1Equipped_ContainsLevelUpgradeOption()
{
    Type dbType = RequireType("Babel.SkillDatabase");
    Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
    Type skillSystemType = RequireType("Babel.SkillSystem");
    dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
          .Invoke(null, new object[] { SkillsCsvText });

    var upgradeObj = new GameObject("UpgradeSystemPoolTest");
    var skillObj = new GameObject("SkillSystemPoolTest");
    try
    {
        Component skillSys = skillObj.AddComponent(skillSystemType);
        InvokePrivateStart(skillSys);

        // 装备 meteor level1
        var addOrReplace = skillSystemType.GetMethod("AddOrReplaceSkill", BindingFlags.Public | BindingFlags.Instance);
        var getById = dbType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static);
        addOrReplace.Invoke(skillSys, new[] { getById.Invoke(null, new object[] { "meteor" }) });

        Component upgradeSys = upgradeObj.AddComponent(upgradeSystemType);
        var setSkillSystem = upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance);
        setSkillSystem.Invoke(upgradeSys, new[] { skillSys });

        var generateOptions = upgradeSystemType.GetMethod("GenerateOptionsForTests", BindingFlags.Public | BindingFlags.Instance);
        var options = (System.Collections.IList)generateOptions.Invoke(upgradeSys, new object[] { 3 });

        bool hasLevelUpgrade = false;
        foreach (var opt in options)
        {
            var typeField = opt.GetType().GetField("Type");
            var typeName = typeField.GetValue(opt).ToString();
            if (typeName == "LevelUpgrade") { hasLevelUpgrade = true; break; }
        }
        Assert.That(hasLevelUpgrade, Is.True, "Pool should contain LevelUpgrade option for meteor");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(upgradeObj);
        UnityEngine.Object.DestroyImmediate(skillObj);
    }
}
```

- [ ] **Step 2: 运行所有测试**

Expected: 全部 PASS。

- [ ] **Step 3: Commit**

```bash
git add Babel_Client/Assets/Tests/EditMode/UpgradeSystemTests.cs
git commit -m "test(upgrade): add level upgrade integration tests"
```

---

## 自检：规格覆盖

| 需求 | 覆盖 Task |
|------|---------|
| 三选一能出现"升级已有技能" | Task 6 |
| 升级不重建 Trigger，冷却不丢 | Task 4（UpgradeSkill/UpdateConfig） |
| 零值禁用（某效果 dps=0 视为禁用） | Task 5（CSV meteor level2 的 dot_aoe dps=0） |
| maxLevel 字段 CSV 可配置 | Task 1、2 |
| GetNextLevel 查下一级 | Task 3 |
| 选项权重：level2 行 weight=0 不进新技能池 | Task 5（CSV weight=0） |
| 测试覆盖核心路径 | Task 3、4、7 |
