using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class TransientEnemyPoolTests
    {
        [Test]
        public void Get_WhenPrefabIsUnavailable_CreatesVisibleEnemyAtPosition()
        {
            Type enemyDatabaseType = RequireType("Babel.EnemyDatabase");
            MethodInfo initMethod = enemyDatabaseType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            initMethod.Invoke(null, new object[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,buildTime\n" +
                "worker,Worker,30,2,25,1,1,Missing/Worker,2"
            });

            Type poolType = RequireType("Babel.TransientEnemyPool");
            object pool = Activator.CreateInstance(poolType);
            MethodInfo getMethod = poolType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo activeCountProperty = poolType.GetProperty("ActiveCount", BindingFlags.Public | BindingFlags.Instance);
            var spawnPosition = new Vector2(1.25f, -0.5f);
            GameObject enemyObject = (GameObject)getMethod.Invoke(pool, new object[] { "worker", spawnPosition });

            try
            {
                Type enemyType = RequireType("Babel.Enemy");
                Assert.That(enemyObject, Is.Not.Null);
                Assert.That(enemyObject.transform.position.x, Is.EqualTo(spawnPosition.x).Within(0.001f));
                Assert.That(enemyObject.transform.position.y, Is.EqualTo(spawnPosition.y).Within(0.001f));
                Assert.That(enemyObject.GetComponent(enemyType), Is.Not.Null);
                CircleCollider2D collider = enemyObject.GetComponent<CircleCollider2D>();
                SpriteRenderer renderer = enemyObject.GetComponent<SpriteRenderer>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(collider.bounds.extents.x, Is.GreaterThanOrEqualTo(renderer.bounds.extents.x * 0.95f));
                Assert.That(enemyObject.layer, Is.EqualTo(LayerMask.NameToLayer("Enemy")));
                Assert.That((int)activeCountProperty.GetValue(pool), Is.EqualTo(1));
            }
            finally
            {
                if (enemyObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(enemyObject);
                }
            }
        }

        [Test]
        public void GetBuildApproachPosition_PreservesEnemyGroundY()
        {
            Type enemyType = RequireType("Babel.Enemy");
            Type buildPointType = RequireType("Babel.BuildPoint");
            Type builderMovementType = RequireType("Babel.BuilderMovement");
            Type enemyDataType = RequireType("Babel.EnemyData");
            var enemyObject = new GameObject("EnemyMovementTest");
            var buildPointObject = new GameObject("BuildPointMovementTest");

            try
            {
                enemyObject.transform.position = new Vector3(5f, -0.89f, 0f);
                buildPointObject.transform.position = new Vector3(-2.5f, 1.24f, 7f);
                Component enemy = enemyObject.AddComponent(enemyType);
                Component buildPoint = buildPointObject.AddComponent(buildPointType);

                // GetBuildApproachPosition 已搬到 BuilderMovement，构造并注入 owner
                object movement = Activator.CreateInstance(builderMovementType);
                object data = Activator.CreateInstance(enemyDataType);
                MethodInfo initMethod = builderMovementType.GetMethod(
                    "Init", BindingFlags.Instance | BindingFlags.Public);
                initMethod.Invoke(movement, new object[] { enemy, data });

                MethodInfo method = builderMovementType.GetMethod(
                    "GetBuildApproachPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "BuilderMovement should expose a private build approach helper.");

                var result = (Vector3)method.Invoke(movement, new object[] { buildPoint });

                Assert.That(result.x, Is.EqualTo(buildPointObject.transform.position.x).Within(0.001f));
                Assert.That(result.y, Is.EqualTo(enemyObject.transform.position.y).Within(0.001f));
                Assert.That(result.z, Is.EqualTo(enemyObject.transform.position.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(buildPointObject);
            }
        }

        [Test]
        public void GetNextTimeScaleIndex_CyclesThroughThreeSpeeds()
        {
            Type panelType = RequireType("Babel.UIGamePanel");
            MethodInfo method = panelType.GetMethod(
                "GetNextTimeScaleIndex",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "UIGamePanel should expose a private testable speed-cycle helper.");

            Assert.That((int)method.Invoke(null, new object[] { 0, 3 }), Is.EqualTo(1));
            Assert.That((int)method.Invoke(null, new object[] { 1, 3 }), Is.EqualTo(2));
            Assert.That((int)method.Invoke(null, new object[] { 2, 3 }), Is.EqualTo(0));
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
