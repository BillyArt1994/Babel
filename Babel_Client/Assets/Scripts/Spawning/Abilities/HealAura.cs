using UnityEngine;

namespace Babel
{
    public class HealAura : IEnemyAbility
    {
        private Enemy _owner;
        private float _radius;
        private float _healAmount;
        private float _cooldown;
        private float _cdTimer;

        private static readonly Collider2D[] _buffer = new Collider2D[64];
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _radius = data.AbilityRadius;
            _healAmount = data.AbilityValue;
            _cooldown = data.AbilityCooldown;
            _cdTimer = _cooldown;
        }

        public void Tick(float deltaTime)
        {
            _cdTimer -= deltaTime;
            if (_cdTimer > 0) return;
            _cdTimer = _cooldown;

            int count = Physics2D.OverlapCircleNonAlloc(_owner.Position, _radius, _buffer, EnemyLayerMask);
            for (int i = 0; i < count; i++)
            {
                if (_buffer[i].TryGetComponent<Enemy>(out var enemy) && enemy != _owner && enemy.IsAlive)
                {
                    enemy.Heal(_healAmount);
                }
            }
        }

        public void OnRemoved() { }
    }
}
