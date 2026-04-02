using System;
using UnityEngine;

public static class SkillEvents
{
    // Fired when a skill is added (for HUD update)
    public static event Action<SkillData> OnSkillAdded;
    // Fired when the active click-form skill changes
    public static event Action<SkillData> OnClickFormChanged;
    // Fired on charge start (for particle FX)
    public static event Action<Vector2> OnChargeStarted;
    // Fired each frame while charging (for particle FX intensity)
    public static event Action<Vector2, float> OnChargeUpdated;

    public static void RaiseSkillAdded(SkillData skill) => OnSkillAdded?.Invoke(skill);
    public static void RaiseClickFormChanged(SkillData skill) => OnClickFormChanged?.Invoke(skill);
    public static void RaiseChargeStarted(Vector2 worldPos) => OnChargeStarted?.Invoke(worldPos);
    public static void RaiseChargeUpdated(Vector2 worldPos, float chargeRatio) => OnChargeUpdated?.Invoke(worldPos, chargeRatio);
}
