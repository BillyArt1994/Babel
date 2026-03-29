using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Babel/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [SerializeField] private SkillData[] _allSkills;

    public SkillData[] AllSkills => _allSkills;

    public SkillData GetById(string skillId)
    {
        if (_allSkills == null || string.IsNullOrEmpty(skillId))
        {
            return null;
        }

        for (int i = 0; i < _allSkills.Length; i++)
        {
            SkillData skill = _allSkills[i];
            if (skill != null && skill.SkillId == skillId)
            {
                return skill;
            }
        }

        return null;
    }

    public SkillData[] GetStarterSkills()
    {
        if (_allSkills == null)
        {
            return new SkillData[0];
        }

        return _allSkills.Where(skill => skill != null && skill.IsStarterSkill).ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_allSkills == null)
        {
            return;
        }

        Dictionary<string, List<SkillData>> skillsById = new Dictionary<string, List<SkillData>>();

        for (int i = 0; i < _allSkills.Length; i++)
        {
            SkillData skill = _allSkills[i];
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
            {
                continue;
            }

            if (!skillsById.TryGetValue(skill.SkillId, out List<SkillData> duplicates))
            {
                duplicates = new List<SkillData>();
                skillsById.Add(skill.SkillId, duplicates);
            }

            duplicates.Add(skill);
        }

        foreach (KeyValuePair<string, List<SkillData>> pair in skillsById)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            for (int i = 0; i < pair.Value.Count; i++)
            {
                SkillData duplicateSkill = pair.Value[i];
                Debug.LogWarning(
                    $"SkillDatabase '{name}' contains duplicate skillId '{pair.Key}' on skill '{duplicateSkill.name}'.",
                    duplicateSkill);
            }
        }
    }
#endif
}
