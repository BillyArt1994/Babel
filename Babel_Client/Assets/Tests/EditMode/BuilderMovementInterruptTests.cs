using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    /// <summary>
    /// 测试 BuilderMovement：工人移动途中目标点被他人建完时的行为。
    /// 预期：立即放弃该目标、不扣 charge、重选目标。
    /// </summary>
    public class BuilderMovementInterruptTests
    {
        // ── 场景对象 ──
        private GameObject _pathGo;
        private Path _path;
        private GameObject[] _bpGos;
        private BuildPoint[] _bps;
        private GameObject _enemyGo;
        private Enemy _enemy;

        [SetUp]
        public void SetUp()
        {
            // 建 Path
            _pathGo = new GameObject("TestPath");
            _path = _pathGo.AddComponent<Path>();

            // 建两个 BuildPoint：bp0(x=0)，bp1(x=10)
            _bpGos = new GameObject[2];
            _bps = new BuildPoint[2];
            for (int i = 0; i < 2; i++)
            {
                _bpGos[i] = new GameObject($"BP{i}");
                _bpGos[i].transform.position = new Vector3(i * 10f, 0f, 0f);
                _bps[i] = _bpGos[i].AddComponent<BuildPoint>();
                _bps[i].OwnerPath = _path;
            }
            _path.wayPointList = _bps;

            // 建 Enemy（位置在 x=-5，不在任何 BP 上，MoveSpeed 极小确保不能一帧到达）
            _enemyGo = new GameObject("TestBuilder");
            _enemyGo.transform.position = new Vector3(-5f, 0f, 0f);
            _enemy = _enemyGo.AddComponent<Enemy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_enemyGo);
            Object.DestroyImmediate(_pathGo);
            for (int i = 0; i < _bpGos.Length; i++)
                Object.DestroyImmediate(_bpGos[i]);
        }

        // ── 反射助手（与 ScoutTargetingTests 一致）──

        private static object GetMovement(Enemy enemy)
        {
            FieldInfo mf = typeof(Enemy).GetField("_movement",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mf, Is.Not.Null, "Enemy._movement should exist.");
            object movement = mf.GetValue(enemy);
            Assert.That(movement, Is.Not.Null, "_movement should be initialized after Init.");
            return movement;
        }

        private static int GetInt(object movement, string fieldName)
        {
            FieldInfo f = movement.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"{fieldName} should exist on movement.");
            return (int)f.GetValue(movement);
        }

        private static string GetState(Enemy enemy)
        {
            object movement = GetMovement(enemy);
            FieldInfo f = movement.GetType().GetField("_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "_state should exist on movement.");
            return f.GetValue(movement).ToString();
        }

        private static void Tick(Enemy enemy, float dt)
        {
            object movement = GetMovement(enemy);
            MethodInfo tick = movement.GetType().GetMethod("Tick",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null, "Tick should exist on movement.");
            tick.Invoke(movement, new object[] { dt });
        }

        // ── 测试 1（RED）：移动途中目标被建完 → 不进入 Building 状态，不扣 charge ──

        [Test]
        public void Builder_WhileMoving_TargetCompletedByOther_DoesNotEnterBuilding()
        {
            var data = new EnemyData
            {
                Hp = 30,
                MoveSpeed = 0.1f,   // 极慢，确保一帧内到不了目标
                BuildContribution = 25,
                BuildCharges = 2,
                MoveMode = "builder",
                BuildTime = 1f
            };
            _enemy.Init(_path, data, -1);

            object movement = GetMovement(_enemy);
            int initialTarget = GetInt(movement, "_targetBuildPointIndex");
            int initialCharges = GetInt(movement, "_buildChargesLeft");

            // 前提：Init 后应在 MovingToBuildPoint 状态、已预约某个点
            Assert.That(GetState(_enemy), Is.EqualTo("MovingToBuildPoint"),
                "Init 后应处于 MovingToBuildPoint 状态");
            Assert.That(initialTarget, Is.GreaterThanOrEqualTo(0),
                "Init 后应已预约到某个 BuildPoint");

            // 把初始预约到的那个 BuildPoint 建完（模拟被他人建完）
            _bps[initialTarget].AddBuildProgress(99999);
            Assert.That(_bps[initialTarget].IsBuildCompleted, Is.True,
                "目标点应已被建完");

            // Tick 一次（此时 enemy 仍在移动途中，未到达目标）
            Tick(_enemy, 0.016f);

            // 断言：不应进入 Building 状态
            string stateAfterTick = GetState(_enemy);
            Assert.That(stateAfterTick, Is.Not.EqualTo("Building"),
                "目标点已完成时，移动途中不应进入 Building 状态");

            // 断言：charge 不应被扣减
            int chargesAfterTick = GetInt(GetMovement(_enemy), "_buildChargesLeft");
            Assert.That(chargesAfterTick, Is.EqualTo(initialCharges),
                "目标点已完成时放弃目标不应扣减 charge");
        }

        [Test]
        public void Builder_WhileMoving_TargetCompletedByOther_TargetIndexReset()
        {
            var data = new EnemyData
            {
                Hp = 30,
                MoveSpeed = 0.1f,
                BuildContribution = 25,
                BuildCharges = 2,
                MoveMode = "builder",
                BuildTime = 1f
            };
            _enemy.Init(_path, data, -1);

            object movement = GetMovement(_enemy);
            int initialTarget = GetInt(movement, "_targetBuildPointIndex");

            // 把目标 BP 建完
            _bps[initialTarget].AddBuildProgress(99999);

            // Tick 一次
            Tick(_enemy, 0.016f);

            // 断言：_targetBuildPointIndex 不再是已完成的那个点
            int targetAfterTick = GetInt(GetMovement(_enemy), "_targetBuildPointIndex");
            Assert.That(targetAfterTick, Is.Not.EqualTo(initialTarget),
                "目标点已完成后，_targetBuildPointIndex 应重置而不是继续指向已完成的点");
        }

        // ── 测试 2（正常路径不破坏）：工人正常走到未完成点，照常建造并扣 charge ──

        [Test]
        public void Builder_NormalPath_WalksToPoint_EntersBuilding_DeductsCharge()
        {
            // 把 bp0 建完，只留 bp1 供预约
            _bps[0].AddBuildProgress(99999);

            var data = new EnemyData
            {
                Hp = 30,
                MoveSpeed = 100f,   // 极快，一帧内到达目标
                BuildContribution = 25,
                BuildCharges = 2,
                MoveMode = "builder",
                BuildTime = 0.5f
            };
            // Enemy 位置放在 bp1 附近（x=9），方便一帧到达 bp1(x=10)
            _enemyGo.transform.position = new Vector3(9f, 0f, 0f);
            _enemy.Init(_path, data, -1);

            object movement = GetMovement(_enemy);
            int target = GetInt(movement, "_targetBuildPointIndex");
            Assert.That(target, Is.EqualTo(1), "应预约到 bp1（唯一未完成点）");

            // bp1 未完成，Tick 一帧（极快，直接到达 bp1）
            Tick(_enemy, 1f);

            // 应进入 Building 状态（正常到达）
            Assert.That(GetState(_enemy), Is.EqualTo("Building"),
                "正常到达未完成点后应进入 Building 状态");
        }
    }
}
