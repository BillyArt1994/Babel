using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the upgrade system from design/gdd/升级系统.md.
/// Accumulates faith from enemy kills, generates weighted 3-option pools,
/// and broadcasts UpgradeEvents.OnOptionsGenerated for the UI to display.
/// </summary>
public class UpgradeSystem : MonoBehaviour
{
    private const float FAITH_BASE = 10f;
    private const float FAITH_SCALE_FACTOR = 1.2f;
    private const int OPTIONS_COUNT = 3;

    public static UpgradeSystem Instance { get; private set; }

    [SerializeField] private SkillDatabase _skillDatabase;
    [SerializeField] private SkillSystem _skillSystem;

    private float _faithAccumulated;
    private float _faithThreshold;
    private int _levelCount;
    private SkillData[] _pendingOptions;

    public float GetFaithProgress() =>
        _faithThreshold > 0f ? Mathf.Clamp01(_faithAccumulated / _faithThreshold) : 0f;

    public int GetLevelCount() => _levelCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.OnGameStart += ResetFaith;
        EnemyEvents.OnEnemyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart -= ResetFaith;
        EnemyEvents.OnEnemyDied -= OnEnemyDied;
    }

    // Called by UpgradeSelectionUI when player picks a card
    public void SelectOption(int index)
    {
        if (_pendingOptions == null || index < 0 || index >= _pendingOptions.Length)
            return;

        SkillData chosen = _pendingOptions[index];
        _pendingOptions = null;

        if (_skillSystem != null && chosen != null)
            _skillSystem.AddSkill(chosen);

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.RequestLevelUpComplete();
    }

    private void ResetFaith()
    {
        _faithAccumulated = 0f;
        _faithThreshold = FAITH_BASE;
        _levelCount = 0;
        _pendingOptions = null;
    }

    private void OnEnemyDied(EnemyData data, Vector2 position)
    {
        if (data == null) return;
        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying()) return;

        _faithAccumulated += data.FaithValue;
        if (_faithAccumulated >= _faithThreshold)
            DoUpgrade();
    }

    private void DoUpgrade()
    {
        _faithAccumulated -= _faithThreshold;
        _faithThreshold = Mathf.Ceil(_faithThreshold * FAITH_SCALE_FACTOR);
        _levelCount++;

        SkillData[] options = BuildOptions(OPTIONS_COUNT);
        if (options == null || options.Length == 0)
            return;

        _pendingOptions = options;

        if (GameLoopManager.Instance != null)
            GameLoopManager.Instance.RequestLevelUp();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BabelLogger.Event(BabelLogger.Tags.Skill, string.Concat(
            "[UPGRADE_READY] level=", _levelCount.ToString(),
            " options=", _pendingOptions != null ? _pendingOptions.Length.ToString() : "0",
            " opt0=", (_pendingOptions != null && _pendingOptions.Length > 0 && _pendingOptions[0] != null) ? _pendingOptions[0].SkillName : "?",
            " opt1=", (_pendingOptions != null && _pendingOptions.Length > 1 && _pendingOptions[1] != null) ? _pendingOptions[1].SkillName : "?",
            " opt2=", (_pendingOptions != null && _pendingOptions.Length > 2 && _pendingOptions[2] != null) ? _pendingOptions[2].SkillName : "?"));
#endif
        UpgradeEvents.RaiseOptionsGenerated(_pendingOptions);
    }

    private SkillData[] BuildOptions(int count)
    {
        if (_skillDatabase == null || _skillDatabase.AllSkills == null)
            return null;

        List<SkillData> eligible = new List<SkillData>(_skillDatabase.AllSkills.Length);
        foreach (SkillData skill in _skillDatabase.AllSkills)
        {
            if (IsEligible(skill))
                eligible.Add(skill);
        }

        if (eligible.Count == 0) return null;

        int take = Mathf.Min(count, eligible.Count);
        SkillData[] result = new SkillData[take];

        for (int i = 0; i < take; i++)
        {
            float totalWeight = 0f;
            for (int k = 0; k < eligible.Count; k++)
                totalWeight += eligible[k].Weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int j = 0; j < eligible.Count; j++)
            {
                cumulative += eligible[j].Weight;
                if (roll <= cumulative)
                {
                    result[i] = eligible[j];
                    eligible.RemoveAt(j);
                    break;
                }
            }
        }

        return result;
    }

    private bool IsEligible(SkillData skill)
    {
        if (skill == null || skill.Weight <= 0f) return false;

        if (skill.UpgradesFrom != null &&
            (_skillSystem == null || !_skillSystem.HasSkill(skill.UpgradesFrom)))
            return false;

        if (skill.SkillType == SkillType.ClickForm)
            return _skillSystem == null || _skillSystem.GetActiveClickForm() != skill;

        if (skill.MaxStackCount > 0 && _skillSystem != null &&
            _skillSystem.GetPassiveStacks(skill) >= skill.MaxStackCount)
            return false;

        return true;
    }
}
