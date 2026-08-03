using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class MainMenuTests
    {
        private const string PrefabPath = "Assets/Babel/Prefabs/UI/UIMainMenuPanel.prefab";
        private const string ScenePath = "Assets/Babel/Scenes/Menu/MainMenuScene.unity";
        private const string ExpectedPrefabGuid = "574ca57e6a565e74d812ff67d4809410";
        private const string ScreenId = "main-menu";

        [Test]
        public void UIMainMenuPanel_PrefabUsesScreenContractAndExplicitButtonBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Type panelType = RequireType("Babel.UIMainMenuPanel");
            Component panel = prefab == null ? null : prefab.GetComponent(panelType);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(PrefabPath), Is.EqualTo(ExpectedPrefabGuid));
            Assert.That(panel, Is.Not.Null);
            Assert.That(panelType.BaseType.FullName, Is.EqualTo("Babel.Unity.Presentation.UI.Screen"));

            var serialized = new SerializedObject(panel);
            Button startButton = serialized.FindProperty("_startButton").objectReferenceValue as Button;
            Button exitButton = serialized.FindProperty("_exitButton").objectReferenceValue as Button;

            Assert.That(startButton, Is.Not.Null);
            Assert.That(exitButton, Is.Not.Null);
            Assert.That(startButton.name, Is.EqualTo("StartButton"));
            Assert.That(exitButton.name, Is.EqualTo("ExitButton"));
            Assert.That(startButton.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(exitButton.transform.IsChildOf(prefab.transform), Is.True);
        }

        [Test]
        public void UIMainMenuPanel_RouterVisibilityOwnsOneShotButtonSubscriptions()
        {
            Type panelType = RequireType("Babel.UIMainMenuPanel");
            Type routerType = RequireType("Babel.Unity.Presentation.UI.ScreenRouter");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject panelObject = UnityEngine.Object.Instantiate(prefab);
            var routerObject = new GameObject("MainMenuRouterTest");
            int startCount = 0;

            try
            {
                Component panel = panelObject.GetComponent(panelType);
                Component router = routerObject.AddComponent(routerType);
                RequireMethod(panelType, "SetActionsForTests").Invoke(
                    panel,
                    new object[] { new Action(() => startCount++), new Action(() => { }) });

                RequireMethod(routerType, "Register").Invoke(router, new object[] { ScreenId, panel });
                RequireMethod(routerType, "Show").Invoke(router, new object[] { ScreenId });

                Button startButton = panelObject.transform.Find("StartButton").GetComponent<Button>();
                Button exitButton = panelObject.transform.Find("ExitButton").GetComponent<Button>();
                startButton.onClick.Invoke();
                startButton.onClick.Invoke();

                Assert.That(startCount, Is.EqualTo(1));
                Assert.That(startButton.interactable, Is.False);
                Assert.That(exitButton.interactable, Is.False);

                RequireMethod(routerType, "Hide").Invoke(router, new object[] { ScreenId });
                RequireMethod(routerType, "Show").Invoke(router, new object[] { ScreenId });
                startButton.onClick.Invoke();
                startButton.onClick.Invoke();

                Assert.That(startCount, Is.EqualTo(2), "Each visibility lifetime should install exactly one one-shot listener.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void MainMenuScene_HasExplicitRouterCanvasEventSystemAndPrefabBindings()
        {
            Type controllerType = RequireType("Babel.MainMenuController");
            Type panelType = RequireType("Babel.UIMainMenuPanel");
            Type routerType = RequireType("Babel.Unity.Presentation.UI.ScreenRouter");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                List<Component> controllers = Collect(scene, controllerType);
                List<Component> panels = Collect(scene, panelType);
                List<Component> routers = Collect(scene, routerType);
                List<Component> canvases = Collect(scene, typeof(Canvas));
                List<Component> eventSystems = Collect(scene, typeof(EventSystem));

                Assert.That(controllers.Count, Is.EqualTo(1));
                Assert.That(panels.Count, Is.EqualTo(1));
                Assert.That(routers.Count, Is.EqualTo(1));
                Assert.That(canvases.Count, Is.EqualTo(1));
                Assert.That(eventSystems.Count, Is.EqualTo(1));
                Assert.That(controllerType.BaseType, Is.EqualTo(typeof(MonoBehaviour)));

                var serialized = new SerializedObject(controllers[0]);
                Assert.That(serialized.FindProperty("_screenRouter").objectReferenceValue, Is.SameAs(routers[0]));
                Assert.That(serialized.FindProperty("_mainMenuPanel").objectReferenceValue, Is.SameAs(panels[0]));
                Assert.That(panels[0].transform.parent, Is.SameAs(canvases[0].transform));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panels[0].gameObject),
                    Is.EqualTo(PrefabPath));
                Assert.That(eventSystems[0].GetComponent<StandaloneInputModule>(), Is.Not.Null);
                Assert.That(canvases[0].GetComponent<CanvasScaler>(), Is.Not.Null);
                Assert.That(canvases[0].GetComponent<GraphicRaycaster>(), Is.Not.Null);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void UIMainMenuPanel_StartClosesStaleSettlementBeforeRequestingGameScene()
        {
            Type menuPanelType = RequireType("Babel.UIMainMenuPanel");
            Type gameOverPanelType = RequireType("Babel.UIGameOverPanel");
            Type routerType = RequireType("Babel.Unity.Presentation.UI.ScreenRouter");
            Type sessionType = RequireType("Babel.GameSession");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject menuObject = UnityEngine.Object.Instantiate(prefab);
            var routerObject = new GameObject("MainMenuStaleSettlementRouter");
            var staleSettlementObject = new GameObject("StaleGameOverPanel", typeof(RectTransform));
            float previousTimeScale = Time.timeScale;

            try
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { false });
                RequireMethod(sessionType, "ResetSession").Invoke(null, null);
                staleSettlementObject.AddComponent(gameOverPanelType);

                Component menuPanel = menuObject.GetComponent(menuPanelType);
                Component router = routerObject.AddComponent(routerType);
                RequireMethod(routerType, "Register").Invoke(router, new object[] { ScreenId, menuPanel });
                RequireMethod(routerType, "Show").Invoke(router, new object[] { ScreenId });

                Button startButton = menuObject.transform.Find("StartButton").GetComponent<Button>();
                startButton.onClick.Invoke();

                Assert.That(staleSettlementObject == null, Is.True);
                Assert.That(
                    RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null),
                    Is.EqualTo("GameScene"));
            }
            finally
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { true });
                RequireMethod(sessionType, "ResetSession").Invoke(null, null);
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(menuObject);
                if (staleSettlementObject != null)
                    UnityEngine.Object.DestroyImmediate(staleSettlementObject);
            }
        }

        private static List<Component> Collect(Scene scene, Type type)
        {
            var values = new List<Component>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                values.AddRange(roots[i].GetComponentsInChildren(type, true));
            return values;
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Babel")
                      ?? Type.GetType(fullName + ", Babel.Unity")
                      ?? Type.GetType(fullName + ", Assembly-CSharp")
                      ?? Type.GetType(fullName);
            if (type == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(fullName);
                    if (type != null) break;
                }
            }

            Assert.That(type, Is.Not.Null, fullName + " should exist in a loaded assembly.");
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName + " should exist.");
            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, type.FullName + "." + propertyName + " should exist.");
            return property;
        }
    }
}
