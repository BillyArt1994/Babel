using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the right-side skill list.
/// Shows skill name; for stackable passives shows "Name x2" etc.
/// </summary>
public class SkillEntryUI : MonoBehaviour
{
    [SerializeField] private Text _label;

    public SkillData SkillData { get; private set; }
    private int _stacks = 1;

    public void SetSkill(SkillData skill)
    {
        SkillData = skill;
        _stacks = 1;
        Refresh();
    }

    public void IncrementStack()
    {
        _stacks++;
        Refresh();
    }

    private void Refresh()
    {
        if (_label == null || SkillData == null) return;
        _label.text = _stacks > 1
            ? $"{SkillData.SkillName} x{_stacks}"
            : SkillData.SkillName;
    }
}
