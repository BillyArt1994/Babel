using System;

/// <summary>
/// Broadcasts combat execution results for downstream gameplay and presentation systems.
/// Implements the event contract from design/gdd/点击攻击系统.md.
/// </summary>
public static class CombatEvents
{
    public static event Action<AttackResult> OnAttackExecuted;

    public static void RaiseAttackExecuted(AttackResult result)
    {
        OnAttackExecuted?.Invoke(result);
    }
}
