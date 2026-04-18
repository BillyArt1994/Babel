using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 技能数据库——游戏启动时由 SkillParser 填充，全生命周期只读。
    /// 提供按 ID 查询、起始技能筛选、进化链查询等接口。
    /// </summary>
    public static class SkillDatabase
    {
        private const string LOG_PREFIX = "[BABEL][SkillDB]";
        private static readonly Dictionary<string, SkillConfig> _byId = new();
        private static readonly List<SkillConfig> _allSkills = new();
        private static bool _initialized;

        /// <summary>
        /// 使用 CSV 文本重新初始化技能数据库。
        /// </summary>
        /// <param name="csvText">技能 CSV 原始文本。</param>
        public static void Init(string csvText)
        {
            _initialized = false;
            _byId.Clear();
            _allSkills.Clear();

            var parsedSkills = SkillParser.Parse(csvText);
            for (var i = 0; i < parsedSkills.Count; i++)
            {
                AddOrReplace(parsedSkills[i]);
            }

            ValidateUpgradeReferences();
            DetectUpgradeCycles();
            _initialized = true;
        }

        /// <summary>
        /// 按技能 ID 获取配置。
        /// </summary>
        /// <param name="skillId">技能唯一标识。</param>
        /// <returns>匹配的技能配置；未找到时返回 null。</returns>
        public static SkillConfig GetById(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            _byId.TryGetValue(skillId, out var config);
            return config;
        }

        /// <summary>
        /// 获取所有起始可选技能。
        /// </summary>
        /// <returns>起始技能列表。</returns>
        public static List<SkillConfig> GetStarterSkills()
        {
            var starterSkills = new List<SkillConfig>();
            if (!_initialized)
            {
                return starterSkills;
            }

            for (var i = 0; i < _allSkills.Count; i++)
            {
                if (_allSkills[i].IsStarterSkill)
                {
                    starterSkills.Add(_allSkills[i]);
                }
            }

            return starterSkills;
        }

        /// <summary>
        /// 获取指定技能的直接升级项。
        /// </summary>
        /// <param name="skillId">父技能 ID。</param>
        /// <returns>直接子升级技能列表。</returns>
        public static List<SkillConfig> GetUpgradesOf(string skillId)
        {
            var upgrades = new List<SkillConfig>();
            if (!_initialized)
            {
                return upgrades;
            }

            for (var i = 0; i < _allSkills.Count; i++)
            {
                if (_allSkills[i].UpgradesFrom == skillId)
                {
                    upgrades.Add(_allSkills[i]);
                }
            }

            return upgrades;
        }

        /// <summary>
        /// 获取所有已加载技能。
        /// </summary>
        /// <returns>只读技能列表。</returns>
        public static IReadOnlyList<SkillConfig> GetAll()
        {
            if (!_initialized)
            {
                return new List<SkillConfig>().AsReadOnly();
            }

            return _allSkills.AsReadOnly();
        }

        /// <summary>
        /// 获取当前已加载技能数量。
        /// </summary>
        public static int Count => _allSkills.Count;

        private static void AddOrReplace(SkillConfig config)
        {
            if (_byId.TryGetValue(config.SkillId, out var existing))
            {
                _allSkills.Remove(existing);
                Debug.LogWarning($"{LOG_PREFIX} Duplicate skillId '{config.SkillId}' found. Overwriting previous config.");
            }

            _byId[config.SkillId] = config;
            _allSkills.Add(config);
        }

        private static void ValidateUpgradeReferences()
        {
            for (var i = 0; i < _allSkills.Count; i++)
            {
                var config = _allSkills[i];
                if (string.IsNullOrEmpty(config.UpgradesFrom) || _byId.ContainsKey(config.UpgradesFrom))
                {
                    continue;
                }

                Debug.LogWarning($"{LOG_PREFIX} Skill '{config.SkillId}' references missing parent '{config.UpgradesFrom}'. Reference cleared.");
                config.UpgradesFrom = string.Empty;
            }
        }

        private static void DetectUpgradeCycles()
        {
            for (var i = 0; i < _allSkills.Count; i++)
            {
                var visited = new HashSet<string>();
                var current = _allSkills[i];
                while (current != null && !string.IsNullOrEmpty(current.UpgradesFrom))
                {
                    visited.Add(current.SkillId);
                    if (visited.Contains(current.UpgradesFrom))
                    {
                        Debug.LogWarning($"{LOG_PREFIX} Upgrade cycle detected at skill '{current.SkillId}'. Reference to '{current.UpgradesFrom}' cleared.");
                        current.UpgradesFrom = string.Empty;
                        break;
                    }

                    current = GetById(current.UpgradesFrom);
                }
            }
        }
    }
}
