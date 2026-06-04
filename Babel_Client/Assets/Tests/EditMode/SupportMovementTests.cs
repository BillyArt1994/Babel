using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class SupportMovementTests
    {
        private static (GameObject go, Enemy enemy) MakeEnemy(Vector3 pos)
        {
            var go = new GameObject("E");
            go.layer = LayerMask.NameToLayer("Enemy");
            go.transform.position = pos;
            // SupportMovement 靠 Physics2D.OverlapCircle 感知队友，需要 Collider2D 才能被探测到
            go.AddComponent<CircleCollider2D>().radius = 0.5f;
            var e = go.AddComponent<Enemy>();
            return (go, e);
        }

        private static (GameObject go, Path path, BuildPoint[] bps)
            MakePath(float[] xPositions, int gatewayIdx = -1)
        {
            var pathGo = new GameObject("Path");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[xPositions.Length];
            var bpGos = new GameObject[xPositions.Length];
            for (int i = 0; i < xPositions.Length; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(xPositions[i], 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
                bps[i].OwnerPath = path;
            }
            if (gatewayIdx >= 0 && gatewayIdx < bps.Length)
                bps[gatewayIdx].isGateway = true;
            path.wayPointList = bps;
            return (pathGo, path, bps);
        }

        [Test]
        public void SupportMovement_IsMoving_TrueWhenMovingTowardsFriends()
        {
            var (pathGo, path, bps) = MakePath(new[] { -10f, 5f, 20f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (friendGo, friendEnemy) = MakeEnemy(new Vector3(5f, 0f, 0f));
            friendEnemy.Init(path,
                new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                                BuildCharges = 1, MoveMode = "", BuildTime = 1f }, -1);

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 10f
            };
            suppEnemy.Init(path, suppData, -1);

            var movement = GetMovement(suppEnemy);
            Assert.That(movement, Is.Not.Null, "SupportMovement 应已绑定");
            movement.Tick(0.1f);

            Assert.That(movement.IsMoving, Is.True, "有队友在感知范围内时应处于移动状态");

            Object.DestroyImmediate(friendGo);
            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_NoFriends_WalksTowardsGateway()
        {
            var (pathGo, path, bps) = MakePath(new[] { -5f, 10f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float xAfter = suppEnemy.transform.position.x;

            Assert.That(xAfter, Is.GreaterThan(xBefore), "无队友时应向 gateway 方向移动");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_OnlyMovesHorizontally()
        {
            var (pathGo, path, bps) = MakePath(new[] { 5f, 15f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 2f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f
            };
            suppEnemy.Init(path, suppData, -1);

            float yBefore = suppEnemy.transform.position.y;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float yAfter = suppEnemy.transform.position.y;

            Assert.That(yAfter, Is.EqualTo(yBefore).Within(0.001f), "支援者不应改变 y 坐标");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_DeadZone_DoesNotMove()
        {
            var (pathGo, path, bps) = MakePath(new[] { 0f, 15f }, gatewayIdx: 1);
            var nextGo = new GameObject("Next");
            var nextPath = nextGo.AddComponent<Path>();
            var nextBpGo = new GameObject("NBP");
            nextPath.wayPointList = new[] { nextBpGo.AddComponent<BuildPoint>() };
            path.nextLayerPath = nextPath;

            var (friendGo, friendEnemy) = MakeEnemy(new Vector3(0.3f, 0f, 0f));
            friendEnemy.Init(path,
                new EnemyData { Hp = 30, MoveSpeed = 1, BuildContribution = 25,
                                BuildCharges = 1, MoveMode = "", BuildTime = 1f }, -1);

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 10f
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);
            movement.Tick(0.5f);
            float xAfter = suppEnemy.transform.position.x;

            Assert.That(Mathf.Abs(xAfter - xBefore), Is.LessThan(0.001f),
                "在死区内不应移动");

            Object.DestroyImmediate(friendGo);
            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            Object.DestroyImmediate(nextGo);
            Object.DestroyImmediate(nextBpGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        [Test]
        public void SupportMovement_TopLayer_Idles()
        {
            var (pathGo, path, bps) = MakePath(new[] { 5f, 15f }, gatewayIdx: 1);

            var (suppGo, suppEnemy) = MakeEnemy(new Vector3(0f, 0f, 0f));
            var suppData = new EnemyData
            {
                Hp = 60, MoveSpeed = 1.5f, BuildContribution = 0,
                BuildCharges = 0, MoveMode = "support", SenseRadius = 3f
            };
            suppEnemy.Init(path, suppData, -1);

            float xBefore = suppEnemy.transform.position.x;
            var movement = GetMovement(suppEnemy);

            Assert.DoesNotThrow(() => movement.Tick(0.5f));
            float xAfter = suppEnemy.transform.position.x;

            Assert.That(Mathf.Abs(xAfter - xBefore), Is.LessThan(0.001f),
                "顶层无队友时支援者应静止");

            Object.DestroyImmediate(suppGo);
            Object.DestroyImmediate(pathGo);
            foreach (var bp in bps) if (bp != null) Object.DestroyImmediate(bp.gameObject);
        }

        private static IEnemyMovement GetMovement(Enemy enemy)
        {
            var f = typeof(Enemy).GetField("_movement",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "Enemy._movement 字段应存在");
            return f.GetValue(enemy) as IEnemyMovement;
        }
    }
}
