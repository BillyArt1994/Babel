using System.Collections.Generic;
using UnityEngine;

public class UpgradeSystemPlaceholder : MonoBehaviour
{
    private const float FAITH_BASE = 10f;
    private const float FAITH_SCALE_FACTOR = 1.2f;

    [SerializeField] private SkillDatabase _skillDatabase;
    [SerializeField] private SkillSystem _skillSystem;

    private float _faithAccumulated;
    private float _faithThreshold;
    private int _levelCount;

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

    private void ResetFaith()
    {
        _faithAccumulated = 0f;
        _faithThreshold = FAITH_BASE;
        _levelCount = 0;
    }

    private void OnEnemyDied(EnemyData data, Vector2 position)
    {
        if (data == null)
        {
            return;
        }

        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying())
        {
            return;
        }

        _faithAccumulated += data.FaithValue;

        if (_faithAccumulated >= _faithThreshold)
        {
            TriggerUpgrade();
        }
    }

    private void TriggerUpgrade()
    {
        _faithAccumulated -= _faithThreshold;
        _faithThreshold = Mathf.Ceil(_faithThreshold * FAITH_SCALE_FACTOR);
        _levelCount++;

        SkillData skill = GetRandomEligibleSkill();
        if (skill == null)
        {
            return;
        }

        AddSkill(skill);
        Debug.Log($"[UpgradePlaceholder] Level {_levelCount}: Added {skill.SkillName}");
    }

    private SkillData GetRandomEligibleSkill()
    {
        if (_skillDatabase == null || _skillDatabase.AllSkills == null || _skillDatabase.AllSkills.Length == 0)
        {
            return null;
        }

        List<SkillData> eligibleSkills = new List<SkillData>();

        for (int i = 0; i < _skillDatabase.AllSkills.Length; i++)
        {
            SkillData skill = _skillDatabase.AllSkills[i];
            if (!IsEligible(skill))
            {
                continue;
            }

            eligibleSkills.Add(skill);
        }

        if (eligibleSkills.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, eligibleSkills.Count);
        return eligibleSkills[randomIndex];
    }

    private bool IsEligible(SkillData skill)
    {
        if (skill == null || skill.Weight <= 0f)
        {
            return false;
        }

        if (skill.MaxStackCount <= 0)
        {
            return true;
        }

        if (skill.SkillType == SkillType.ClickForm)
        {
            return !HasSkill(skill);
        }

        return GetPassiveStacks(skill) < skill.MaxStackCount;
    }

    private void AddSkill(SkillData skill)
    {
        if (_skillSystem == null || skill == null) return;
        _skillSystem.AddSkill(skill);
    }

    private bool HasSkill(SkillData skill)
    {
        if (_skillSystem == null || skill == null) return false;
        return _skillSystem.HasSkill(skill);
    }

    private int GetPassiveStacks(SkillData skill)
    {
        if (_skillSystem == null || skill == null) return 0;
        return _skillSystem.GetPassiveStacks(skill);
    }
}
