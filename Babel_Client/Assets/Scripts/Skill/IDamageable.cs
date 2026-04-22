using UnityEngine;

namespace Babel
{
    public interface IDamageable
    {
        void TakeDamage(float damage, bool isCrit);
        Vector2 Position { get; }
        bool IsAlive { get; }
    }
}
