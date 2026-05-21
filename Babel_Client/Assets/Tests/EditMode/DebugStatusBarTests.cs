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
            Type globalType = RequireType("Babel.Global");
            var enemyObject = new GameObject("EnemyFatalHitFeedbackTest");
            var circleObject = new GameObject("Circle");
            int deathEventCount = 0;
            object expProperty = globalType.GetField("Exp").GetValue(null);
            PropertyInfo expValueProperty = expProperty.GetType().GetProperty("Value");
            int previousExp = (int)expValueProperty.GetValue(expProperty);

            Action<Vector2> onEnemyDied = _ => deathEventCount++;

            try
            {
                expValueProperty.SetValue(expProperty, 0);
                enemyEventsType.GetEvent("OnEnemyDied").AddEventHandler(null, onEnemyDied);
                circleObject.transform.SetParent(enemyObject.transform, false);
                SpriteRenderer renderer = circleObject.AddComponent<SpriteRenderer>();
                renderer.color = Color.white;
                Component enemy = enemyObject.AddComponent(enemyType);
                enemyType.GetField("Circle").SetValue(enemy, renderer);
                InvokeAwake(enemyType, enemy);

                enemyType.GetMethod("TakeDamage").Invoke(enemy, new object[] { 50f, false });
                InvokePrivate(enemyType, enemy, "Update");

                Assert.That(deathEventCount, Is.EqualTo(0));
                Assert.That(expValueProperty.GetValue(expProperty), Is.EqualTo(0));
                Assert.That(renderer.color.r, Is.EqualTo(Color.red.r).Within(0.001f));

                MethodInfo tickDeathMethod = enemyType.GetMethod("TickDeathFeedback", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(tickDeathMethod, Is.Not.Null, "Fatal damage should keep the enemy visible until feedback time finishes.");
                tickDeathMethod.Invoke(enemy, new object[] { 1f });

                Assert.That(deathEventCount, Is.EqualTo(1));
                Assert.That(expValueProperty.GetValue(expProperty), Is.EqualTo(1));
            }
            finally
            {
                enemyEventsType.GetEvent("OnEnemyDied").RemoveEventHandler(null, onEnemyDied);
                expValueProperty.SetValue(expProperty, previousExp);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
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
    }
}
