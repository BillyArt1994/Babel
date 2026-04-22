using UnityEngine;

namespace Babel
{
    public struct TriggerContext
    {
        public Vector2 WorldPos;
        public float ChargeRatio;
        public IDamageable Target;
        public bool IsPassive;
    }

    public struct AttackResult
    {
        public Vector2 WorldPos;
        public IDamageable Target;
        public bool IsPassive;
        public int HitCount;
    }
}
