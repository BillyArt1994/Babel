using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 管理升级三选一的选项生成、选择应用与暂停恢复流程。
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeSystem : ViewController
    {
        private const int OPTIONS_COUNT = 3;
        private const int EXP_THRESHOLD = 5;

        [Header("升级配置")]
        [SerializeField]
        [Tooltip("负责实际装备技能的技能系统。")]
        private SkillSystem skillSystem;

        private readonly List<SkillConfig> _pendingOptions = new();
        private bool _isHandlingExpChange;

        /// <summary>
        /// 当前待选择选项数量，仅供测试验证。
        /// </summary>
        public int PendingOptionCountForTests => _pendingOptions.Count;

        private void OnEnable()
        {
            UpgradeEvents.OnOptionSelected += SelectOption;
        }

        private void OnDisable()
        {
            UpgradeEvents.OnOptionSelected -= SelectOption;
        }

        private void Start()
        {
            Global.Exp.RegisterWithInitValue(OnExpChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        /// <summary>
        /// 选择并应用指定索引的升级选项。
        /// </summary>
        /// <param name="index">选项索引。</param>
        public void SelectOption(int index)
        {
            if (index < 0 || index >= _pendingOptions.Count)
            {
                Debug.LogWarning($"[BABEL][UpgradeSystem] Invalid upgrade option index {index}");
                return;
            }

            if (skillSystem == null)
            {
                Debug.LogWarning("[BABEL][UpgradeSystem] No SkillSystem assigned");
                return;
            }

            SkillConfig selected = _pendingOptions[index];
            skillSystem.AddOrReplaceSkill(selected);
            skillSystem.ResetClickCooldowns();
            _pendingOptions.Clear();
            Time.timeScale = 1f;
            UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
        }

        /// <summary>
        /// 为测试注入技能系统依赖。
        /// </summary>
        /// <param name="system">技能系统实例。</param>
        public void SetSkillSystemForTests(SkillSystem system)
        {
            skillSystem = system;
        }

        /// <summary>
        /// 为测试生成指定数量的升级选项。
        /// </summary>
        /// <param name="count">目标选项数量。</param>
        /// <returns>生成的技能选项。</returns>
        public IReadOnlyList<SkillConfig> GenerateOptionsForTests(int count)
        {
            return GenerateOptions(count);
        }

        /// <summary>
        /// 为测试直接设置待选项。
        /// </summary>
        /// <param name="options">待选择技能。</param>
        public void SetPendingOptionsForTests(IReadOnlyList<SkillConfig> options)
        {
            _pendingOptions.Clear();
            if (options == null)
            {
                return;
            }

            for (int i = 0; i < options.Count; i++)
            {
                _pendingOptions.Add(options[i]);
            }
        }

        private IReadOnlyList<SkillConfig> GenerateOptions(int count)
        {
            var pool = BuildEligiblePool();
            var selected = new List<SkillConfig>(Mathf.Min(count, pool.Count));
            while (selected.Count < count && pool.Count > 0)
            {
                int index = RollWeightedIndex(pool);
                selected.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return selected;
        }

        private void OnExpChanged(int exp)
        {
            if (_isHandlingExpChange || exp < EXP_THRESHOLD || _pendingOptions.Count > 0)
            {
                return;
            }

            _isHandlingExpChange = true;
            Global.Exp.Value -= EXP_THRESHOLD;
            Global.Level.Value++;
            _pendingOptions.Clear();
            IReadOnlyList<SkillConfig> options = GenerateOptions(OPTIONS_COUNT);
            for (int i = 0; i < options.Count; i++)
            {
                _pendingOptions.Add(options[i]);
            }

            if (_pendingOptions.Count == 0)
            {
                Time.timeScale = 1f;
                UpgradeEvents.RaiseOptionsGenerated(Array.Empty<SkillConfig>());
                _isHandlingExpChange = false;
                return;
            }

            Time.timeScale = 0f;
            UpgradeEvents.RaiseOptionsGenerated(_pendingOptions);
            _isHandlingExpChange = false;
        }

        private List<SkillConfig> BuildEligiblePool()
        {
            var pool = new List<SkillConfig>();
            IReadOnlyList<SkillConfig> allSkills = SkillDatabase.GetAll();
            for (int i = 0; i < allSkills.Count; i++)
            {
                SkillConfig config = allSkills[i];
                if (IsEligible(config))
                {
                    pool.Add(config);
                }
            }

            return pool;
        }

        private bool IsEligible(SkillConfig config)
        {
            if (config == null || config.Weight <= 0f)
            {
                return false;
            }

            if (skillSystem != null && skillSystem.HasSkill(config.SkillId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(config.UpgradesFrom) &&
                (skillSystem == null || !skillSystem.HasSkill(config.UpgradesFrom)))
            {
                return false;
            }

            return true;
        }

        private static int RollWeightedIndex(IReadOnlyList<SkillConfig> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Mathf.Max(0f, pool[i].Weight);
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += Mathf.Max(0f, pool[i].Weight);
                if (roll <= cumulative)
                {
                    return i;
                }
            }

            return pool.Count - 1;
        }
    }
}
