using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class UISkillHudTests
    {
        private const string SKILLS_CSV_PATH = "Assets/Data/Skills/skills.csv";
        private const string GAME_PANEL_PATH = "Assets/Art/UIPrefab/UIGamePanel.prefab";

        [Test]
        public void SkillSystem_WhenPassiveSkillAdded_RaisesChangeEventAndExposesActiveAndPassiveSkills()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            Type skillSystemType = RequireType("Babel.SkillSystem");
            Type skillEventsType = RequireType("Babel.SkillEvents");
            InitSkillDatabase(skillDatabaseType);
            var skillObject = new GameObject("SkillSystemPassiveHudTest");
            int eventCount = 0;
            object lastSkills = Array.Empty<object>();
            Action<object> recorder = skills =>
            {
                eventCount++;
                lastSkills = skills;
            };
            Delegate onChanged = null;

            try
            {
                EventInfo changedEvent = skillEventsType.GetEvent("OnEquippedSkillsChanged", BindingFlags.Public | BindingFlags.Static);
                Assert.That(changedEvent, Is.Not.Null);
                onChanged = CreateEventRecorder(changedEvent.EventHandlerType, recorder);
                changedEvent.AddEventHandler(null, onChanged);
                Component skillSystem = skillObject.AddComponent(skillSystemType);
                InvokePrivate(skillSystemType, skillSystem, "Start");
                MethodInfo addOrReplaceMethod = skillSystemType.GetMethod("AddOrReplaceSkill", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getByIdMethod = skillDatabaseType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static);
                MethodInfo getActiveClickSkillMethod = skillSystemType.GetMethod("GetActiveClickSkill", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getPassiveSkillsMethod = skillSystemType.GetMethod("GetPassiveSkills", BindingFlags.Public | BindingFlags.Instance);

                Assert.That(getActiveClickSkillMethod, Is.Not.Null);
                Assert.That(getPassiveSkillsMethod, Is.Not.Null);
                addOrReplaceMethod.Invoke(skillSystem, new[] { getByIdMethod.Invoke(null, new object[] { "aftershock" }) });

                object activeSkill = getActiveClickSkillMethod.Invoke(skillSystem, Array.Empty<object>());
                List<string> passiveIds = CollectSkillIds((IEnumerable)getPassiveSkillsMethod.Invoke(skillSystem, Array.Empty<object>()));

                Assert.That(eventCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(CountItems((IEnumerable)lastSkills), Is.EqualTo(2));
                Assert.That(GetSkillId(activeSkill), Is.EqualTo("divine_finger"));
                Assert.That(passiveIds, Is.EqualTo(new[] { "aftershock" }));
            }
            finally
            {
                if (onChanged != null)
                {
                    skillEventsType.GetEvent("OnEquippedSkillsChanged", BindingFlags.Public | BindingFlags.Static)
                        ?.RemoveEventHandler(null, onChanged);
                }

                skillSystemType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(skillObject.GetComponent(skillSystemType), null);
                UnityEngine.Object.DestroyImmediate(skillObject);
            }
        }

        [Test]
        public void SkillIconLoader_WhenIconPathMissing_ReturnsFallbackSprite()
        {
            Type iconLoaderType = RequireType("Babel.SkillIconLoader");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            MethodInfo loadMethod = iconLoaderType.GetMethod("LoadIcon", BindingFlags.Public | BindingFlags.Static);
            Assert.That(loadMethod, Is.Not.Null);
            object config = Activator.CreateInstance(skillConfigType);
            skillConfigType.GetField("SkillId").SetValue(config, "missing_icon_skill");
            skillConfigType.GetField("SkillName").SetValue(config, "Missing Icon");
            skillConfigType.GetField("IconPath").SetValue(config, "Icons/does_not_exist");

            Sprite sprite = (Sprite)loadMethod.Invoke(null, new object[] { config });

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Does.Contain("Fallback"));
        }

        [Test]
        public void UIGamePanel_CategorizesOnClickAsActiveAndOtherTriggersAsPassive()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            MethodInfo isPassiveMethod = panelType.GetMethod("IsPassiveSkill", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(isPassiveMethod, Is.Not.Null);

            Assert.That((bool)isPassiveMethod.Invoke(null, new[] { CreateSkillConfig(skillConfigType, "OnClick") }), Is.False);
            Assert.That((bool)isPassiveMethod.Invoke(null, new[] { CreateSkillConfig(skillConfigType, "OnTimer") }), Is.True);
            Assert.That((bool)isPassiveMethod.Invoke(null, new[] { CreateSkillConfig(skillConfigType, "OnHit") }), Is.True);
            Assert.That((bool)isPassiveMethod.Invoke(null, new[] { CreateSkillConfig(skillConfigType, "OnKill") }), Is.True);
        }

        [Test]
        public void PassiveSkillIconView_WhenConfigured_UsesFallbackIconAndShowsStackBadge()
        {
            Type viewType = RequireType("Babel.PassiveSkillIconView");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            var iconObject = new GameObject("PassiveSkillIconViewTest", typeof(RectTransform));
            Component view = iconObject.AddComponent(viewType);
            object config = Activator.CreateInstance(skillConfigType);
            skillConfigType.GetField("SkillName").SetValue(config, "Aftershock");
            skillConfigType.GetField("IconPath").SetValue(config, "Icons/does_not_exist");

            try
            {
                MethodInfo configureMethod = viewType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(configureMethod, Is.Not.Null);

                configureMethod.Invoke(view, new[] { config, 3 });

                Image icon = iconObject.GetComponentInChildren<Image>(true);
                Text badge = iconObject.GetComponentInChildren<Text>(true);
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.sprite.name, Does.Contain("Fallback"));
                Assert.That(badge, Is.Not.Null);
                Assert.That(badge.gameObject.activeSelf, Is.True);
                Assert.That(badge.text, Is.EqualTo("3"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(iconObject);
            }
        }

        [Test]
        public void UIGamePanel_WhenSkillHudRefreshes_ShowsActiveIconAndSmallerPassiveColumn()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component gamePanel = panel.GetComponent(panelType);
                MethodInfo refreshMethod = panelType.GetMethod("RefreshSkillHud", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(refreshMethod, Is.Not.Null);

                object activeSkill = CreateSkillConfig(skillConfigType, "OnClick");
                skillConfigType.GetField("IconPath").SetValue(activeSkill, "Icons/does_not_exist");
                object passiveSkill = CreateSkillConfig(skillConfigType, "OnTimer");
                skillConfigType.GetField("IconPath").SetValue(passiveSkill, "Icons/does_not_exist");
                object skills = CreateSkillConfigList(skillConfigType, activeSkill, passiveSkill);

                refreshMethod.Invoke(gamePanel, new[] { skills });

                Image mainIcon = (Image)panelType.GetField("MainSkill_Image").GetValue(gamePanel);
                Transform passiveList = panel.transform.Find("PassiveSkillList");
                Assert.That(mainIcon.sprite, Is.Not.Null);
                Assert.That(mainIcon.sprite.name, Does.Contain("Fallback"));
                Assert.That(passiveList, Is.Not.Null);
                Assert.That(passiveList.childCount, Is.EqualTo(1));
                Assert.That(((RectTransform)passiveList.GetChild(0)).sizeDelta.x, Is.LessThan(mainIcon.rectTransform.sizeDelta.x));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_WhenUpgradeCardConfigured_ShowsIconTypeNameAndDescription()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component gamePanel = panel.GetComponent(panelType);
                Button card = (Button)panelType.GetField("Card1Btn").GetValue(gamePanel);
                object config = CreateSkillConfig(skillConfigType, "OnTimer");
                skillConfigType.GetField("SkillName").SetValue(config, "Aftershock");
                skillConfigType.GetField("Description").SetValue(config, "Damage nearby enemies.");
                skillConfigType.GetField("IconPath").SetValue(config, "Icons/does_not_exist");

                MethodInfo configureMethod = panelType.GetMethod("ConfigureUpgradeCard", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(configureMethod, Is.Not.Null);

                configureMethod.Invoke(gamePanel, new object[] { card, config });

                Image icon = card.transform.Find("SkillIcon").GetComponent<Image>();
                Text typeLabel = card.transform.Find("TypeLabel").GetComponent<Text>();
                Text nameText = card.transform.Find("SkillNameText").GetComponent<Text>();
                Text descText = card.transform.Find("SkillDecsText").GetComponent<Text>();
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.sprite.name, Does.Contain("Fallback"));
                Assert.That(typeLabel.text, Is.EqualTo("被动"));
                Assert.That(nameText.text, Is.EqualTo("Aftershock"));
                Assert.That(descText.text, Is.EqualTo("Damage nearby enemies."));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_WhenUpgradeCardConfigured_UsesReadableNonWhiteCardStyle()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            Type skillConfigType = RequireType("Babel.SkillConfig");
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component gamePanel = panel.GetComponent(panelType);
                Button card = (Button)panelType.GetField("Card1Btn").GetValue(gamePanel);
                object config = CreateSkillConfig(skillConfigType, "OnTimer");
                skillConfigType.GetField("SkillName").SetValue(config, "Aftershock");
                skillConfigType.GetField("Description").SetValue(config, "Damage nearby enemies.");
                skillConfigType.GetField("IconPath").SetValue(config, "Icons/does_not_exist");

                MethodInfo configureMethod = panelType.GetMethod("ConfigureUpgradeCard", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(configureMethod, Is.Not.Null);

                configureMethod.Invoke(gamePanel, new object[] { card, config });

                Image cardImage = card.GetComponent<Image>();
                Text typeLabel = card.transform.Find("TypeLabel").GetComponent<Text>();
                Text nameText = card.transform.Find("SkillNameText").GetComponent<Text>();
                Text descText = card.transform.Find("SkillDecsText").GetComponent<Text>();
                float backgroundLuminance = GetLuminance(cardImage.color);

                Assert.That(backgroundLuminance, Is.LessThan(0.85f), "Upgrade card background should not be near-white.");
                Assert.That(GetLuminance(card.colors.normalColor), Is.LessThan(0.85f), "Button normal tint should not turn the card white.");
                AssertReadableContrast(cardImage.color, typeLabel.color, "type label");
                AssertReadableContrast(cardImage.color, nameText.color, "skill name");
                AssertReadableContrast(cardImage.color, descText.color, "description");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        private static void AssertReadableContrast(Color background, Color foreground, string label)
        {
            float contrast = Mathf.Abs(GetLuminance(background) - GetLuminance(foreground));
            Assert.That(contrast, Is.GreaterThan(0.45f), $"{label} should contrast with upgrade card background.");
        }

        private static float GetLuminance(Color color)
        {
            return (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);
        }

        [Test]
        public void UIGamePanel_WhenRuntimeControlsEnsured_CreatesNarrowPauseButtonLeftOfSpeedButton()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                Component gamePanel = panel.GetComponent(panelType);
                MethodInfo ensureMethod = panelType.GetMethod("EnsureRuntimeHudControls", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensureMethod, Is.Not.Null);

                ensureMethod.Invoke(gamePanel, Array.Empty<object>());

                RectTransform pauseRect = (RectTransform)panel.transform.Find("PauseButton");
                RectTransform speedRect = RequireRect(panel, "TimeScale");
                Assert.That(pauseRect, Is.Not.Null);
                Assert.That(pauseRect.anchoredPosition.y, Is.EqualTo(speedRect.anchoredPosition.y));
                Assert.That(pauseRect.anchoredPosition.x, Is.LessThan(speedRect.anchoredPosition.x));
                Assert.That(pauseRect.sizeDelta.x, Is.LessThan(speedRect.sizeDelta.x));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_WhenPauseClicked_TogglesTimeScaleToZeroAndRestoresSelectedSpeed()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);
            float originalTimeScale = Time.timeScale;

            try
            {
                Component gamePanel = panel.GetComponent(panelType);
                panelType.GetMethod("EnsureRuntimeHudControls", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(gamePanel, Array.Empty<object>());
                Time.timeScale = 2f;

                MethodInfo pauseMethod = panelType.GetMethod("OnPauseClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pauseMethod, Is.Not.Null);
                pauseMethod.Invoke(gamePanel, Array.Empty<object>());
                Assert.That(Time.timeScale, Is.EqualTo(0f));

                pauseMethod.Invoke(gamePanel, Array.Empty<object>());
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        private static void InitSkillDatabase(Type skillDatabaseType)
        {
            TextAsset skillsCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(SKILLS_CSV_PATH);
            Assert.That(skillsCsv, Is.Not.Null, "Test fixture requires the production skills CSV.");
            skillDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { skillsCsv.text });
        }

        private static void InvokePrivate(Type type, Component component, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(component, Array.Empty<object>());
        }

        private static List<string> CollectSkillIds(IEnumerable skills)
        {
            var ids = new List<string>();
            foreach (object item in skills)
            {
                ids.Add(GetSkillId(item));
            }

            return ids;
        }

        private static Delegate CreateEventRecorder(Type eventHandlerType, Action<object> recorder)
        {
            MethodInfo invokeMethod = eventHandlerType.GetMethod("Invoke");
            ParameterInfo[] parameters = invokeMethod.GetParameters();
            var parameterExpressions = new ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterExpressions[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);
            }

            MethodInfo recordMethod = recorder.GetType().GetMethod("Invoke");
            Expression body = Expression.Call(
                Expression.Constant(recorder),
                recordMethod,
                Expression.Convert(parameterExpressions[0], typeof(object)));
            return Expression.Lambda(eventHandlerType, body, parameterExpressions).Compile();
        }

        private static object CreateSkillConfig(Type skillConfigType, string triggerType)
        {
            object config = Activator.CreateInstance(skillConfigType);
            skillConfigType.GetField("TriggerType").SetValue(config, triggerType);
            return config;
        }

        private static object CreateSkillConfigList(Type skillConfigType, params object[] configs)
        {
            Type listType = typeof(List<>).MakeGenericType(skillConfigType);
            object list = Activator.CreateInstance(listType);
            MethodInfo addMethod = listType.GetMethod("Add");
            for (int i = 0; i < configs.Length; i++)
            {
                addMethod.Invoke(list, new[] { configs[i] });
            }

            return list;
        }

        private static int CountItems(IEnumerable items)
        {
            int count = 0;
            foreach (object _ in items)
            {
                count++;
            }

            return count;
        }

        private static string GetSkillId(object item)
        {
            object config = item.GetType().GetProperty("Config")?.GetValue(item) ?? item;
            return (string)config.GetType().GetField("SkillId").GetValue(config);
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

        private static RectTransform RequireRect(GameObject panel, string path)
        {
            Transform target = panel.transform.Find(path);
            Assert.That(target, Is.Not.Null, $"{path} should exist in UIGamePanel prefab.");
            return (RectTransform)target;
        }
    }
}
