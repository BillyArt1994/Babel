using System.Collections.Generic;
using UnityEngine;
using QFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Babel
{
    public partial class SkillSystem : ViewController
    {
        private const string SKILLS_CSV_ASSET_PATH = "Assets/Data/Skills/skills.csv";
        private const string DEFAULT_CLICK_SKILL_ID = "divine_finger";

        [Header("技能配置")]
        [SerializeField]
        [Tooltip("技能 CSV 数据源。未手动指定时，Editor 下自动读取 Assets/Data/Skills/skills.csv。")]
        private TextAsset skillsCSV;

        [SerializeField]
        [Tooltip("游戏开始时自动装备的默认点击技能 ID。")]
        private string defaultClickSkillId = DEFAULT_CLICK_SKILL_ID;

        private readonly List<Skill> _skills = new();
        private readonly EffectManager _effectManager = new();
        private bool _enabled;

        /// <summary>
        /// 当前场景中的技能系统实例，供 HUD 读取只读冷却状态。
        /// </summary>
        public static SkillSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Instance = this;
            _enabled = true;
            InitializeDefaultSkills();
        }

        private void Update()
        {
            if (!_enabled) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Tick(dt);
            }
            _effectManager.Tick(dt);
        }

        public void AddSkill(SkillConfig config)
        {
            var skill = SkillFactory.Create(config, _effectManager, GetBasePosition);
            _skills.Add(skill);
            if (_enabled)
            {
                skill.Trigger.Enable();
            }

            RaiseEquippedSkillsChanged();
        }

        public void RemoveSkill(string skillId)
        {
            for (int i = _skills.Count - 1; i >= 0; i--)
            {
                if (_skills[i].Config.SkillId == skillId)
                {
                    _skills[i].Trigger.Disable();
                    _skills.RemoveAt(i);
                    RaiseEquippedSkillsChanged();
                    return;
                }
            }
        }

        public IReadOnlyList<Skill> GetEquippedSkills() => _skills;

        /// <summary>
        /// 获取当前装备技能的配置列表。
        /// </summary>
        /// <returns>当前装备技能配置列表。</returns>
        public IReadOnlyList<SkillConfig> GetEquippedSkillsAsConfigs()
        {
            var configs = new List<SkillConfig>(_skills.Count);
            for (int i = 0; i < _skills.Count; i++)
            {
                configs.Add(_skills[i].Config);
            }

            return configs;
        }

        /// <summary>
        /// 获取当前装备的点击触发技能配置。
        /// </summary>
        /// <returns>当前点击技能；未装备时返回 null。</returns>
        public SkillConfig GetActiveClickSkill()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.TriggerType == "OnClick")
                {
                    return _skills[i].Config;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取当前装备的被动技能配置列表。
        /// </summary>
        /// <returns>所有非点击触发技能。</returns>
        public IReadOnlyList<SkillConfig> GetPassiveSkills()
        {
            var passiveSkills = new List<SkillConfig>();
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.TriggerType != "OnClick")
                {
                    passiveSkills.Add(_skills[i].Config);
                }
            }

            return passiveSkills;
        }

        /// <summary>
        /// 查询当前是否已经装备指定技能。
        /// </summary>
        /// <param name="skillId">技能唯一标识。</param>
        /// <returns>已装备返回 true，否则返回 false。</returns>
        public bool HasSkill(string skillId)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.SkillId == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 添加技能；若新技能是点击触发形态，则替换当前点击形态。
        /// </summary>
        /// <param name="config">要添加或替换的技能配置。</param>
        public void AddOrReplaceSkill(SkillConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BABEL][SkillSystem] Cannot add null skill config");
                return;
            }

            if (config.TriggerType == "OnClick")
            {
                RemoveAllOnClickSkillsExcept(config.SkillId);
            }

            if (!HasSkill(config.SkillId))
            {
                AddSkill(config);
            }
        }

        public float GetCooldownProgress(string skillId)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.SkillId == skillId &&
                    _skills[i].Trigger is OnClickTrigger clickTrigger)
                {
                    return clickTrigger.CooldownProgress;
                }
            }
            return 0f;
        }

        /// <summary>
        /// 获取当前点击技能冷却填充比例；1 表示刚进入冷却，0 表示可用。
        /// </summary>
        /// <returns>当前点击技能冷却进度。</returns>
        public float GetActiveClickCooldownProgress()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Trigger is OnClickTrigger clickTrigger)
                {
                    return clickTrigger.CooldownProgress;
                }
            }

            return 0f;
        }

        /// <summary>
        /// 清空所有点击技能冷却，避免升级暂停结束后旧冷却阻挡下一次攻击。
        /// </summary>
        public void ResetClickCooldowns()
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Trigger is OnClickTrigger clickTrigger)
                {
                    clickTrigger.ResetCooldown();
                }
            }
        }

        public void EnableAll()
        {
            _enabled = true;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Enable();
            }
        }

        public void DisableAll()
        {
            _enabled = false;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Disable();
            }
        }

        public void ClearAll()
        {
            DisableAll();
            _skills.Clear();
            _effectManager.ClearAll();
            RaiseEquippedSkillsChanged();
        }

        private Vector2 GetBasePosition()
        {
            return (Vector2)transform.position;
        }

        private void InitializeDefaultSkills()
        {
            ResolveMissingReferences();
            if (!ValidateStartupReferences())
            {
                return;
            }

            SkillDatabase.Init(skillsCSV.text);
            var defaultSkill = SkillDatabase.GetById(defaultClickSkillId);
            if (defaultSkill == null)
            {
                Debug.LogWarning($"[BABEL][SkillSystem] Missing default skill '{defaultClickSkillId}'");
                return;
            }

            if (!HasSkill(defaultSkill.SkillId))
            {
                AddSkill(defaultSkill);
            }
        }

        private void ResolveMissingReferences()
        {
#if UNITY_EDITOR
            if (skillsCSV == null)
            {
                skillsCSV = AssetDatabase.LoadAssetAtPath<TextAsset>(SKILLS_CSV_ASSET_PATH);
            }
#endif
        }

        private bool ValidateStartupReferences()
        {
            if (skillsCSV == null)
            {
                Debug.LogWarning("[BABEL][SkillSystem] No skills CSV assigned");
                return false;
            }

            if (string.IsNullOrWhiteSpace(defaultClickSkillId))
            {
                Debug.LogWarning("[BABEL][SkillSystem] No default click skill ID assigned");
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void RemoveAllOnClickSkillsExcept(string skillId)
        {
            for (int i = _skills.Count - 1; i >= 0; i--)
            {
                Skill skill = _skills[i];
                if (skill.Config.TriggerType == "OnClick" && skill.Config.SkillId != skillId)
                {
                    skill.Trigger.Disable();
                    _skills.RemoveAt(i);
                }
            }
        }

        private void RaiseEquippedSkillsChanged()
        {
            SkillEvents.RaiseEquippedSkillsChanged(GetEquippedSkillsAsConfigs());
        }
    }
}
