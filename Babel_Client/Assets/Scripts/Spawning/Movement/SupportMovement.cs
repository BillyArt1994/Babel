using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 支援型移动策略：感知半径内有队友则追质心，无队友则朝 gateway 方向移动可爬楼，
    /// 顶层待命。不进行建造，不扣 buildCharges。
    /// </summary>
    public class SupportMovement : IEnemyMovement
    {
        private const float DEAD_ZONE = 1.0f;

        private Enemy _owner;
        private EnemyData _data;
        private float _senseRadius;

        // NonAlloc buffer（静态，节省 GC 压力）
        private static readonly Collider2D[] _senseBuffer = new Collider2D[32];
        private static readonly int EnemyMask = LayerMask.GetMask("Enemy");

        public bool IsMoving { get; private set; }

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _data = data;
            _senseRadius = data.SenseRadius > 0f ? data.SenseRadius : 8f;
            IsMoving = false;
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null) return;

            // 1. 计算队友质心（排除自身）
            Vector2 centroid;
            bool hasFriends = TryGetFriendCentroid(out centroid);

            if (hasFriends)
            {
                float dx = centroid.x - _owner.transform.position.x;
                if (Mathf.Abs(dx) <= DEAD_ZONE)
                {
                    // 死区内：停止
                    IsMoving = false;
                    return;
                }

                IsMoving = true;
                float step = _owner.EffectiveSpeed * deltaTime;
                float newX = _owner.transform.position.x + Mathf.Sign(dx) * Mathf.Min(step, Mathf.Abs(dx));
                _owner.transform.position = new Vector3(
                    newX,
                    _owner.transform.position.y,
                    _owner.transform.position.z);
                return;
            }

            // 2. 无队友：朝 gateway 走（若有上层则可爬梯）
            var path = _owner.currentPath;
            if (path == null)
            {
                IsMoving = false;
                return;
            }

            // 顶层（nextLayerPath=null）：待命
            if (path.nextLayerPath == null)
            {
                IsMoving = false;
                return;
            }

            // 朝 gateway x 坐标移动
            int gwIdx = path.GetGatewayIndex();
            if (gwIdx < 0 || gwIdx >= path.wayPointList.Length)
            {
                IsMoving = false;
                return;
            }

            float gatewayX = path.wayPointList[gwIdx].transform.position.x;
            float dxGw = gatewayX - _owner.transform.position.x;

            if (Mathf.Abs(dxGw) <= 0.1f)
            {
                // 到达 gateway x 位置：若 gateway 已建好则爬梯
                if (path.IsGatewayBuilt())
                {
                    ClimbToNextLayer();
                }
                IsMoving = false;
                return;
            }

            IsMoving = true;
            float stepGw = _owner.EffectiveSpeed * deltaTime;
            float newXGw = _owner.transform.position.x
                + Mathf.Sign(dxGw) * Mathf.Min(stepGw, Mathf.Abs(dxGw));
            _owner.transform.position = new Vector3(
                newXGw,
                _owner.transform.position.y,
                _owner.transform.position.z);
        }

        public void OnRemoved() { /* 无预约需释放 */ }

        // ── 私有 ─────────────────────────────────────────────────────────────
        private bool TryGetFriendCentroid(out Vector2 centroid)
        {
            centroid = Vector2.zero;
            int count = Physics2D.OverlapCircleNonAlloc(
                _owner.Position, _senseRadius, _senseBuffer, EnemyMask);

            float sumX = 0f;
            int friendCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (_senseBuffer[i] == null) continue;
                if (!_senseBuffer[i].TryGetComponent<Enemy>(out var e)) continue;
                if (e == _owner) continue;
                if (!e.IsAlive) continue;
                sumX += e.transform.position.x;
                friendCount++;
            }

            if (friendCount == 0) return false;
            centroid = new Vector2(sumX / friendCount, _owner.transform.position.y);
            return true;
        }

        private void ClimbToNextLayer()
        {
            var next = _owner.currentPath.nextLayerPath;
            if (next == null) return;
            _owner.currentPath = next;
            if (next.wayPointList != null && next.wayPointList.Length > 0)
                _owner.transform.position = next.wayPointList[0].transform.position;
        }
    }
}
