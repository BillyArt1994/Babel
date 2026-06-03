using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Babel.Tests
{
    public class UpgradeSystemTests
    {
        private static string SkillsCsvText
        {
            get
            {
                TextAsset skillsCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Skills/skills.csv");
                Assert.That(skillsCsv, Is.Not.Null, "Test fixture requires the production skills CSV.");
                return skillsCsv.text;
            }
        }

        [Test]
        public void AddOrReplaceSkill_WhenNewSkillIsOnClick_RemovesExistingOnClickSkill()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var obj = new GameObject("SkillSystemUpgradeTest");

            try
            {
                Component system = obj.AddComponent(skillSystemType);
                InvokePrivateStart(system);
                MethodInfo addOrReplaceMethod = skillSystemType.GetMethod(
                    "AddOrReplaceSkill",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getByIdMethod = skillDatabaseType.GetMethod(
                    "GetById",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo hasSkillMethod = skillSystemType.GetMethod(
                    "HasSkill",
                    BindingFlags.Public | BindingFlags.Instance);

                Assert.That(addOrReplaceMethod, Is.Not.Null);
                Assert.That(hasSkillMethod, Is.Not.Null);
                addOrReplaceMethod.Invoke(system, new[] { getByIdMethod.Invoke(null, new object[] { "meteor" }) });

                Assert.That((bool)hasSkillMethod.Invoke(system, new object[] { "meteor" }), Is.True);
                Assert.That((bool)hasSkillMethod.Invoke(system, new object[] { "divine_finger" }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void AddOrReplaceSkill_WhenPassiveSkillSelected_KeepsCurrentClickSkill()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var obj = new GameObject("SkillSystemPassiveUpgradeTest");

            try
            {
                Component system = obj.AddComponent(skillSystemType);
                InvokePrivateStart(system);
                MethodInfo addOrReplaceMethod = skillSystemType.GetMethod(
                    "AddOrReplaceSkill",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getByIdMethod = skillDatabaseType.GetMethod(
                    "GetById",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo hasSkillMethod = skillSystemType.GetMethod(
                    "HasSkill",
                    BindingFlags.Public | BindingFlags.Instance);

                Assert.That(addOrReplaceMethod, Is.Not.Null);
                Assert.That(hasSkillMethod, Is.Not.Null);
                addOrReplaceMethod.Invoke(system, new[] { getByIdMethod.Invoke(null, new object[] { "aftershock" }) });

                Assert.That((bool)hasSkillMethod.Invoke(system, new object[] { "divine_finger" }), Is.True);
                Assert.That((bool)hasSkillMethod.Invoke(system, new object[] { "aftershock" }), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void GenerateOptions_ReturnsUpToThreeUniqueEligibleSkills()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var skillObj = new GameObject("SkillSystemForOptions");
            var upgradeObj = new GameObject("UpgradeSystemForOptions");

            try
            {
                Component skillSystem = skillObj.AddComponent(skillSystemType);
                InvokePrivateStart(skillSystem);
                Component upgradeSystem = upgradeObj.AddComponent(upgradeSystemType);
                upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { skillSystem });

                var options = (IEnumerable)upgradeSystemType.GetMethod(
                    "GenerateOptionsForTests",
                    BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { 3 });
                List<string> skillIds = CollectSkillIds(options);

                Assert.That(skillIds.Count, Is.EqualTo(3));
                Assert.That(new HashSet<string>(skillIds).Count, Is.EqualTo(skillIds.Count));
                Assert.That(skillIds.Contains("divine_finger"), Is.False);
                Assert.That(skillIds.Contains("meteor_evolved"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skillObj);
                UnityEngine.Object.DestroyImmediate(upgradeObj);
            }
        }

        [Test]
        public void SelectOption_AppliesChosenSkillClearsPendingAndResumesTime()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
            Type upgradeOptionType = RequireType("Babel.UpgradeOption");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var skillObj = new GameObject("SkillSystemForSelect");
            var upgradeObj = new GameObject("UpgradeSystemForSelect");
            float previousTimeScale = Time.timeScale;

            try
            {
                Component skillSystem = skillObj.AddComponent(skillSystemType);
                InvokePrivateStart(skillSystem);
                Component upgradeSystem = upgradeObj.AddComponent(upgradeSystemType);
                MethodInfo getByIdMethod = skillDatabaseType.GetMethod(
                    "GetById",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo hasSkillMethod = skillSystemType.GetMethod(
                    "HasSkill",
                    BindingFlags.Public | BindingFlags.Instance);

                upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { skillSystem });
                object aftershockConfig = getByIdMethod.Invoke(null, new object[] { "aftershock" });

                // 包装成 UpgradeOption
                object upgradeOpt = Activator.CreateInstance(upgradeOptionType);
                Type optionTypeEnum = upgradeOptionType.GetNestedType("OptionType");
                object newSkillValue = Enum.Parse(optionTypeEnum, "NewSkill");
                upgradeOptionType.GetField("Type").SetValue(upgradeOpt, newSkillValue);
                upgradeOptionType.GetField("Config").SetValue(upgradeOpt, aftershockConfig);
                var pendingOptions = new List<object> { upgradeOpt };

                // 通过反射传入 IReadOnlyList<UpgradeOption>
                // 创建泛型 List<UpgradeOption>
                Type listType = typeof(List<>).MakeGenericType(upgradeOptionType);
                object typedList = Activator.CreateInstance(listType);
                listType.GetMethod("Add").Invoke(typedList, new object[] { upgradeOpt });

                upgradeSystemType.GetMethod("SetPendingOptionsForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { typedList });
                Time.timeScale = 0f;

                upgradeSystemType.GetMethod("SelectOption", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { 0 });

                Assert.That((bool)hasSkillMethod.Invoke(skillSystem, new object[] { "aftershock" }), Is.True);
                Assert.That(
                    (int)upgradeSystemType.GetProperty("PendingOptionCountForTests").GetValue(upgradeSystem),
                    Is.EqualTo(0));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(skillObj);
                UnityEngine.Object.DestroyImmediate(upgradeObj);
            }
        }

        [Test]
        public void SelectOption_WhenClickSkillIsCoolingDown_RefreshesClickDamageAfterUpgrade()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
            Type upgradeOptionType = RequireType("Babel.UpgradeOption");
            Type inputContextType = RequireType("Babel.PointerInputContext");
            Type inputEventsType = RequireType("Babel.InputEvents");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var skillObj = new GameObject("SkillSystemCooldownUpgradeTest");
            var upgradeObj = new GameObject("UpgradeSystemCooldownUpgradeTest");
            float previousTimeScale = Time.timeScale;

            try
            {
                Component skillSystem = skillObj.AddComponent(skillSystemType);
                InvokePrivateStart(skillSystem);
                Component upgradeSystem = upgradeObj.AddComponent(upgradeSystemType);
                MethodInfo getByIdMethod = skillDatabaseType.GetMethod(
                    "GetById",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo getCooldownMethod = skillSystemType.GetMethod(
                    "GetCooldownProgress",
                    BindingFlags.Public | BindingFlags.Instance);
                upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { skillSystem });
                object aftershockConfig = getByIdMethod.Invoke(null, new object[] { "aftershock" });

                // 包装成 UpgradeOption
                object upgradeOpt = Activator.CreateInstance(upgradeOptionType);
                Type optionTypeEnum2 = upgradeOptionType.GetNestedType("OptionType");
                object newSkillValue2 = Enum.Parse(optionTypeEnum2, "NewSkill");
                upgradeOptionType.GetField("Type").SetValue(upgradeOpt, newSkillValue2);
                upgradeOptionType.GetField("Config").SetValue(upgradeOpt, aftershockConfig);

                // 创建泛型 List<UpgradeOption>
                Type listType = typeof(List<>).MakeGenericType(upgradeOptionType);
                object typedList = Activator.CreateInstance(listType);
                listType.GetMethod("Add").Invoke(typedList, new object[] { upgradeOpt });

                upgradeSystemType.GetMethod("SetPendingOptionsForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { typedList });

                object clickContext = CreatePointerInputContext(inputContextType, Vector2.zero);
                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                Assert.That(
                    (float)getCooldownMethod.Invoke(skillSystem, new object[] { "divine_finger" }),
                    Is.GreaterThan(0f));
                Time.timeScale = 0f;

                upgradeSystemType.GetMethod("SelectOption", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { 0 });

                Assert.That(
                    (float)getCooldownMethod.Invoke(skillSystem, new object[] { "divine_finger" }),
                    Is.EqualTo(0f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObj.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObj);
                UnityEngine.Object.DestroyImmediate(upgradeObj);
            }
        }

        [Test]
        public void ExpThreshold_GeneratesPendingOptionsIncrementsLevelAndPausesTime()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
            Type globalType = RequireType("Babel.Global");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { SkillsCsvText });
            var skillObj = new GameObject("SkillSystemForExp");
            var upgradeObj = new GameObject("UpgradeSystemForExp");
            float previousTimeScale = Time.timeScale;
            int previousExp = GetGlobalInt(globalType, "Exp");
            int previousLevel = GetGlobalInt(globalType, "Level");

            try
            {
                SetGlobalInt(globalType, "Exp", 0);
                SetGlobalInt(globalType, "Level", 1);
                Time.timeScale = 1f;
                Component skillSystem = skillObj.AddComponent(skillSystemType);
                InvokePrivateStart(skillSystem);
                Component upgradeSystem = upgradeObj.AddComponent(upgradeSystemType);
                upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { skillSystem });
                InvokePrivateStart(upgradeSystem);

                SetGlobalInt(globalType, "Exp", 5);

                Assert.That(GetGlobalInt(globalType, "Exp"), Is.EqualTo(0));
                Assert.That(GetGlobalInt(globalType, "Level"), Is.EqualTo(2));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(
                    (int)upgradeSystemType.GetProperty("PendingOptionCountForTests").GetValue(upgradeSystem),
                    Is.GreaterThan(0));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                SetGlobalInt(globalType, "Exp", previousExp);
                SetGlobalInt(globalType, "Level", previousLevel);
                UnityEngine.Object.DestroyImmediate(skillObj);
                UnityEngine.Object.DestroyImmediate(upgradeObj);
            }
        }

        private static void InvokePrivateStart(Component component)
        {
            MethodInfo startMethod = component.GetType().GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(startMethod, Is.Not.Null);
            startMethod.Invoke(component, Array.Empty<object>());
        }

        /// <summary>
        /// 从 IEnumerable&lt;UpgradeOption&gt; 中提取每个选项的 Config.SkillId。
        /// </summary>
        private static List<string> CollectSkillIds(IEnumerable options)
        {
            var skillIds = new List<string>();
            foreach (object option in options)
            {
                object config = option.GetType().GetField("Config").GetValue(option);
                skillIds.Add((string)config.GetType().GetField("SkillId").GetValue(config));
            }

            return skillIds;
        }

        private static object CreatePointerInputContext(Type inputContextType, Vector2 worldPosition)
        {
            return Activator.CreateInstance(
                inputContextType,
                new object[] { Vector2.zero, worldPosition, 0f, 0f });
        }

        private static int GetGlobalInt(Type globalType, string fieldName)
        {
            object property = globalType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
            return (int)property.GetType().GetProperty("Value").GetValue(property);
        }

        private static void SetGlobalInt(Type globalType, string fieldName, int value)
        {
            object property = globalType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);
            property.GetType().GetProperty("Value").SetValue(property, value);
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
            return type;
        }

        [Test]
        public void GetNextLevel_WhenNextLevelExists_ReturnsCorrectConfig()
        {
            Type dbType = RequireType("Babel.SkillDatabase");
            dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                  .Invoke(null, new object[] { SkillsCsvText });

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

                // 1. 获取 meteor level1 配置（GetById 返回最后一条，需用 GetAll 过滤 level=1）
                var getAll = dbType.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Static);
                var allSkills = (System.Collections.IList)getAll.Invoke(null, null);
                object meteorLevel1 = null;
                foreach (var cfg in allSkills)
                {
                    string id = (string)cfg.GetType().GetField("SkillId").GetValue(cfg);
                    int lv = (int)cfg.GetType().GetField("Level").GetValue(cfg);
                    if (id == "meteor" && lv == 1) { meteorLevel1 = cfg; break; }
                }
                Assert.That(meteorLevel1, Is.Not.Null, "meteor level1 must exist in CSV");

                // 2. 装备 meteor level1
                var addOrReplace = skillSystemType.GetMethod("AddOrReplaceSkill", BindingFlags.Public | BindingFlags.Instance);
                addOrReplace.Invoke(system, new[] { meteorLevel1 });

                // 3. 获取 level2 配置
                var getNextLevel = dbType.GetMethod("GetNextLevel", BindingFlags.Public | BindingFlags.Static);
                object level2Config = getNextLevel.Invoke(null, new object[] { "meteor", 1 });
                Assert.That(level2Config, Is.Not.Null, "meteor level2 must exist in CSV");

                // 4. 调用 UpgradeSkill
                var upgradeSkill = skillSystemType.GetMethod("UpgradeSkill", BindingFlags.Public | BindingFlags.Instance);
                upgradeSkill.Invoke(system, new[] { level2Config });

                // 5. 验证：HasSkill("meteor") 仍为 true
                //    meteor 是 OnClick 技能，替换了 divine_finger，所以装备数量为 1（仅 meteor）
                var hasSkill = skillSystemType.GetMethod("HasSkill", BindingFlags.Public | BindingFlags.Instance);
                Assert.That((bool)hasSkill.Invoke(system, new object[] { "meteor" }), Is.True);

                var getEquipped = skillSystemType.GetMethod("GetEquippedSkills", BindingFlags.Public | BindingFlags.Instance);
                var equipped = (System.Collections.IList)getEquipped.Invoke(system, null);
                Assert.That(equipped.Count, Is.EqualTo(1), "meteor is OnClick and replaces divine_finger; only 1 skill equipped");

                // 6. 验证：装备的 meteor 已升至 level2
                object equippedMeteor = equipped[0];
                object config = equippedMeteor.GetType().GetProperty("Config").GetValue(equippedMeteor);
                int equippedLevel = (int)config.GetType().GetField("Level").GetValue(config);
                Assert.That(equippedLevel, Is.EqualTo(2), "meteor should have been upgraded to level 2");
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [Test]
        public void BuildEligiblePool_WhenMeteorLevel1Equipped_ContainsLevelUpgradeOption()
        {
            Type dbType = RequireType("Babel.SkillDatabase");
            Type upgradeSystemType = RequireType("Babel.UpgradeSystem");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type upgradeOptionType = RequireType("Babel.UpgradeOption");
            dbType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                  .Invoke(null, new object[] { SkillsCsvText });

            var upgradeObj = new GameObject("UpgradeSystemPoolTest");
            var skillObj = new GameObject("SkillSystemPoolTest");
            try
            {
                Component skillSys = skillObj.AddComponent(skillSystemType);
                InvokePrivateStart(skillSys);

                // 获取 meteor level1 配置（GetById 返回最后一条 level2，需从 GetAll 中筛选）
                var getAll = dbType.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Static);
                var allSkills = (System.Collections.IList)getAll.Invoke(null, null);
                object meteorLevel1 = null;
                foreach (var cfg in allSkills)
                {
                    string id = (string)cfg.GetType().GetField("SkillId").GetValue(cfg);
                    int lv = (int)cfg.GetType().GetField("Level").GetValue(cfg);
                    if (id == "meteor" && lv == 1) { meteorLevel1 = cfg; break; }
                }
                Assert.That(meteorLevel1, Is.Not.Null, "meteor level1 must exist in CSV");

                // 装备 meteor level1
                var addOrReplace = skillSystemType.GetMethod("AddOrReplaceSkill", BindingFlags.Public | BindingFlags.Instance);
                addOrReplace.Invoke(skillSys, new[] { meteorLevel1 });

                Component upgradeSys = upgradeObj.AddComponent(upgradeSystemType);
                var setSkillSystem = upgradeSystemType.GetMethod("SetSkillSystemForTests", BindingFlags.Public | BindingFlags.Instance);
                setSkillSystem.Invoke(upgradeSys, new[] { skillSys });

                var generateOptions = upgradeSystemType.GetMethod("GenerateOptionsForTests", BindingFlags.Public | BindingFlags.Instance);
                var options = (System.Collections.IList)generateOptions.Invoke(upgradeSys, new object[] { 10 });

                Type optionTypeEnum = upgradeOptionType.GetNestedType("OptionType");
                object levelUpgradeValue = Enum.Parse(optionTypeEnum, "LevelUpgrade");

                bool hasLevelUpgrade = false;
                foreach (var opt in options)
                {
                    object typeValue = upgradeOptionType.GetField("Type").GetValue(opt);
                    if (typeValue.Equals(levelUpgradeValue)) { hasLevelUpgrade = true; break; }
                }
                Assert.That(hasLevelUpgrade, Is.True, "Pool should contain LevelUpgrade option for meteor when level1 is equipped");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upgradeObj);
                UnityEngine.Object.DestroyImmediate(skillObj);
            }
        }
    }
}
