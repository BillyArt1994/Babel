#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>
/// Editor-only: auto-diagnoses and triggers upgrade flow 2 s after start.
/// </summary>
public class DebugUpgradeShortcut : MonoBehaviour
{
    [SerializeField] private bool _autoTriggerOnStart = true;
    [SerializeField] private float _autoTriggerDelay  = 2f;

    private IEnumerator Start()
    {
        if (!_autoTriggerOnStart) yield break;
        yield return new WaitForSeconds(_autoTriggerDelay);

        // --- Diagnostic dump ---
        bool glmOk  = GameLoopManager.Instance != null;
        bool upgOk  = UpgradeSystem.Instance   != null;
        bool playing = glmOk && GameLoopManager.Instance.IsPlaying();
        float faith  = upgOk  ? UpgradeSystem.Instance.GetFaithProgress() : -1f;
        int   level  = upgOk  ? UpgradeSystem.Instance.GetLevelCount()    : -1;
        Debug.Log("[Debug] GLM=" + glmOk + " UPG=" + upgOk + " playing=" + playing
                  + " faithPct=" + faith.ToString("F2") + " level=" + level);

        if (!upgOk || !playing) yield break;

        EnemyData[] all = Resources.FindObjectsOfTypeAll<EnemyData>();
        Debug.Log("[Debug] EnemyData in memory: " + (all != null ? all.Length : 0));
        if (all == null || all.Length == 0) yield break;

        EnemyData data = all[0];
        Debug.Log("[Debug] Using EnemyData: " + data.name + " FaithValue=" + data.FaithValue);

        float perKill = Mathf.Max(data.FaithValue, 0.01f);
        int kills = Mathf.CeilToInt(9999f / perKill);
        for (int i = 0; i < kills; i++)
            EnemyEvents.RaiseEnemyDied(data, Vector2.zero);

        Debug.Log("[Debug] Injected 9999 faith via " + kills + " kills. State="
                  + (glmOk ? GameLoopManager.Instance.GetCurrentState().ToString() : "?")
                  + " level=" + (upgOk ? UpgradeSystem.Instance.GetLevelCount() : -1));
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F1) && !Input.GetKeyDown(KeyCode.F2)) return;
        if (UpgradeSystem.Instance == null || GameLoopManager.Instance == null) return;
        if (!GameLoopManager.Instance.IsPlaying()) return;

        float amount = Input.GetKeyDown(KeyCode.F1) ? 9999f : 5f;
        EnemyData[] all = Resources.FindObjectsOfTypeAll<EnemyData>();
        if (all == null || all.Length == 0) return;
        EnemyData data = all[0];
        float perKill = Mathf.Max(data.FaithValue, 0.01f);
        int kills = Mathf.CeilToInt(amount / perKill);
        for (int i = 0; i < kills; i++)
            EnemyEvents.RaiseEnemyDied(data, Vector2.zero);
        Debug.Log("[Debug] Injected " + amount + " faith");
    }
}
#endif
