using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class ScoutTargetingTests
    {
        [Test]
        public void ScoutEnemy_OnInit_ReservesGatewayWhenAvailable()
        {
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i * 5, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var enemyGo = new GameObject("Scout");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData { Hp = 25, MoveSpeed = 5, BuildContribution = 25, BuildCharges = 1, TargetMode = "scout", BuildTime = 1.2f };

            enemy.Init(path, data, -1);

            Assert.That(bps[1].IsOccupied, Is.True, "斥候应优先预约 gateway");

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        [Test]
        public void Enemy_WhenNoReservableButGatewayBuilt_StartsClimbing()
        {
            var nextGo = new GameObject("NextLayer");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NextBP");
            var nbp = nextBpGo.AddComponent<BuildPoint>();
            nextPath.wayPointList = new[] { nbp };

            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            path.nextLayerPath = nextPath;
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i * 5, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            bps[0].isGateway = true;
            path.wayPointList = bps;
            // gateway(0)+idx1 建完；idx2 被外部占用 → 预约返回 -1，但 IsCompleted=false
            bps[0].AddBuildProgress(99999);
            bps[1].AddBuildProgress(99999);
            bps[2].SetOccupied(true);

            var enemyGo = new GameObject("W");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25, BuildCharges = 1, TargetMode = "", BuildTime = 1f };
            enemy.Init(path, data, -1);
            // Init 已 ReserveNextTarget()，此时 _targetBuildPointIndex 应为 -1

            InvokePrivate(enemy, "UpdateMovingToBuildPoint");

            string state = GetState(enemy);
            Assert.That(state, Is.EqualTo("MovingToPassage").Or.EqualTo("ClimbingPassage"));

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        private static void InvokePrivate(object obj, string method)
        {
            MethodInfo m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{method} should exist.");
            m.Invoke(obj, null);
        }

        private static string GetState(object enemy)
        {
            FieldInfo f = enemy.GetType().GetField("_moveState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null);
            return f.GetValue(enemy).ToString();
        }
    }
}
