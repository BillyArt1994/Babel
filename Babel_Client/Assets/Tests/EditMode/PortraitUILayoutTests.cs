using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class PortraitUILayoutTests
    {
        private const string UI_ROOT_PATH = "Assets/QFramework/Toolkits/UIKit/Scripts/Resources/UIRoot.prefab";
        private const string GAME_PANEL_PATH = "Assets/Art/UIPrefab/UIGamePanel.prefab";
        private const string GAME_PASS_PANEL_PATH = "Assets/Art/UIPrefab/UIGamePassPanel.prefab";
        private const string GAME_OVER_PANEL_PATH = "Assets/Art/UIPrefab/UIGameOverPanel.prefab";

        [Test]
        public void UIRoot_UsesPortraitFriendlyCanvasScaler()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UI_ROOT_PATH);

            try
            {
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();

                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution.x, Is.EqualTo(720f));
                Assert.That(scaler.referenceResolution.y, Is.EqualTo(1280f));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void UIGamePanel_HudControlsStayInPortraitSafeAreas()
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                ApplyRuntimePortraitLayout(panel);
                AssertCornerControl(panel, "PauseButton", new Vector2(0f, 1f), new Vector2(24f, -72f), new Vector2(32f, 80f));
                AssertCornerControl(panel, "TimeScale", new Vector2(0f, 1f), new Vector2(84f, -72f), new Vector2(80f, 80f));
                AssertTopCenter(panel, "LevelTimer/TimerText", -56f, 180f, 56f);
                AssertCornerControl(panel, "MainSkill_Image", new Vector2(1f, 1f), new Vector2(-72f, -72f));
                AssertPassiveColumn(panel, "PassiveSkillList");
                AssertBottomStretch(panel, "EXP_Info", 64f, -64f, 96f);
                AssertBottomStretch(panel, "EXP_Info/EXPScrollbar", 24f, -32f, 20f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_UpgradeCardsAreHorizontalPortraitRow()
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                ApplyRuntimePortraitLayout(panel);
                AssertPortraitCard(panel, "UpgradePanel/Card1Btn", -215f);
                AssertPortraitCard(panel, "UpgradePanel/Card2Btn", 0f);
                AssertPortraitCard(panel, "UpgradePanel/Card3Btn", 215f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_UpgradePanelDoesNotOverrideCardPositionsWithLayoutGroup()
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                ApplyRuntimePortraitLayout(panel);
                RectTransform upgradePanel = RequireRect(panel, "UpgradePanel");
                LayoutGroup[] layoutGroups = upgradePanel.GetComponents<LayoutGroup>();

                Assert.That(layoutGroups, Is.Not.Empty, "UpgradePanel should keep its existing layout component disabled instead of silently re-enabling horizontal layout.");
                for (int i = 0; i < layoutGroups.Length; i++)
                {
                    Assert.That(layoutGroups[i].enabled, Is.False, $"{layoutGroups[i].GetType().Name} overrides portrait card positions at runtime.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [Test]
        public void UIGamePanel_UpgradeCardTextHasPortraitReadableInset()
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(GAME_PANEL_PATH);

            try
            {
                ApplyRuntimePortraitLayout(panel);
                AssertPortraitCardText(panel, "UpgradePanel/Card1Btn");
                AssertPortraitCardText(panel, "UpgradePanel/Card2Btn");
                AssertPortraitCardText(panel, "UpgradePanel/Card3Btn");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        [TestCase(GAME_PASS_PANEL_PATH)]
        [TestCase(GAME_OVER_PANEL_PATH)]
        public void EndStatePanel_TextUsesReadablePortraitLayout(string prefabPath)
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                AssertFullScreenRoot((RectTransform)panel.transform);
                AssertCenteredText(panel, "Title", 96f, 560f, 120f);
                AssertCenteredText(panel, "RestartDesc", -80f, 560f, 64f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        private static void AssertCornerControl(
            GameObject panel,
            string path,
            Vector2 expectedAnchor,
            Vector2 expectedPosition,
            Vector2 expectedSize = default)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(rect.anchorMax, Is.EqualTo(expectedAnchor));
            Assert.That(rect.anchoredPosition, Is.EqualTo(expectedPosition));
            Vector2 size = expectedSize == default ? new Vector2(80f, 80f) : expectedSize;
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
        }

        private static void AssertTopCenter(
            GameObject panel,
            string path,
            float expectedY,
            float expectedWidth,
            float expectedHeight)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, expectedY)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(expectedWidth, expectedHeight)));
        }

        private static void AssertPassiveColumn(GameObject panel, string path)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-24f, -118f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(40f, 360f)));
        }

        private static void AssertBottomStretch(
            GameObject panel,
            string path,
            float expectedY,
            float expectedWidthDelta,
            float expectedHeight)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(expectedY));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(expectedWidthDelta));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(expectedHeight));
        }

        private static void AssertPortraitCard(GameObject panel, string path, float expectedX)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(expectedX, 0f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(190f, 280f)));
        }

        private static void AssertPortraitCardText(GameObject panel, string cardPath)
        {
            RectTransform card = RequireRect(panel, cardPath);

            AssertCardChild(card, "SkillIcon", new Vector2(0f, 126f), new Vector2(56f, 56f));
            AssertCardText(card, "TypeLabel", new Vector2(0f, 78f), new Vector2(88f, 26f), 20);
            AssertCardText(card, "SkillNameText", new Vector2(0f, 36f), new Vector2(-28f, 44f), 24);
            AssertCardText(card, "SkillDecsText", new Vector2(0f, -58f), new Vector2(-28f, 118f), 18);
        }

        private static void AssertCardText(RectTransform card, string childName, Vector2 position, Vector2 sizeDelta, int fontSize)
        {
            RectTransform rect = AssertCardChild(card, childName, position, sizeDelta);
            Text text = rect.GetComponent<Text>();

            Assert.That(text, Is.Not.Null, $"{childName} should have a text label.");
            Assert.That(text.fontSize, Is.EqualTo(fontSize));
            Assert.That(text.resizeTextForBestFit, Is.True);
            Assert.That(text.resizeTextMinSize, Is.EqualTo(12));
            Assert.That(text.resizeTextMaxSize, Is.EqualTo(fontSize));
        }

        private static RectTransform AssertCardChild(RectTransform card, string childName, Vector2 position, Vector2 sizeDelta)
        {
            Transform child = card.Find(childName);
            Assert.That(child, Is.Not.Null, $"{card.name}/{childName} should exist.");
            RectTransform rect = (RectTransform)child;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(rect.sizeDelta, Is.EqualTo(sizeDelta));
            return rect;
        }

        private static void AssertFullScreenRoot(RectTransform rect)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
        }

        private static void AssertCenteredText(
            GameObject panel,
            string path,
            float expectedY,
            float expectedWidth,
            float expectedHeight)
        {
            RectTransform rect = RequireRect(panel, path);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, expectedY)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(expectedWidth, expectedHeight)));
        }

        private static RectTransform RequireRect(GameObject panel, string path)
        {
            Transform target = panel.transform.Find(path);
            Assert.That(target, Is.Not.Null, $"{path} should exist in UIGamePanel prefab.");
            return (RectTransform)target;
        }

        private static void ApplyRuntimePortraitLayout(GameObject panel)
        {
            Type panelType = Type.GetType("Babel.UIGamePanel, Babel")
                          ?? Type.GetType("Babel.UIGamePanel, Assembly-CSharp");
            Assert.That(panelType, Is.Not.Null);
            Component gamePanel = panel.GetComponent(panelType);
            Assert.That(gamePanel, Is.Not.Null);
            MethodInfo method = panelType.GetMethod("ApplyRuntimePortraitLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(gamePanel, Array.Empty<object>());
        }
    }
}
