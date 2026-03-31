#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility allowing AI/automation to select an upgrade option during LevelingUp state.
/// Menu: Babel/AI Select Upgrade/Option 0 (1 / 2)
/// Also exposes a static method for programmatic use.
/// </summary>
public static class AIUpgradeSelector
{
    [MenuItem("Babel/AI Select Upgrade/Option 0 (first)")]
    public static void SelectOption0() => Select(0);

    [MenuItem("Babel/AI Select Upgrade/Option 1 (second)")]
    public static void SelectOption1() => Select(1);

    [MenuItem("Babel/AI Select Upgrade/Option 2 (third)")]
    public static void SelectOption2() => Select(2);

    [MenuItem("Babel/AI Select Upgrade/Auto (pick first available)")]
    public static void SelectAuto()
    {
        if (UpgradeSystem.Instance == null)
        {
            Debug.LogWarning("[AIUpgradeSelector] UpgradeSystem.Instance is null.");
            return;
        }
        Select(0);
    }

    // ── Validation — grey out menus when not in LevelingUp state ──────────────

    [MenuItem("Babel/AI Select Upgrade/Option 0 (first)", true)]
    [MenuItem("Babel/AI Select Upgrade/Option 1 (second)", true)]
    [MenuItem("Babel/AI Select Upgrade/Option 2 (third)", true)]
    [MenuItem("Babel/AI Select Upgrade/Auto (pick first available)", true)]
    private static bool ValidateSelect()
    {
        return Application.isPlaying
               && GameLoopManager.Instance != null
               && GameLoopManager.Instance.GetCurrentState() == GameState.LevelingUp
               && UpgradeSystem.Instance != null;
    }

    private static void Select(int index)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIUpgradeSelector] Must be in Play Mode.");
            return;
        }
        if (GameLoopManager.Instance == null || UpgradeSystem.Instance == null)
        {
            Debug.LogWarning("[AIUpgradeSelector] GameLoopManager or UpgradeSystem not found.");
            return;
        }
        if (GameLoopManager.Instance.GetCurrentState() != GameState.LevelingUp)
        {
            Debug.LogWarning("[AIUpgradeSelector] Not in LevelingUp state. Current: "
                             + GameLoopManager.Instance.GetCurrentState());
            return;
        }

        UpgradeSystem.Instance.SelectOption(index);
        Debug.Log($"[AIUpgradeSelector] Selected upgrade option {index}.");
    }
}
#endif
