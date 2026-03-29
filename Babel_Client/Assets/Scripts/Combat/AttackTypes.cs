using UnityEngine;

/// <summary>
/// Implements the core data contracts from design/gdd/点击攻击系统.md.
/// </summary>
public enum AttackType
{
    Single,
    AOE,
    Chain,
    Pierce
}

public struct AttackRequest
{
    public Vector2 worldPos;
    public AttackType attackType;
    public float damage;
    public float radius;
    public int chainCount;
    public float chainRadius;
    public float chainDecay;
    public float slowPercent;
    public float slowDuration;
    public bool isPassiveAttack;
}

public struct HitInfo
{
    public EnemyController enemy;
    public Vector2 hitPosition;
    public float damageDone;
}

public struct AttackResult
{
    public AttackRequest request;
    public HitInfo[] hits;
}
