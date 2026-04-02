using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UpgradeCardUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _typeText;
    [SerializeField] private Text _descText;

    private Button _button;

    public void Setup(SkillData skill, int index, UpgradeSelectionUI ui)
    {
        _button = GetComponent<Button>();

        if (_icon != null)
        {
            _icon.sprite = skill.Icon;
            _icon.enabled = skill.Icon != null;
        }

        if (_nameText != null)
            _nameText.text = skill.SkillName;

        if (_typeText != null)
            _typeText.text = skill.SkillType == SkillType.ClickForm ? "点击形态" : "被动增强";

        if (_descText != null)
        {
            int stacks = SkillSystem.Instance != null ? SkillSystem.Instance.GetPassiveStacks(skill) : 0;
            string prefix = stacks > 0 ? $"Lv{stacks} → Lv{stacks + 1}\n" : "";
            _descText.text = prefix + skill.Description;
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => ui.OnCardClicked(index));
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveAllListeners();
    }
}
