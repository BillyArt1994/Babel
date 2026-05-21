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
                Array pendingOptions = Array.CreateInstance(aftershockConfig.GetType(), 1);
                pendingOptions.SetValue(aftershockConfig, 0);
                upgradeSystemType.GetMethod("SetPendingOptionsForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { pendingOptions });
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
                Array pendingOptions = Array.CreateInstance(aftershockConfig.GetType(), 1);
                pendingOptions.SetValue(aftershockConfig, 0);
                upgradeSystemType.GetMethod("SetPendingOptionsForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(upgradeSystem, new object[] { pendingOptions });

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

        private static List<string> CollectSkillIds(IEnumerable skills)
        {
            var skillIds = new List<string>();
            foreach (object skill in skills)
            {
                skillIds.Add((string)skill.GetType().GetField("SkillId").GetValue(skill));
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
    }
}
