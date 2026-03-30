using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Implements the upgrade selection UI from design/gdd/升级选择UI.md.
/// Listens for UpgradeEvents.OnOptionsGenerated, shows 3 skill cards,
/// calls UpgradeSystem.SelectOption on click.
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Image _overlay;
    [SerializeField] private GameObject[] _cardRoots;       // 3 card GameObjects
    [SerializeField] private Image[] _cardIcons;
    [SerializeField] private Text[] _cardNames;
    [SerializeField] private Text[] _cardTypes;
    [SerializeField] private Text[] _cardDescriptions;
    [SerializeField] private Button[] _cardButtons;

    private SkillData[] _currentOptions;
    private bool _accepted;

    private void OnEnable()  => UpgradeEvents.OnOptionsGenerated += Show;
    private void OnDisable() => UpgradeEvents.OnOptionsGenerated -= Show;

    private void Start() => Hide();

    private void Show(SkillData[] options)
    {
        _currentOptions = options;
        _accepted = false;

        int count = options != null ? options.Length : 0;

        for (int i = 0; i < _cardRoots.Length; i++)
        {
            bool visible = i < count && options[i] != null;
            _cardRoots[i].SetActive(visible);

            if (!visible) continue;

            SkillData skill = options[i];

            if (_cardIcons[i] != null)
            {
                _cardIcons[i].sprite = skill.Icon;
                _cardIcons[i].enabled = skill.Icon != null;
            }

            if (_cardNames[i] != null)
                _cardNames[i].text = skill.SkillName;

            if (_cardTypes[i] != null)
                _cardTypes[i].text = skill.SkillType == SkillType.ClickForm ? "点击形态" : "被动增强";

            if (_cardDescriptions[i] != null)
            {
                int stacks = SkillSystem.Instance != null
                    ? SkillSystem.Instance.GetPassiveStacks(skill)
                    : 0;
                string prefix = stacks > 0 ? $"Lv{stacks} → Lv{stacks + 1}\n" : "";
                _cardDescriptions[i].text = prefix + skill.Description;
            }
        }

        _root.SetActive(true);
    }

    private void Hide()
    {
        _root.SetActive(false);
    }

    // Wired to each card button in Inspector (pass 0/1/2)
    public void OnCardClicked(int index)
    {
        if (_accepted) return;
        _accepted = true;

        Hide();

        if (UpgradeSystem.Instance != null)
            UpgradeSystem.Instance.SelectOption(index);
    }
}
