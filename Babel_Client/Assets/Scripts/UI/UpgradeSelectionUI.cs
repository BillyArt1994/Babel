using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Upgrade selection UI. Subscribes in Awake/OnDestroy so the listener
/// survives Hide() toggling _root inactive.
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Image _overlay;
    [SerializeField] private GameObject[] _cardRoots;
    [SerializeField] private Image[] _cardIcons;
    [SerializeField] private Text[] _cardNames;
    [SerializeField] private Text[] _cardTypes;
    [SerializeField] private Text[] _cardDescriptions;
    [SerializeField] private Button[] _cardButtons;

    private SkillData[] _currentOptions;
    private bool _accepted;

    private void Awake()
    {
        UpgradeEvents.OnOptionsGenerated += OnOptionsGenerated;
    }

    private void OnDestroy()
    {
        UpgradeEvents.OnOptionsGenerated -= OnOptionsGenerated;
    }

    private void Start()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void OnOptionsGenerated(SkillData[] options)
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

            if (_cardIcons != null && i < _cardIcons.Length && _cardIcons[i] != null)
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

        if (_root != null) _root.SetActive(true);
    }

    public void OnCardClicked(int index)
    {
        if (_accepted) return;
        _accepted = true;

        if (_root != null) _root.SetActive(false);

        if (UpgradeSystem.Instance != null)
            UpgradeSystem.Instance.SelectOption(index);
    }
}
