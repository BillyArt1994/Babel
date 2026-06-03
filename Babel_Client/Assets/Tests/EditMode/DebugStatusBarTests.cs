using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class DebugStatusBarTests
    {
        [Test]
        public void Enemy_WhenInitializedAndDamaged_ExposesHealthDebugState()
        {
            Type enemyType = RequireType("Babel.Enemy");
            Type enemyDataType = RequireType("Babel.EnemyData");
            Type pathType = RequireType("Babel.Path");
            Type buildPointType = RequireType("Babel.BuildPoint");
            var enemyObject = new GameObject("EnemyHealthDebugTest");
            var pathObject = new GameObject("PathForEnemyHealthDebugTest");

            try
            {
                object enemy = enemyObject.AddComponent(enemyType);
                object path = pathObject.AddComponent(pathType);
                pathType.GetField("wayPointList").SetValue(path, Array.CreateInstance(buildPointType, 0));
                object data = Activator.CreateInstance(enemyDataType);
                enemyDataType.GetField("Hp").SetValue(data, 30f);
                enemyDataType.GetField("MoveSpeed").SetValue(data, 1f);
                enemyDataType.GetField("BuildContribution").SetValue(data, 25);
                enemyDataType.GetField("BuildCharges").SetValue(data, 1);

                enemyType.GetMethod("Init").Invoke(enemy, new[] { path, data, -1 });
                enemyType.GetMethod("TakeDamage").Invoke(enemy, new object[] { 7f, false });

                Assert.That(RequireProperty(enemyType, "CurrentHealth").GetValue(enemy), Is.EqualTo(23f).Within(0.001f));
                Assert.That(RequireProperty(enemyType, "MaxHealth").GetValue(enemy), Is.EqualTo(30f).Within(0.001f));
                Assert.That(RequireProperty(enemyType, "HealthPercent").GetValue(enemy), Is.EqualTo(23f / 30f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(pathObject);
            }
        }

        [Test]
        public void BuildPoint_WhenProgressAdded_ExposesBuildDebugState()
        {
            Type buildPointType = RequireType("Babel.BuildPoint");
            var buildPointObject = new GameObject("BuildProgressDebugTest");

            try
            {
                object buildPoint = buildPointObject.AddComponent(buildPointType);
                buildPointType.GetField("buildAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(buildPoint, 100);

                buildPointType.GetMethod("AddBuildProgress").Invoke(buildPoint, new object[] { 25 });

                Assert.That(RequireProperty(buildPointType, "CurrentProgress").GetValue(buildPoint), Is.EqualTo(25));
                Assert.That(RequireProperty(buildPointType, "RequiredProgress").GetValue(buildPoint), Is.EqualTo(100));
                Assert.That(RequireProperty(buildPointType, "BuildProgressPercent").GetValue(buildPoint), Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void BuildPoint_WhenBuilt_TransitionsHiddenToBuildingToCompleted()
        {
            Type buildPointType = RequireType("Babel.BuildPoint");
            Type buildPointStateType = RequireType("Babel.BuildPointState");
            Type debugBarType = RequireType("Babel.DebugBuildProgressBar");
            var buildPointObject = new GameObject("BuildPointStateAndColorTest");

            try
            {
                SpriteRenderer renderer = buildPointObject.AddComponent<SpriteRenderer>();
                renderer.color = Color.black;
                Component buildPoint = buildPointObject.AddComponent(buildPointType);
                buildPointType.GetField("buildAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(buildPoint, 50);

                InvokeAwake(buildPointType, buildPoint);
                Component debugBar = (Component)buildPointObject.GetComponentInChildren(debugBarType, true);
                Assert.That(debugBar, Is.Not.Null);
                InvokeAwake(debugBarType, debugBar);

                PropertyInfo stateProperty = RequireProperty(buildPointType, "State");
                object hiddenState = Enum.Parse(buildPointStateType, "Hidden");
                object buildingState = Enum.Parse(buildPointStateType, "Building");
                object completedState = Enum.Parse(buildPointStateType, "Completed");

                Assert.That(stateProperty.GetValue(buildPoint), Is.EqualTo(hiddenState));
                Assert.That(renderer.enabled, Is.False);
                AssertRenderersEnabled(debugBar.GetComponentsInChildren<SpriteRenderer>(true), false);
                buildPointObject.SetActive(false);
                Assert.That(buildPointObject.activeSelf, Is.False);

                buildPointType.GetMethod("BeginBuild").Invoke(buildPoint, null);
                InvokePrivate(debugBarType, debugBar, "LateUpdate");

                Assert.That(buildPointObject.activeSelf, Is.True);
                Assert.That(stateProperty.GetValue(buildPoint), Is.EqualTo(buildingState));
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.color, Is.EqualTo(Color.white));
                AssertRenderersEnabled(debugBar.GetComponentsInChildren<SpriteRenderer>(true), true);

                buildPointType.GetMethod("AddBuildProgress").Invoke(buildPoint, new object[] { 50 });

                Assert.That(stateProperty.GetValue(buildPoint), Is.EqualTo(completedState));
                Assert.That(RequireProperty(buildPointType, "IsBuildCompleted").GetValue(buildPoint), Is.EqualTo(true));
                Assert.That(buildPointObject.activeSelf, Is.True);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.color, Is.EqualTo(Color.red));

                buildPointType.GetMethod("Reset").Invoke(buildPoint, null);
                InvokePrivate(debugBarType, debugBar, "LateUpdate");

                Assert.That(stateProperty.GetValue(buildPoint), Is.EqualTo(hiddenState));
                Assert.That(RequireProperty(buildPointType, "IsBuildCompleted").GetValue(buildPoint), Is.EqualTo(false));
                Assert.That(buildPointObject.activeSelf, Is.False);
                Assert.That(renderer.enabled, Is.False);
                AssertRenderersEnabled(debugBar.GetComponentsInChildren<SpriteRenderer>(true), false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void BuildEvents_ExposesBuildStateChangedEvent()
        {
            Type buildEventsType = RequireType("Babel.BuildEvents");
            EventInfo stateChangedEvent = buildEventsType.GetEvent("OnBuildStateChanged", BindingFlags.Public | BindingFlags.Static);

            Assert.That(stateChangedEvent, Is.Not.Null);
        }

        [Test]
        public void Enemy_WhenCreated_AddsDebugHealthBar()
        {
            Type enemyType = RequireType("Babel.Enemy");
            Type debugBarType = RequireType("Babel.DebugHealthBar");
            var enemyObject = new GameObject("EnemyDebugBarTest");

            try
            {
                Component enemy = enemyObject.AddComponent(enemyType);
                InvokeAwake(enemyType, enemy);

                Assert.That(enemyObject.GetComponentInChildren(debugBarType, true), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void BuildPoint_WhenCreated_AddsDebugBuildProgressBar()
        {
            Type buildPointType = RequireType("Babel.BuildPoint");
            Type debugBarType = RequireType("Babel.DebugBuildProgressBar");
            var buildPointObject = new GameObject("BuildPointDebugBarTest");

            try
            {
                Component buildPoint = buildPointObject.AddComponent(buildPointType);
                InvokeAwake(buildPointType, buildPoint);

                Assert.That(buildPointObject.GetComponentInChildren(debugBarType, true), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void Enemy_WhenBuildTimerCompletes_AddsProgressToTargetBuildPoint()
        {
            Type enemyType = RequireType("Babel.Enemy");
            Type enemyDataType = RequireType("Babel.EnemyData");
            Type pathType = RequireType("Babel.Path");
            Type buildPointType = RequireType("Babel.BuildPoint");
            var enemyObject = new GameObject("EnemyBuildContributionTest");
            var pathObject = new GameObject("PathBuildContributionTest");
            var buildPointObject = new GameObject("BuildPointContributionTest");

            try
            {
                Component enemy = enemyObject.AddComponent(enemyType);
                Component path = pathObject.AddComponent(pathType);
                Component buildPoint = buildPointObject.AddComponent(buildPointType);
                buildPointObject.transform.position = new Vector3(3f, 2f, 0f);
                enemyObject.transform.position = new Vector3(3f, -1f, 0f);
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
                InvokePrivate(enemyType, enemy, "Update");

                Assert.That(RequireProperty(buildPointType, "CurrentProgress").GetValue(buildPoint), Is.EqualTo(25));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(pathObject);
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void Enemy_WhenDamaged_FlashesRendererRedThenRestoresOriginalColor()
        {
            Type enemyType = RequireType("Babel.Enemy");
            var enemyObject = new GameObject("EnemyHitFlashTest");
            var circleObject = new GameObject("Circle");

            try
            {
                circleObject.transform.SetParent(enemyObject.transform, false);
                SpriteRenderer renderer = circleObject.AddComponent<SpriteRenderer>();
                renderer.color = Color.blue;
                Component enemy = enemyObject.AddComponent(enemyType);
                enemyType.GetField("Circle").SetValue(enemy, renderer);
                InvokeAwake(enemyType, enemy);

                enemyType.GetMethod("TakeDamage").Invoke(enemy, new object[] { 1f, false });

                Assert.That(renderer.color.r, Is.EqualTo(Color.red.r).Within(0.001f));
                Assert.That(renderer.color.g, Is.EqualTo(Color.red.g).Within(0.001f));
                Assert.That(renderer.color.b, Is.EqualTo(Color.red.b).Within(0.001f));

                MethodInfo tickMethod = enemyType.GetMethod("TickHitFlash", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(tickMethod, Is.Not.Null, "Enemy should expose a testable hit flash tick.");
                tickMethod.Invoke(enemy, new object[] { 1f });

                Assert.That(renderer.color.r, Is.EqualTo(Color.blue.r).Within(0.001f));
                Assert.That(renderer.color.g, Is.EqualTo(Color.blue.g).Within(0.001f));
                Assert.That(renderer.color.b, Is.EqualTo(Color.blue.b).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void Enemy_WhenDamagedWithoutCircle_UsesFallbackChildRendererForFlash()
        {
            Type enemyType = RequireType("Babel.Enemy");
            var enemyObject = new GameObject("EnemyFallbackHitFlashTest");
            var visualObject = new GameObject("Visual");

            try
            {
                visualObject.transform.SetParent(enemyObject.transform, false);
                SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
                renderer.color = Color.white;
                Component enemy = enemyObject.AddComponent(enemyType);
                InvokeAwake(enemyType, enemy);

                enemyType.GetMethod("TakeDamage").Invoke(enemy, new object[] { 1f, false });

                Assert.That(renderer.color.r, Is.EqualTo(Color.red.r).Within(0.001f));
                Assert.That(renderer.color.g, Is.EqualTo(Color.red.g).Within(0.001f));
                Assert.That(renderer.color.b, Is.EqualTo(Color.red.b).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void Enemy_WhenFatalDamageTaken_DelaysDeathCleanupUntilHitFeedbackFinishes()
        {
            Type enemyType = RequireType("Babel.Enemy");
            Type enemyEventsType = RequireType("Babel.EnemyEvents");
            var enemyObject = new GameObject("EnemyFatalHitFeedbackTest");
            var circleObject = new GameObject("Circle");
            int deathEventCount = 0;

            Action<Vector2> onEnemyDied = _ => deathEventCount++;

            try
            {
                enemyEventsType.GetEvent("OnEnemyDied").AddEventHandler(null, onEnemyDied);
                circleObject.transform.SetParent(enemyObject.transform, false);
                SpriteRenderer renderer = circleObject.AddComponent<SpriteRenderer>();
                renderer.color = Color.white;
                Component enemy = enemyObject.AddComponent(enemyType);
                enemyType.GetField("Circle").SetValue(enemy, renderer);
                InvokeAwake(enemyType, enemy);

                enemyType.GetMethod("TakeDamage").Invoke(enemy, new object[] { 50f, false });
                InvokePrivate(enemyType, enemy, "Update");

                // 致死后：死亡事件尚未触发，renderer 已变红（hit feedback 进行中）
                Assert.That(deathEventCount, Is.EqualTo(0));
                Assert.That(renderer.color.r, Is.EqualTo(Color.red.r).Within(0.001f));

                MethodInfo tickDeathMethod = enemyType.GetMethod("TickDeathFeedback", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(tickDeathMethod, Is.Not.Null, "Fatal damage should keep the enemy visible until feedback time finishes.");
                tickDeathMethod.Invoke(enemy, new object[] { 1f });

                // feedback 结束后死亡事件触发一次
                Assert.That(deathEventCount, Is.EqualTo(1));
            }
            finally
            {
                enemyEventsType.GetEvent("OnEnemyDied").RemoveEventHandler(null, onEnemyDied);
                UnityEngine.Object.DestroyImmediate(enemyObject);
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

        private static PropertyInfo RequireProperty(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, $"{type.FullName}.{propertyName} should exist for debug UI.");
            return property;
        }

        private static void InvokeAwake(Type type, Component component)
        {
            MethodInfo awake = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, $"{type.FullName}.Awake should initialize debug UI.");
            awake.Invoke(component, null);
        }

        private static void InvokePrivate(Type type, Component component, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            method.Invoke(component, null);
        }

        private static void AssertRenderersEnabled(SpriteRenderer[] renderers, bool expectedEnabled)
        {
            Assert.That(renderers.Length, Is.GreaterThan(0));
            foreach (SpriteRenderer renderer in renderers)
            {
                Assert.That(renderer.enabled, Is.EqualTo(expectedEnabled));
            }
        }
    }
}
