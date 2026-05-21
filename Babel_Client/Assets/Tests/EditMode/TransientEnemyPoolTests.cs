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
            var enemyObject = new GameObject("EnemyMovementTest");
            var buildPointObject = new GameObject("BuildPointMovementTest");

            try
            {
                enemyObject.transform.position = new Vector3(5f, -0.89f, 0f);
                buildPointObject.transform.position = new Vector3(-2.5f, 1.24f, 7f);
                Component enemy = enemyObject.AddComponent(enemyType);
                Component buildPoint = buildPointObject.AddComponent(buildPointType);

                MethodInfo method = enemyType.GetMethod(
                    "GetBuildApproachPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, "Enemy should expose a private testable build approach helper.");

                var result = (Vector3)method.Invoke(enemy, new object[] { buildPoint });

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

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
            return type;
        }
    }
}
