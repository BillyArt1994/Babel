using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 负责将技能 CSV 文本解析为 <see cref="SkillConfig"/> 列表。
    /// </summary>
    public static class SkillParser
    {
        private const string LOG_PREFIX = "[BABEL][SkillParser]";
        private static readonly string[] REQUIRED_COLUMNS = { "skillId", "skillName", "triggerType", "effectType" };
        private static readonly HashSet<string> KNOWN_TRIGGER_TYPES = new HashSet<string> { "OnClick", "OnHit", "OnTimer", "OnKill" };
        private static readonly HashSet<string> KNOWN_EFFECT_TYPES = new HashSet<string> { "hit_single", "hit_aoe", "hit_chain", "dot_aoe", "stat_buff", "spawn_projectile", "apply_status", "execute" };

        /// <summary>
        /// 将 CSV 文本解析为技能配置列表。
        /// </summary>
        /// <param name="csvText">技能 CSV 原始文本。</param>
        /// <returns>解析后的技能配置列表。</returns>
        public static List<SkillConfig> Parse(string csvText)
        {
            var results = new List<SkillConfig>();
            var lines = GetNonEmptyLines(csvText);
            if (lines.Count == 0)
            {
                return results;
            }

            var headerMap = BuildHeaderMap(ParseCsvLine(lines[0]));
            if (!HasRequiredColumns(headerMap))
            {
                return results;
            }

            for (var i = 1; i < lines.Count; i++)
            {
                var fields = ParseCsvLine(lines[i]);
                if (fields.Length < headerMap.Count)
                {
                    Debug.LogWarning($"{LOG_PREFIX} Line {i + 1}: field count {fields.Length} is smaller than header count {headerMap.Count}, row skipped.");
                    continue;
                }

                var config = ParseSkillConfig(fields, headerMap, i + 1);
                if (config != null)
                {
                    results.Add(config);
                }
            }

            return results;
        }

        private static List<string> GetNonEmptyLines(string csvText)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(csvText))
            {
                return lines;
            }

            if (csvText[0] == '\uFEFF')
            {
                csvText = csvText.Substring(1);
            }

            var rawLines = csvText.Split('\n');
            for (var i = 0; i < rawLines.Length; i++)
            {
                var line = rawLines[i].TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] headers)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Length; i++)
            {
                map[headers[i].Trim()] = i;
            }

            return map;
        }

        private static bool HasRequiredColumns(Dictionary<string, int> headerMap)
        {
            var missing = new List<string>();
            for (var i = 0; i < REQUIRED_COLUMNS.Length; i++)
            {
                if (!headerMap.ContainsKey(REQUIRED_COLUMNS[i]))
                {
                    missing.Add(REQUIRED_COLUMNS[i]);
                }
            }

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError($"{LOG_PREFIX} Missing required columns: {string.Join(", ", missing)}.");
            return false;
        }

        private static SkillConfig ParseSkillConfig(string[] fields, Dictionary<string, int> map, int lineNumber)
        {
            var skillId = GetString(fields, map, "skillId");
            var triggerType = GetString(fields, map, "triggerType");
            if (!KNOWN_TRIGGER_TYPES.Contains(triggerType))
            {
                Debug.LogWarning($"{LOG_PREFIX} Line {lineNumber}: unknown triggerType '{triggerType}' for skill '{skillId}', row skipped.");
                return null;
            }

            var primaryEffect = ParseEffectConfig(fields, map, "");
            var effect2 = ParseEffectConfig(fields, map, "e2");
            var effect3 = ParseEffectConfig(fields, map, "e3");
            if (!ValidateEffectConfig(primaryEffect, true, lineNumber, skillId, "effectType") ||
                !ValidateEffectConfig(effect2, false, lineNumber, skillId, "effect2Type") ||
                !ValidateEffectConfig(effect3, false, lineNumber, skillId, "effect3Type"))
            {
                return null;
            }

            WarnIfOrphanedEffectParams(fields, map, "e2", lineNumber, skillId);
            if (effect2 == null && effect3 != null)
            {
                Debug.LogWarning($"{LOG_PREFIX} Line {lineNumber}: effect3 exists without effect2 for skill '{skillId}', effect3 discarded.");
                effect3 = null;
            }

            var config = CreateSkillConfig(fields, map);
            AddEffect(config, primaryEffect);
            AddEffect(config, effect2);
            AddEffect(config, effect3);
            return config;
        }

        private static SkillConfig CreateSkillConfig(string[] fields, Dictionary<string, int> map)
        {
            var config = new SkillConfig
            {
                SkillId = GetString(fields, map, "skillId"),
                SkillName = GetString(fields, map, "skillName"),
                Description = GetString(fields, map, "description"),
                IconPath = GetString(fields, map, "iconPath"),
                TriggerType = GetString(fields, map, "triggerType"),
                Cooldown = GetFloat(fields, map, "cooldown"),
                ChargeTime = GetFloat(fields, map, "chargeTime"),
                Interval = GetFloat(fields, map, "interval"),
                Chance = GetFloat(fields, map, "chance"),
                Level = GetInt(fields, map, "level"),
                IsStarterSkill = GetBool(fields, map, "isStarterSkill"),
                UpgradesFrom = GetString(fields, map, "upgradesFrom")
            };

            config.Weight = ResolveWeight(fields, map, config.SkillId);
            return config;
        }

        private static float ResolveWeight(string[] fields, Dictionary<string, int> map, string skillId)
        {
            if (!map.ContainsKey("weight"))
            {
                return 1.0f;
            }

            var weightRaw = GetString(fields, map, "weight");
            if (!string.IsNullOrEmpty(weightRaw))
            {
                return GetFloat(fields, map, "weight");
            }

            Debug.LogWarning($"{LOG_PREFIX} Skill '{skillId}' has empty weight cell. Defaulting to 1.0.");
            return 1.0f;
        }

        private static bool ValidateEffectConfig(EffectConfig effect, bool required, int lineNumber, string skillId, string columnName)
        {
            if (effect == null)
            {
                if (!required)
                {
                    return true;
                }

                Debug.LogWarning($"{LOG_PREFIX} Line {lineNumber}: missing {columnName} for skill '{skillId}', row skipped.");
                return false;
            }

            if (KNOWN_EFFECT_TYPES.Contains(effect.EffectType))
            {
                return true;
            }

            Debug.LogWarning($"{LOG_PREFIX} Line {lineNumber}: unknown {columnName} '{effect.EffectType}' for skill '{skillId}', row skipped.");
            return false;
        }

        private static void AddEffect(SkillConfig config, EffectConfig effect)
        {
            if (effect != null)
            {
                config.Effects.Add(effect);
            }
        }

        private static void WarnIfOrphanedEffectParams(string[] fields, Dictionary<string, int> map, string prefix, int lineNumber, string skillId)
        {
            if (!string.IsNullOrEmpty(GetString(fields, map, GetEffectTypeColumn(prefix))) || !HasOrphanedEffectData(fields, map, prefix))
            {
                return;
            }

            Debug.LogWarning($"{LOG_PREFIX} Line {lineNumber}: orphan {prefix} parameters found without {GetEffectTypeColumn(prefix)} for skill '{skillId}'.");
        }

        private static bool HasOrphanedEffectData(string[] fields, Dictionary<string, int> map, string prefix)
        {
            if (!string.IsNullOrEmpty(GetString(fields, map, GetEffectParamColumn(prefix, "statName"))))
            {
                return true;
            }

            return HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "damage")) ||
                   HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "damageRatio")) ||
                   HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "radius")) ||
                   HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "dps")) ||
                   HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "duration")) ||
                   HasMeaningfulNumericValue(fields, map, GetEffectParamColumn(prefix, "statValue"));
        }

        private static bool HasMeaningfulNumericValue(string[] fields, Dictionary<string, int> map, string columnName)
        {
            var raw = GetString(fields, map, columnName);
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            return !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || Math.Abs(value) > 0f;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var current = line[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    fields.Add(builder.ToString());
                    builder.Length = 0;
                    continue;
                }

                builder.Append(current);
            }

            fields.Add(builder.ToString());
            return fields.ToArray();
        }

        private static string GetString(string[] fields, Dictionary<string, int> map, string col)
        {
            if (!map.TryGetValue(col, out var index) || index < 0 || index >= fields.Length)
            {
                return string.Empty;
            }

            return fields[index].Trim();
        }

        private static float GetFloat(string[] fields, Dictionary<string, int> map, string col)
        {
            var raw = GetString(fields, map, col);
            if (string.IsNullOrEmpty(raw))
            {
                return 0f;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return 0f;
        }

        private static int GetInt(string[] fields, Dictionary<string, int> map, string col)
        {
            var raw = GetString(fields, map, col);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static bool GetBool(string[] fields, Dictionary<string, int> map, string col)
        {
            var raw = GetString(fields, map, col);
            return raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static EffectConfig ParseEffectConfig(string[] fields, Dictionary<string, int> map, string prefix)
        {
            var typeColumn = GetEffectTypeColumn(prefix);
            var effectType = GetString(fields, map, typeColumn);
            if (string.IsNullOrEmpty(effectType))
            {
                return null;
            }

            return new EffectConfig
            {
                EffectType = effectType,
                Damage = GetFloat(fields, map, GetEffectParamColumn(prefix, "damage")),
                DamageRatio = GetFloat(fields, map, GetEffectParamColumn(prefix, "damageRatio")),
                Radius = GetFloat(fields, map, GetEffectParamColumn(prefix, "radius")),
                Dps = GetFloat(fields, map, GetEffectParamColumn(prefix, "dps")),
                Duration = GetFloat(fields, map, GetEffectParamColumn(prefix, "duration")),
                StatName = GetString(fields, map, GetEffectParamColumn(prefix, "statName")),
                StatValue = GetFloat(fields, map, GetEffectParamColumn(prefix, "statValue"))
            };
        }

        private static string GetEffectTypeColumn(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return "effectType";
            }

            return prefix == "e2" ? "effect2Type" : "effect3Type";
        }

        private static string GetEffectParamColumn(string prefix, string baseName)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return baseName;
            }

            return prefix + char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
        }
    }
}
