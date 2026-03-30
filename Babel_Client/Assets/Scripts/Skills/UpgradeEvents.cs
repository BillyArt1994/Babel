using System;

public static class UpgradeEvents
{
    // Fired when upgrade options are ready — UI subscribes to show cards
    public static event Action<SkillData[]> OnOptionsGenerated;

    public static void RaiseOptionsGenerated(SkillData[] options) =>
        OnOptionsGenerated?.Invoke(options);
}
