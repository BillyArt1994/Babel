using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "New Skill", menuName = "Babel/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique key used by code systems, for example \"god_finger\".")]
    [SerializeField] private string _skillId;

    [Tooltip("Display name shown to players in UI.")]
    [SerializeField] private string _skillName;

    [Tooltip("Description text shown in upgrade selection and other UI.")]
    [TextArea]
    [SerializeField] private string _description;

    [Tooltip("Skill icon displayed in HUD and upgrade UI.")]
    [SerializeField] private Sprite _icon;

    [Header("Classification")]
    [Tooltip("Whether this skill is an active click-form skill or a passive upgrade.")]
    [SerializeField] private SkillType _skillType;

    [Tooltip("Passive subtype. Only relevant when Skill Type is Passive.")]
    [SerializeField] private PassiveCategory _passiveCategory;

    [Header("Combat (ClickForm)")]
    [Tooltip("Base damage for ClickForm skills, or per-stack bonus value for Power passives.")]
    [SerializeField] private float _damage;

    [Tooltip("Additional damage per stack beyond the first for stacking Power passives.")]
    [SerializeField] private float _bonusDamagePerStack;

    [Tooltip("Seconds between uses. Set to 0 for passives.")]
    [SerializeField] private float _cooldown;

    [Tooltip("0 means instant. Values above 0 require holding to reach full charge.")]
    [SerializeField] private float _chargeTime;

    [Tooltip("Area-of-effect radius in Unity units.")]
    [SerializeField] private float _aoeRadius;

    [Tooltip("Number of chain jumps for chain-style attacks.")]
    [SerializeField] private int _chainCount;

    [Tooltip("Whether the attack pierces through enemies.")]
    [SerializeField] private bool _piercing;

    [Tooltip("Slow amount expressed as 0-1.")]
    [SerializeField] private float _slowPercent;

    [Tooltip("Duration of the slow effect in seconds.")]
    [SerializeField] private float _slowDuration;

    [Header("Passive / Auto")]
    [Tooltip("Seconds between automatic shots for AutoAttack passives.")]
    [SerializeField] private float _triggerInterval;

    [Tooltip("Maximum stack count. 1 means no stacking, 0 means unlimited.")]
    [SerializeField] private int _maxStackCount = 1;

    [Tooltip("Whether this passive spreads on kill, for plague-style effects.")]
    [SerializeField] private bool _spreadOnKill;

    [Header("Upgrade Pool")]
    [Tooltip("Higher values make this skill more likely to appear in the upgrade pool.")]
    [SerializeField] private float _weight = 1f;

    [Tooltip("Prerequisite skill required before this skill can appear. Null means always available.")]
    [SerializeField] private SkillData _upgradesFrom;

    [Tooltip("Whether this skill can appear in the initial starter skill selection.")]
    [SerializeField] private bool _isStarterSkill;

    public string SkillId => _skillId;
    public string SkillName => _skillName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public SkillType SkillType => _skillType;
    public PassiveCategory PassiveCategory => _passiveCategory;
    public float Damage => _damage;
    public float BonusDamagePerStack => _bonusDamagePerStack;
    public float Cooldown => _cooldown;
    public float ChargeTime => _chargeTime;
    public float AoeRadius => _aoeRadius;
    public int ChainCount => _chainCount;
    public bool Piercing => _piercing;
    public float SlowPercent => _slowPercent;
    public float SlowDuration => _slowDuration;
    public float TriggerInterval => _triggerInterval;
    public int MaxStackCount => _maxStackCount;
    public bool SpreadOnKill => _spreadOnKill;
    public float Weight => _weight;
    public SkillData UpgradesFrom => _upgradesFrom;
    public bool IsStarterSkill => _isStarterSkill;

    private void OnValidate()
    {
        _slowPercent = Mathf.Clamp(_slowPercent, 0f, 0.99f);
        _weight = Mathf.Max(0f, _weight);
        _maxStackCount = Mathf.Max(0, _maxStackCount);
        _damage = Mathf.Max(0f, _damage);
        _cooldown = Mathf.Max(0f, _cooldown);

        if (_skillType == SkillType.ClickForm)
        {
            _triggerInterval = 0f;
            _maxStackCount = 1;
            _spreadOnKill = false;
            _passiveCategory = PassiveCategory.Frequency;
        }
        else if (_passiveCategory != PassiveCategory.AutoAttack)
        {
            _triggerInterval = 0f;
        }

#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(_skillId))
        {
            Debug.LogWarning($"SkillData '{name}' has an empty skillId.", this);
        }

        if (_weight <= 0f && _skillType != SkillType.ClickForm)
        {
            Debug.LogWarning($"SkillData '{name}' has weight <= 0 and will not appear in weighted upgrade rolls.", this);
        }

        ValidateDuplicateSkillIds();
        ValidateUpgradeChain();
#endif
    }

#if UNITY_EDITOR
    private void ValidateDuplicateSkillIds()
    {
        if (string.IsNullOrWhiteSpace(_skillId))
        {
            return;
        }

        string[] assetGuids = AssetDatabase.FindAssets("t:SkillData");
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            SkillData otherSkill = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);

            if (otherSkill == null || otherSkill == this)
            {
                continue;
            }

            if (_skillId == otherSkill.SkillId)
            {
                Debug.LogWarning(
                    $"Duplicate skillId '{_skillId}' found on SkillData assets '{name}' and '{otherSkill.name}'.",
                    this);
            }
        }
    }

    private void ValidateUpgradeChain()
    {
        if (_upgradesFrom == null)
        {
            return;
        }

        HashSet<SkillData> visited = new HashSet<SkillData>();
        SkillData current = _upgradesFrom;

        while (current != null)
        {
            if (current == this || !visited.Add(current))
            {
                Debug.LogWarning(
                    $"SkillData '{name}' has an invalid upgradesFrom chain that creates a cycle. Clearing the reference.",
                    this);
                _upgradesFrom = null;
                EditorUtility.SetDirty(this);
                return;
            }

            current = current.UpgradesFrom;
        }
    }
#endif
}
