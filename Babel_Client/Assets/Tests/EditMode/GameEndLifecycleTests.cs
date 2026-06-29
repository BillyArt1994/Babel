using NUnit.Framework;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.UI;

namespace Babel.Tests
{
    public class GameEndLifecycleTests
    {
        [Test]
        public void GameSession_EndGameFreezesTimeAndBroadcastsOnlyOnce()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type reasonType = RequireType("Babel.GameEndReason");
            float previousTimeScale = Time.timeScale;
            int endedCount = 0;
            object lastResult = null;
            Delegate handler = null;

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = 2f;
                EventInfo endedEvent = RequireEvent(sessionType, "OnGameEnded");
                handler = CreateEventRecorder(endedEvent.EventHandlerType, result =>
                {
                    endedCount++;
                    lastResult = result;
                });
                endedEvent.AddEventHandler(null, handler);

                bool first = (bool)RequireMethod(sessionType, "EndGame")
                    .Invoke(null, new[] { Enum.Parse(reasonType, "Defeat") });
                bool second = (bool)RequireMethod(sessionType, "EndGame")
                    .Invoke(null, new[] { Enum.Parse(reasonType, "Victory") });

                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That((bool)RequireProperty(sessionType, "IsGameEnded").GetValue(null), Is.True);
                Assert.That(RequireProperty(sessionType, "EndReason").GetValue(null), Is.EqualTo(Enum.Parse(reasonType, "Defeat")));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(endedCount, Is.EqualTo(1));
                Assert.That(GetFieldValue(lastResult, "Reason"), Is.EqualTo(Enum.Parse(reasonType, "Defeat")));
            }
            finally
            {
                if (handler != null)
                {
                    RequireEvent(sessionType, "OnGameEnded").RemoveEventHandler(null, handler);
                }

                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = previousTimeScale;
            }
        }

        [Test]
        public void GameSession_TickCountdownStopsAfterEndAndTriggersVictoryAtZero()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type globalType = RequireType("Babel.Global");
            Type reasonType = RequireType("Babel.GameEndReason");
            float previousTimeScale = Time.timeScale;
            object currentTime = globalType.GetField("CurrentTime").GetValue(null);
            PropertyInfo valueProperty = currentTime.GetType().GetProperty("Value");

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                valueProperty.SetValue(currentTime, 2f);
                RequireMethod(sessionType, "TickCountdown").Invoke(null, new object[] { 0.5f });
                Assert.That((float)valueProperty.GetValue(currentTime), Is.EqualTo(1.5f).Within(0.001f));

                RequireMethod(sessionType, "EndGame").Invoke(null, new[] { Enum.Parse(reasonType, "Defeat") });
                RequireMethod(sessionType, "TickCountdown").Invoke(null, new object[] { 10f });
                Assert.That((float)valueProperty.GetValue(currentTime), Is.EqualTo(1.5f).Within(0.001f));

                InvokeStatic(sessionType, "ResetSession");
                valueProperty.SetValue(currentTime, 0.25f);
                RequireMethod(sessionType, "TickCountdown").Invoke(null, new object[] { 1f });
                Assert.That((float)valueProperty.GetValue(currentTime), Is.EqualTo(0f).Within(0.001f));
                Assert.That(RequireProperty(sessionType, "EndReason").GetValue(null), Is.EqualTo(Enum.Parse(reasonType, "Victory")));
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = previousTimeScale;
            }
        }

        [Test]
        public void BuildPoint_WhenFinalLayerCompleted_EndsGameAsDefeat()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type reasonType = RequireType("Babel.GameEndReason");
            Type pathType = RequireType("Babel.Path");
            Type buildPointType = RequireType("Babel.BuildPoint");
            var pathObject = new GameObject("FinalLayerPathDefeatTest");
            var buildPointObject = new GameObject("FinalLayerBuildPointDefeatTest");
            float previousTimeScale = Time.timeScale;

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                Component path = pathObject.AddComponent(pathType);
                Component buildPoint = buildPointObject.AddComponent(buildPointType);

                buildPointType.GetField("buildAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(buildPoint, 100);
                buildPointType.GetField("OwnerPath").SetValue(buildPoint, path);

                Array wayPoints = Array.CreateInstance(buildPointType, 1);
                wayPoints.SetValue(buildPoint, 0);
                pathType.GetField("wayPointList").SetValue(path, wayPoints);
                pathType.GetField("nextLayerPath").SetValue(path, null);

                buildPointType.GetMethod("AddBuildProgress").Invoke(buildPoint, new object[] { 100 });

                Assert.That((bool)RequireProperty(sessionType, "IsGameEnded").GetValue(null), Is.True);
                Assert.That(RequireProperty(sessionType, "EndReason").GetValue(null), Is.EqualTo(Enum.Parse(reasonType, "Defeat")));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(pathObject);
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void GameSession_RestartStartAndReturnRouteToExpectedScenes()
        {
            Type sessionType = RequireType("Babel.GameSession");
            float previousTimeScale = Time.timeScale;

            try
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { false });
                InvokeStatic(sessionType, "ResetSession");

                RequireMethod(sessionType, "RestartGame").Invoke(null, null);
                Assert.That(RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null), Is.EqualTo("GameScene"));

                RequireMethod(sessionType, "ReturnToMainMenu").Invoke(null, null);
                Assert.That(RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null), Is.EqualTo("MainMenuScene"));

                RequireMethod(sessionType, "StartGame").Invoke(null, null);
                Assert.That(RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null), Is.EqualTo("GameScene"));
            }
            finally
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { true });
                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = previousTimeScale;
            }
        }

        [Test]
        public void Enemy_WhenGameEnded_DoesNotContinueBuilding()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type reasonType = RequireType("Babel.GameEndReason");
            Type enemyType = RequireType("Babel.Enemy");
            Type enemyDataType = RequireType("Babel.EnemyData");
            Type pathType = RequireType("Babel.Path");
            Type buildPointType = RequireType("Babel.BuildPoint");
            var enemyObject = new GameObject("EnemyGameEndStopTest");
            var pathObject = new GameObject("PathGameEndStopTest");
            var buildPointObject = new GameObject("BuildPointGameEndStopTest");

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                Component enemy = enemyObject.AddComponent(enemyType);
                Component path = pathObject.AddComponent(pathType);
                Component buildPoint = buildPointObject.AddComponent(buildPointType);
                buildPointObject.transform.position = Vector3.zero;
                enemyObject.transform.position = Vector3.zero;
                buildPointType.GetField("buildAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(buildPoint, 100);
                buildPointType.GetField("OwnerPath").SetValue(buildPoint, path);
                Array wayPoints = Array.CreateInstance(buildPointType, 1);
                wayPoints.SetValue(buildPoint, 0);
                pathType.GetField("wayPointList").SetValue(path, wayPoints);

                object data = Activator.CreateInstance(enemyDataType);
                enemyDataType.GetField("Hp").SetValue(data, 30f);
                enemyDataType.GetField("MoveSpeed").SetValue(data, 1f);
                enemyDataType.GetField("BuildContribution").SetValue(data, 25);
                enemyDataType.GetField("BuildCharges").SetValue(data, 1);
                enemyDataType.GetField("BuildTime").SetValue(data, 0f);
                enemyType.GetMethod("Init").Invoke(enemy, new object[] { path, data, -1 });

                InvokePrivate(enemyType, enemy, "Update");
                Assert.That(RequireProperty(buildPointType, "CurrentProgress").GetValue(buildPoint), Is.EqualTo(0));

                RequireMethod(sessionType, "EndGame").Invoke(null, new[] { Enum.Parse(reasonType, "Defeat") });
                InvokePrivate(enemyType, enemy, "Update");

                Assert.That(RequireProperty(buildPointType, "CurrentProgress").GetValue(buildPoint), Is.EqualTo(0));
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(pathObject);
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void EnemyGenerator_ShouldUpdateSchedulerOnlyWhileSessionIsPlaying()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type reasonType = RequireType("Babel.GameEndReason");
            Type generatorType = RequireType("Babel.EnemyGenerator");
            Type schedulerType = RequireType("Babel.WaveScheduler");
            var generatorObject = new GameObject("EnemyGeneratorGateTest");

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                Component generator = generatorObject.AddComponent(generatorType);
                generatorType.GetField("_scheduler", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(generator, FormatterServices.GetUninitializedObject(schedulerType));

                MethodInfo shouldUpdateMethod = generatorType.GetMethod(
                    "ShouldUpdateScheduler",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(shouldUpdateMethod, Is.Not.Null);
                Assert.That((bool)shouldUpdateMethod.Invoke(generator, null), Is.True);

                RequireMethod(sessionType, "EndGame").Invoke(null, new[] { Enum.Parse(reasonType, "Defeat") });

                Assert.That((bool)shouldUpdateMethod.Invoke(generator, null), Is.False);
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                UnityEngine.Object.DestroyImmediate(generatorObject);
            }
        }

        [Test]
        public void SettlementPanelRuntime_ConfiguresResultTextKillCountAndButtons()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type statsType = RequireType("Babel.StatsTracker");
            Type reasonType = RequireType("Babel.GameEndReason");
            Type panelRuntimeType = RequireType("Babel.SettlementPanelRuntime");
            var panelObject = new GameObject("SettlementPanelRuntimeTest", typeof(RectTransform));
            CreateTextChild(panelObject.transform, "Title");
            CreateTextChild(panelObject.transform, "RestartDesc");
            int restartCount = 0;
            int menuCount = 0;

            try
            {
                InvokeStatic(sessionType, "ResetSession");
                RequireMethod(statsType, "RecordKill").Invoke(null, null);
                RequireMethod(statsType, "RecordKill").Invoke(null, null);
                RequireMethod(sessionType, "EndGame").Invoke(null, new[] { Enum.Parse(reasonType, "Victory") });
                object result = RequireProperty(sessionType, "Result").GetValue(null);

                RequireMethod(panelRuntimeType, "Configure").Invoke(
                    null,
                    new object[]
                    {
                        panelObject.transform,
                        result,
                        new Action(() => restartCount++),
                        new Action(() => menuCount++)
                    });

                Transform root = panelObject.transform.Find("SettlementRoot");
                Text title = root.Find("VictoryCard/Title").GetComponent<Text>();
                Text description = root.Find("VictoryCard/Subtitle").GetComponent<Text>();
                Text killCount = root.Find("VictoryCard/KillCountBadge").GetComponent<Text>();
                Button restartButton = root.Find("SettlementButtons/RestartButton").GetComponent<Button>();
                Button menuButton = root.Find("SettlementButtons/MenuButton").GetComponent<Button>();

                Assert.That(title.text, Does.Contain("天神"));
                Assert.That(description.text, Does.Contain("人类"));
                Assert.That(killCount.text, Does.Contain("消灭了 2 名人类"));
                Assert.That(restartButton, Is.Not.Null);
                Assert.That(menuButton, Is.Not.Null);

                restartButton.onClick.Invoke();
                restartButton.onClick.Invoke();
                menuButton.onClick.Invoke();

                Assert.That(restartCount, Is.EqualTo(1));
                Assert.That(menuCount, Is.EqualTo(0));
                Assert.That(restartButton.interactable, Is.False);
                Assert.That(menuButton.interactable, Is.False);
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void SettlementPanelRuntime_UsesVictoryCardAndDefeatFullScreenOverlay()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type panelRuntimeType = RequireType("Babel.SettlementPanelRuntime");
            Type reasonType = RequireType("Babel.GameEndReason");
            Type resultType = RequireType("Babel.GameSessionResult");
            var panelObject = new GameObject("SettlementLayoutVariantTest", typeof(RectTransform));

            try
            {
                object victoryResult = Activator.CreateInstance(
                    resultType,
                    Enum.Parse(reasonType, "Victory"),
                    8,
                    0f);
                object defeatResult = Activator.CreateInstance(
                    resultType,
                    Enum.Parse(reasonType, "Defeat"),
                    3,
                    120f);

                RequireMethod(panelRuntimeType, "Configure").Invoke(
                    null,
                    new object[] { panelObject.transform, victoryResult, new Action(() => { }), new Action(() => { }) });

                Transform victoryRoot = panelObject.transform.Find("SettlementRoot");
                Assert.That(victoryRoot, Is.Not.Null);
                Assert.That(victoryRoot.Find("DimOverlay"), Is.Not.Null);
                Assert.That(victoryRoot.Find("VictoryCard"), Is.Not.Null);
                Assert.That(victoryRoot.Find("VictoryCard/ResultBadge"), Is.Not.Null);
                Assert.That(victoryRoot.Find("VictoryCard/KillCountBadge"), Is.Not.Null);
                Assert.That(victoryRoot.Find("DefeatOverlay"), Is.Null);

                RequireMethod(panelRuntimeType, "Configure").Invoke(
                    null,
                    new object[] { panelObject.transform, defeatResult, new Action(() => { }), new Action(() => { }) });

                Transform defeatRoot = panelObject.transform.Find("SettlementRoot");
                RectTransform defeatOverlay = defeatRoot.Find("DefeatOverlay") as RectTransform;
                Assert.That(defeatOverlay, Is.Not.Null);
                Assert.That(defeatRoot.Find("VictoryCard"), Is.Null);
                Assert.That(defeatRoot.Find("DefeatOverlay/ResultBadge"), Is.Not.Null);
                Assert.That(defeatRoot.Find("DefeatOverlay/KillCountBadge"), Is.Not.Null);
                Assert.That(defeatOverlay.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(defeatOverlay.anchorMax, Is.EqualTo(Vector2.one));
            }
            finally
            {
                InvokeStatic(sessionType, "ResetSession");
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void SettlementPanel_ReturnToMenuImmediatelyHidesItselfBeforeSceneRouting()
        {
            Type sessionType = RequireType("Babel.GameSession");
            Type overPanelType = RequireType("Babel.UIGameOverPanel");
            var panelObject = new GameObject("SettlementSelfCloseTest", typeof(RectTransform));
            float previousTimeScale = Time.timeScale;

            try
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { false });
                InvokeStatic(sessionType, "ResetSession");
                Component panel = panelObject.AddComponent(overPanelType);

                InvokePrivate(overPanelType, panel, "ReturnToMenuFromSettlement");

                Assert.That(panelObject.activeSelf, Is.False);
                Assert.That(RequireProperty(sessionType, "LastRequestedSceneNameForTests").GetValue(null), Is.EqualTo("MainMenuScene"));
            }
            finally
            {
                RequireMethod(sessionType, "SetSceneLoadingEnabledForTests").Invoke(null, new object[] { true });
                InvokeStatic(sessionType, "ResetSession");
                Time.timeScale = previousTimeScale;
                if (panelObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(panelObject);
                }
            }
        }

        private static void CreateTextChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            child.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private static EventInfo RequireEvent(Type type, string eventName)
        {
            EventInfo eventInfo = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(eventInfo, Is.Not.Null, $"{type.FullName}.{eventName} should exist.");
            return eventInfo;
        }

        private static void InvokeStatic(Type type, string methodName)
        {
            RequireMethod(type, methodName).Invoke(null, null);
        }

        private static void InvokePrivate(Type type, Component component, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            method.Invoke(component, null);
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"{target.GetType().FullName}.{fieldName} should exist.");
            return field.GetValue(target);
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
    }
}
