using System;
using System.Collections.Generic;
using Babel.Unity.Infrastructure.Time;
using UnityEngine;

namespace Babel
{
    public class UpgradeOption
    {
        public enum OptionType { NewSkill, LevelUpgrade }
        public OptionType Type;
        public SkillConfig Config;
    }

    /// <summary>
    /// Legacy option generator retained during WP1. RunFlow owns the ChoosingUpgrade phase;
    /// this component only generates options and applies the selected legacy skill.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeSystem : MonoBehaviour
    {
        private const int OPTIONS_COUNT = 3;

        [Header("升级配置")]
        [SerializeField]
        [Tooltip("负责实际装备技能的技能系统。")]
        private SkillSystem skillSystem;

        [SerializeField]
        [Tooltip("经验值与等级成长系统。")]
        private XpSystem xpSystem;

        private readonly List<UpgradeOption> _pendingOptions = new List<UpgradeOption>();

        public int PendingOptionCountForTests => _pendingOptions.Count;

        private void OnEnable()
        {
            UpgradeEvents.OnOptionSelected += SelectOption;
            if (xpSystem != null) xpSystem.OnLevelsGained += HandleLevelsGained;
        }

        private void OnDisable()
        {
            UpgradeEvents.OnOptionSelected -= SelectOption;
            if (xpSystem != null) xpSystem.OnLevelsGained -= HandleLevelsGained;
        }

        private void HandleLevelsGained(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_pendingOptions.Count > 0) break;

                _pendingOptions.Clear();
                IReadOnlyList<UpgradeOption> options = GenerateOptions(OPTIONS_COUNT);
                for (int j = 0; j < options.Count; j++) _pendingOptions.Add(options[j]);

                if (_pendingOptions.Count == 0)
                {
                    if (!LegacyRunBridge.IsAvailable) PresentationTimeScaleAdapter.ResetLegacy();
                    UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
                    continue;
                }

                var configList = new List<SkillConfig>(_pendingOptions.Count);
                for (int j = 0; j < _pendingOptions.Count; j++) configList.Add(_pendingOptions[j].Config);
                UpgradeEvents.RaiseOptionsGenerated(configList);

                if (LegacyRunBridge.IsAvailable)
                {
                    if (!LegacyRunBridge.TryBeginUpgradeChoice(this))
                    {
                        Debug.LogWarning("[Babel][UpgradeSystem] RunFlow rejected the upgrade-choice phase.");
                        _pendingOptions.Clear();
                        UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
                    }
                }
                else
                {
                    PresentationTimeScaleAdapter.FreezeLegacy();
                }
            }
        }

        public void SelectOption(int index)
        {
            if (!CanApplySelection(index)) return;

            if (LegacyRunBridge.IsAvailable)
            {
                if (!LegacyRunBridge.TryRequestUpgradeSelection(this, index))
                    Debug.LogWarning("[Babel][UpgradeSystem] RunFlow rejected the upgrade selection.");
                return;
            }

            if (ApplySelectedOptionFromRun(index))
                PresentationTimeScaleAdapter.ResetLegacy();
        }

        internal bool ApplySelectedOptionFromRun(int index)
        {
            if (!CanApplySelection(index)) return false;

            UpgradeOption selected = _pendingOptions[index];
            if (selected.Type == UpgradeOption.OptionType.LevelUpgrade)
                skillSystem.UpgradeSkill(selected.Config);
            else
                skillSystem.AddOrReplaceSkill(selected.Config);

            skillSystem.ResetClickCooldowns();
            _pendingOptions.Clear();
            UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
            return true;
        }

        public void SetSkillSystemForTests(SkillSystem system)
        {
            skillSystem = system;
        }

        public IReadOnlyList<UpgradeOption> GenerateOptionsForTests(int count)
        {
            return GenerateOptions(count);
        }

        public void SetPendingOptionsForTests(IReadOnlyList<UpgradeOption> options)
        {
            _pendingOptions.Clear();
            if (options == null) return;
            for (int i = 0; i < options.Count; i++) _pendingOptions.Add(options[i]);
        }

        private bool CanApplySelection(int index)
        {
            if (index < 0 || index >= _pendingOptions.Count)
            {
                Debug.LogWarning("[Babel][UpgradeSystem] Invalid upgrade option index " + index);
                return false;
            }

            if (skillSystem == null)
            {
                Debug.LogWarning("[Babel][UpgradeSystem] No SkillSystem assigned");
                return false;
            }

            return true;
        }

        private IReadOnlyList<UpgradeOption> GenerateOptions(int count)
        {
            var pool = BuildEligiblePool();
            var selected = new List<UpgradeOption>(Mathf.Min(count, pool.Count));
            while (selected.Count < count && pool.Count > 0)
            {
                int index = RollWeightedIndex(pool);
                selected.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return selected;
        }

        private List<UpgradeOption> BuildEligiblePool()
        {
            var pool = new List<UpgradeOption>();

            IReadOnlyList<SkillConfig> allSkills = SkillDatabase.GetAll();
            for (int i = 0; i < allSkills.Count; i++)
            {
                if (IsEligibleNewSkill(allSkills[i]))
                    pool.Add(new UpgradeOption { Type = UpgradeOption.OptionType.NewSkill, Config = allSkills[i] });
            }

            if (skillSystem != null)
            {
                IReadOnlyList<Skill> equipped = skillSystem.GetEquippedSkills();
                for (int i = 0; i < equipped.Count; i++)
                {
                    SkillConfig current = equipped[i].Config;
                    if (!skillSystem.CanUpgradeSkill(current.SkillId)) continue;
                    SkillConfig next = SkillDatabase.GetNextLevel(current.SkillId, current.Level);
                    if (next != null)
                        pool.Add(new UpgradeOption { Type = UpgradeOption.OptionType.LevelUpgrade, Config = next });
                }
            }

            return pool;
        }

        private bool IsEligibleNewSkill(SkillConfig config)
        {
            if (config == null || config.Weight <= 0f) return false;
            if (skillSystem != null && skillSystem.HasSkill(config.SkillId)) return false;

            if (!string.IsNullOrEmpty(config.UpgradesFrom) &&
                (skillSystem == null || !skillSystem.HasSkill(config.UpgradesFrom)))
                return false;

            return true;
        }

        private static int RollWeightedIndex(IReadOnlyList<UpgradeOption> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                float weight = pool[i].Type == UpgradeOption.OptionType.LevelUpgrade
                    ? Mathf.Max(1f, pool[i].Config.Weight)
                    : Mathf.Max(0f, pool[i].Config.Weight);
                totalWeight += weight;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                float weight = pool[i].Type == UpgradeOption.OptionType.LevelUpgrade
                    ? Mathf.Max(1f, pool[i].Config.Weight)
                    : Mathf.Max(0f, pool[i].Config.Weight);
                cumulative += weight;
                if (roll <= cumulative) return i;
            }

            return pool.Count - 1;
        }
    }
}
