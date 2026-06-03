using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class MainMenuTests
    {
        [Test]
        public void UIMainMenuPanel_WhenRuntimeLayoutApplied_CreatesTitleStartAndExitButtons()
        {
            Type panelType = RequireType("Babel.UIMainMenuPanel");
            var panelObject = new GameObject("MainMenuPanelLayoutTest", typeof(RectTransform));

            try
            {
                Component panel = panelObject.AddComponent(panelType);

                InvokePrivate(panelType, panel, "BuildRuntimeLayout");

                Assert.That(panelObject.transform.Find("MenuBackground"), Is.Not.Null);
                Assert.That(panelObject.transform.Find("TowerBackground"), Is.Not.Null);
                Assert.That(panelObject.transform.Find("LightningAccent"), Is.Not.Null);
                Assert.That(panelObject.transform.Find("MenuTitle").GetComponent<Text>().text, Is.EqualTo("BABEL"));
                Assert.That(panelObject.transform.Find("MenuSubtitle").GetComponent<Text>().text, Does.Contain("天庭"));
                Assert.That(panelObject.transform.Find("StartButton").GetComponent<Button>(), Is.Not.Null);
                Assert.That(panelObject.transform.Find("ExitButton").GetComponent<Button>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void UIMainMenuPanel_StartButton_DisablesButtonsAndInvokesStartOnce()
        {
            Type panelType = RequireType("Babel.UIMainMenuPanel");
            var panelObject = new GameObject("MainMenuPanelStartTest", typeof(RectTransform));
            int startCount = 0;

            try
            {
                Component panel = panelObject.AddComponent(panelType);
                InvokePrivate(panelType, panel, "BuildRuntimeLayout");
                panelType.GetMethod("SetActionsForTests", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(panel, new object[] { new Action(() => startCount++), new Action(() => { }) });
                InvokePrivate(panelType, panel, "BindButtons");

                Button startButton = panelObject.transform.Find("StartButton").GetComponent<Button>();
                Button exitButton = panelObject.transform.Find("ExitButton").GetComponent<Button>();

                startButton.onClick.Invoke();
                startButton.onClick.Invoke();

                Assert.That(startCount, Is.EqualTo(1));
                Assert.That(startButton.interactable, Is.False);
                Assert.That(exitButton.interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void MainMenuController_ExistsForMainMenuSceneEntry()
        {
            Type controllerType = RequireType("Babel.MainMenuController");

            Assert.That(controllerType, Is.Not.Null);
        }

        [Test]
        public void UIMainMenuPanel_StartButton_ClosesStaleSettlementPanelsBeforeLoadingGame()
        {
            Type menuPanelType = RequireType("Babel.UIMainMenuPanel");
            Type gameOverPanelType = RequireType("Babel.UIGameOverPanel");
            var menuObject = new GameObject("MainMenuPanelStaleSettlementTest", typeof(RectTransform));
            var staleSettlementObject = new GameObject("StaleGameOverPanel", typeof(RectTransform));
            float previousTimeScale = Time.timeScale;

            try
            {
                Type sessionType = RequireType("Babel.GameSession");
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { false });
                RequireMethod(sessionType, "ResetSession").Invoke(null, null);
                staleSettlementObject.AddComponent(gameOverPanelType);

                Component menuPanel = menuObject.AddComponent(menuPanelType);
                InvokePrivate(menuPanelType, menuPanel, "BuildRuntimeLayout");
                InvokePrivate(menuPanelType, menuPanel, "BindButtons");

                Button startButton = menuPanel.transform.Find("StartButton").GetComponent<Button>();
                try
                {
                    startButton.onClick.Invoke();
                }
                catch (Exception exception)
                {
                    Assert.Fail(exception.ToString());
                }

                Assert.That(staleSettlementObject == null, Is.True);
                Assert.That(RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null), Is.EqualTo("GameScene"));
            }
            finally
            {
                Type sessionType = RequireType("Babel.GameSession");
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { true });
                RequireMethod(sessionType, "ResetSession").Invoke(null, null);
                Time.timeScale = previousTimeScale;
                if (menuObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(menuObject);
                }

                if (staleSettlementObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(staleSettlementObject);
                }
            }
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

        private static void InvokePrivate(Type type, Component component, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            method.Invoke(component, null);
        }

        private static MethodInfo RequireMethod(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, $"{type.FullName}.{propertyName} should exist.");
            return property;
        }
    }
}
