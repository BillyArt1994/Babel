using UnityEngine;

namespace Babel
{
    public class SpeedAura : IEnemyAbility
    {
        private Enemy _owner;
        private float _radius;
        private float _speedMultiplier;
        private float _checkTimer;
        private const float CHECK_INTERVAL = 0.5f;
        private const float BUFF_DURATION = 0.6f;

        private static readonly Collider2D[] _buffer = new Collider2D[64];
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _radius = data.AbilityRadius;
            _speedMultiplier = data.AbilityValue;
        }

        public void Tick(float deltaTime)
        {
            _checkTimer -= deltaTime;
            if (_checkTimer > 0) return;
            _checkTimer = CHECK_INTERVAL;

            int count = Physics2D.OverlapCircleNonAlloc(_owner.Position, _radius, _buffer, EnemyLayerMask);
            for (int i = 0; i < count; i++)
            {
                if (_buffer[i].TryGetComponent<Enemy>(out var enemy) && enemy != _owner && enemy.IsAlive)
                {
                    enemy.ApplySpeedBuff(_speedMultiplier, BUFF_DURATION);
                }
            }
        }

        public void OnRemoved() { }
    }
}
