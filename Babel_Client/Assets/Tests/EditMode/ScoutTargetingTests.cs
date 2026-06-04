using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class ScoutTargetingTests
    {
        [Test]
        public void ScoutEnemy_OnInit_TargetsGatewayWhenAvailable()
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
            var data = new EnemyData
            {
                Hp = 25, MoveSpeed = 5, BuildContribution = 25,
                BuildCharges = 1, MoveMode = "scout", BuildTime = 1.2f
            };

            enemy.Init(path, data, -1);

            // 斥候用 GatewayFirstSelector，Init 内预约应锁定 gateway（index 1）
            int targetIndex = GetMovementInt(enemy, "_targetBuildPointIndex");
            Assert.That(targetIndex, Is.EqualTo(1), "斥候应优先预约 gateway（index 1）");

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
            // 全部点建完 → 无可预约点 + IsCompleted/IsGatewayBuilt 成立 → 应爬楼
            bps[0].AddBuildProgress(99999);
            bps[1].AddBuildProgress(99999);
            bps[2].AddBuildProgress(99999);

            var enemyGo = new GameObject("W");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData
            {
                Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                BuildCharges = 1, MoveMode = "", BuildTime = 1f
            };
            enemy.Init(path, data, -1);

            // 驱动 movement 一帧：无可预约点 + gateway built → 进入爬楼流程
            TickMovement(enemy, 0.1f);

            string state = GetMovementState(enemy);
            Assert.That(state, Is.EqualTo("MovingToPassage").Or.EqualTo("ClimbingPassage"));

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        // ── 反射助手：读取 Enemy._movement(BuilderMovement) 内部状态 ──
        private static object GetMovement(object enemy)
        {
            FieldInfo mf = enemy.GetType().GetField("_movement",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mf, Is.Not.Null, "Enemy._movement should exist.");
            object movement = mf.GetValue(enemy);
            Assert.That(movement, Is.Not.Null, "_movement should be initialized after Init.");
            return movement;
        }

        private static int GetMovementInt(object enemy, string fieldName)
        {
            object movement = GetMovement(enemy);
            FieldInfo f = movement.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"{fieldName} should exist on movement.");
            return (int)f.GetValue(movement);
        }

        private static string GetMovementState(object enemy)
        {
            object movement = GetMovement(enemy);
            FieldInfo f = movement.GetType().GetField("_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "_state should exist on movement.");
            return f.GetValue(movement).ToString();
        }

        private static void TickMovement(object enemy, float dt)
        {
            object movement = GetMovement(enemy);
            MethodInfo tick = movement.GetType().GetMethod("Tick",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null, "Tick should exist on movement.");
            tick.Invoke(movement, new object[] { dt });
        }

        [Test]
        public void BuilderMode_RoutesToBuilderMovement()
        {
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bpGo = new GameObject("BP"); bpGo.transform.position = Vector3.zero;
            var bp = bpGo.AddComponent<BuildPoint>(); bp.OwnerPath = path;
            path.wayPointList = new[] { bp };

            var enemyGo = new GameObject("W");
            var enemy = enemyGo.AddComponent<Enemy>();
            var data = new EnemyData
            {
                Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                BuildCharges = 1, MoveMode = "builder", BuildTime = 1f
            };
            enemy.Init(path, data, -1);

            var mf = typeof(Enemy).GetField("_movement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var movement = mf.GetValue(enemy);
            Assert.That(movement, Is.TypeOf<BuilderMovement>(),
                "moveMode=builder 应路由到 BuilderMovement");

            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(bpGo);
        }
    }
}
