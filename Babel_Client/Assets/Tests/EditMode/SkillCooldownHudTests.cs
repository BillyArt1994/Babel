using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class SkillCooldownHudTests
    {
        private const string SKILLS_CSV_PATH = "Assets/Data/Skills/skills.csv";
        private const string GAME_PANEL_PATH = "Assets/Art/UIPrefab/UIGamePanel.prefab";

        [Test]
        public void SkillSystem_WhenClickSkillFires_ExposesActiveClickCooldownProgress()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type inputContextType = RequireType("Babel.PointerInputContext");
            Type inputEventsType = RequireType("Babel.InputEvents");
            InitSkillDatabase(skillDatabaseType);
            var skillObject = new GameObject("SkillSystemActiveCooldownTest");

            try
            {
                Component skillSystem = skillObject.AddComponent(skillSystemType);
                InvokePrivate(skillSystemType, skillSystem, "Start");
                object clickContext = CreatePointerInputContext(inputContextType, Vector2.zero);

                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });

                MethodInfo progressMethod = skillSystemType.GetMethod(
                    "GetActiveClickCooldownProgress",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(progressMethod, Is.Not.Null);
                Assert.That((float)progressMethod.Invoke(skillSystem, Array.Empty<object>()), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObject.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObject);
            }
        }

        [Test]
        public void UIGamePanel_WhenMainSkillIsCoolingDown_UpdatesMainSkillFillFromSkillSystem()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type inputContextType = RequireType("Babel.PointerInputContext");
            Type inputEventsType = RequireType("Babel.InputEvents");
            InitSkillDatabase(skillDatabaseType);
            var skillObject = new GameObject("SkillSystemHudCooldownTest");
            GameObject panelObject = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component skillSystem = skillObject.AddComponent(skillSystemType);
                InvokePrivate(skillSystemType, skillSystem, "Start");
                object clickContext = CreatePointerInputContext(inputContextType, Vector2.zero);
                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });

                Component panel = panelObject.GetComponent(RequireType("Babel.UIGamePanel"));
                Image fill = (Image)panel.GetType().GetField("MainSkill_ImageFill").GetValue(panel);
                fill.fillAmount = 0f;

                MethodInfo updateMethod = panel.GetType().GetMethod(
                    "UpdateMainSkillCooldownFill",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(updateMethod, Is.Not.Null);
                updateMethod.Invoke(panel, Array.Empty<object>());

                Assert.That(fill.fillAmount, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObject.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObject);
                PrefabUtility.UnloadPrefabContents(panelObject);
            }
        }

        [Test]
        public void SkillSystem_WhenPointerDownDuringCooldown_DoesNotQueueReleaseAfterCooldownExpires()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type enemyType = RequireType("Babel.Enemy");
            Type inputContextType = RequireType("Babel.PointerInputContext");
            Type inputEventsType = RequireType("Babel.InputEvents");
            InitSkillDatabase(skillDatabaseType);
            var skillObject = new GameObject("SkillSystemQueuedCooldownTest");
            var enemyObject = new GameObject("EnemyQueuedCooldownTest");

            try
            {
                Component skillSystem = skillObject.AddComponent(skillSystemType);
                InvokePrivate(skillSystemType, skillSystem, "Start");
                enemyObject.transform.position = Vector3.zero;
                enemyObject.AddComponent<CircleCollider2D>().radius = 0.5f;
                Component enemy = enemyObject.AddComponent(enemyType);
                enemyType.GetField("HP").SetValue(enemy, 200f);
                Physics2D.SyncTransforms();
                object clickContext = CreatePointerInputContext(inputContextType, Vector2.zero);

                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                float hpAfterFirstClick = (float)enemyType.GetField("HP").GetValue(enemy);
                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                TickFirstEquippedTrigger(skillSystemType, skillSystem, 1.1f);
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });

                Assert.That((float)enemyType.GetField("HP").GetValue(enemy), Is.EqualTo(hpAfterFirstClick));
            }
            finally
            {
                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObject.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void UIGamePanel_WhenPointerDownDuringCooldown_DoesNotShowChargeRing()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type inputContextType = RequireType("Babel.PointerInputContext");
            Type inputEventsType = RequireType("Babel.InputEvents");
            InitSkillDatabase(skillDatabaseType);
            var skillObject = new GameObject("SkillSystemChargeCooldownTest");
            GameObject panelObject = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component skillSystem = skillObject.AddComponent(skillSystemType);
                InvokePrivate(skillSystemType, skillSystem, "Start");
                object clickContext = CreatePointerInputContext(inputContextType, Vector2.zero);
                inputEventsType.GetMethod("RaisePointerDown", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });
                inputEventsType.GetMethod("RaisePointerUp", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { clickContext });

                Component panel = panelObject.GetComponent(RequireType("Babel.UIGamePanel"));
                RectTransform chargeRing = (RectTransform)panel.GetType().GetField("ChargeRing").GetValue(panel);
                chargeRing.gameObject.SetActive(false);
                panel.GetType().GetField("_panelRectTransform", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(panel, panelObject.transform);

                MethodInfo pointerDownMethod = panel.GetType().GetMethod(
                    "OnPointerDown",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                pointerDownMethod.Invoke(panel, new[] { clickContext });

                Assert.That(chargeRing.gameObject.activeSelf, Is.False);
            }
            finally
            {
                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObject.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObject);
                PrefabUtility.UnloadPrefabContents(panelObject);
            }
        }

        private static void InitSkillDatabase(Type skillDatabaseType)
        {
            TextAsset skillsCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(SKILLS_CSV_PATH);
            Assert.That(skillsCsv, Is.Not.Null, "Test fixture requires the production skills CSV.");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { skillsCsv.text });
        }

        private static object CreatePointerInputContext(Type inputContextType, Vector2 worldPosition)
        {
            return Activator.CreateInstance(
                inputContextType,
                new object[] { Vector2.zero, worldPosition, 0f, 0f });
        }

        private static void InvokePrivate(Type type, Component component, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, Array.Empty<object>());
        }

        private static void TickFirstEquippedTrigger(Type skillSystemType, Component skillSystem, float deltaTime)
        {
            var skills = (System.Collections.IEnumerable)skillSystemType.GetMethod(
                "GetEquippedSkills",
                BindingFlags.Public | BindingFlags.Instance)
                .Invoke(skillSystem, Array.Empty<object>());
            foreach (object skill in skills)
            {
                object trigger = skill.GetType().GetProperty("Trigger").GetValue(skill);
                trigger.GetType().GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(trigger, new object[] { deltaTime });
                return;
            }

            Assert.Fail("SkillSystem should have at least one equipped skill.");
        }

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
    }
}
