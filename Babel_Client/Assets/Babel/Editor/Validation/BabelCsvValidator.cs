using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Babel.EditorTools.Validation
{
    internal sealed class BabelContentIds
    {
        internal readonly HashSet<string> EnemyIds = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> SkillIds = new HashSet<string>(StringComparer.Ordinal);
    }

    internal static class BabelCsvValidator
    {
        internal static BabelContentIds Validate(
            TextAsset experience,
            TextAsset enemies,
            TextAsset waves,
            TextAsset skills,
            BabelValidationReport report)
        {
            var ids = new BabelContentIds();
            ValidateExperience(experience, report);
            ValidateEnemies(enemies, ids, report);
            ValidateSkills(skills, ids, report);
            ValidateWaves(waves, ids, report);
            return ids;
        }

        private static void ValidateExperience(TextAsset asset, BabelValidationReport report)
        {
            CsvTable table = CsvTable.Create(asset, new[] { "level", "requiredXp" }, "XP000", report);
            if (table == null) return;

            var seen = new HashSet<int>();
            int expectedLevel = 2;
            float previousXp = -1f;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                int level;
                float xp;
                if (!TryInt(row.Get("level"), out level))
                {
                    AddRowError(report, "XP001", table, row, "level must be an integer.");
                    continue;
                }
                if (!TryFiniteFloat(row.Get("requiredXp"), out xp) || xp <= 0f)
                {
                    AddRowError(report, "XP001", table, row, "requiredXp must be finite and greater than zero.");
                    continue;
                }
                if (!seen.Add(level))
                    AddRowError(report, "XP001", table, row, "duplicate level " + level + ".");
                if (level != expectedLevel)
                    AddRowError(report, "XP001", table, row, "levels must be contiguous from 2; expected " + expectedLevel + " but found " + level + ".");
                expectedLevel = level + 1;
                if (previousXp >= 0f && xp < previousXp)
                    AddRowWarning(report, "XP002", table, row, "requiredXp decreases from the previous level.");
                previousXp = xp;
            }
        }

        private static void ValidateEnemies(TextAsset asset, BabelContentIds ids, BabelValidationReport report)
        {
            string[] required =
            {
                "enemyId", "prefab", "hp", "moveSpeed", "buildContribution",
                "buildCharges", "expReward", "buildTime", "moveMode"
            };
            CsvTable table = CsvTable.Create(asset, required, "ENEMY000", report);
            if (table == null) return;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                string id = row.Get("enemyId").Trim();
                if (id.Length == 0)
                {
                    AddRowError(report, "ENEMY001", table, row, "enemyId is required.");
                    continue;
                }
                if (!ids.EnemyIds.Add(id))
                    AddRowError(report, "ENEMY001", table, row, "duplicate enemyId '" + id + "'.");
                if (string.IsNullOrWhiteSpace(row.Get("prefab")))
                    AddRowError(report, "ENEMY002", table, row, "prefab logical key is required for '" + id + "'.");
                if (string.IsNullOrWhiteSpace(row.Get("moveMode")))
                    AddRowError(report, "ENEMY002", table, row, "moveMode is required for '" + id + "'.");

                ValidateFloatRange(table, row, "hp", 0f, false, "ENEMY002", report);
                ValidateFloatRange(table, row, "moveSpeed", 0f, true, "ENEMY002", report);
                ValidateFloatRange(table, row, "buildContribution", 0f, true, "ENEMY002", report);
                ValidateFloatRange(table, row, "buildCharges", 0f, true, "ENEMY002", report);
                ValidateFloatRange(table, row, "expReward", 0f, true, "ENEMY002", report);
                ValidateFloatRange(table, row, "buildTime", 0f, true, "ENEMY002", report);
            }
        }

        private static void ValidateSkills(TextAsset asset, BabelContentIds ids, BabelValidationReport report)
        {
            string[] required =
            {
                "skillId", "level", "maxLevel", "iconPath", "triggerType",
                "effectType", "isStarterSkill", "upgradesFrom"
            };
            CsvTable table = CsvTable.Create(asset, required, "SKILL000", report);
            if (table == null) return;

            var compoundKeys = new HashSet<string>(StringComparer.Ordinal);
            var upgradeEdges = new List<UpgradeEdge>();
            bool hasStarter = false;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                string id = row.Get("skillId").Trim();
                int level;
                int maxLevel;
                if (id.Length == 0)
                {
                    AddRowError(report, "SKILL001", table, row, "skillId is required.");
                    continue;
                }

                ids.SkillIds.Add(id);
                bool levelValid = TryInt(row.Get("level"), out level) && level >= 1;
                bool maxValid = TryInt(row.Get("maxLevel"), out maxLevel) && maxLevel >= 1;
                if (!levelValid || !maxValid)
                {
                    AddRowError(report, "SKILL001", table, row, "level and maxLevel must be positive integers for '" + id + "'.");
                }
                else
                {
                    string key = id + "#" + level.ToString(CultureInfo.InvariantCulture);
                    if (!compoundKeys.Add(key))
                        AddRowError(report, "SKILL001", table, row, "duplicate (skillId, level) key '" + key + "'.");
                    if (level > maxLevel)
                        AddRowWarning(report, "SKILL005", table, row, "level exceeds maxLevel for '" + id + "'; retained for legacy evolution compatibility.");
                }

                bool isStarter;
                if (!TryBool(row.Get("isStarterSkill"), out isStarter))
                    AddRowError(report, "SKILL002", table, row, "isStarterSkill must be TRUE or FALSE for '" + id + "'.");
                else if (isStarter)
                {
                    hasStarter = true;
                    if (levelValid && level != 1)
                        AddRowError(report, "SKILL002", table, row, "starter skill '" + id + "' must start at level 1.");
                }

                if (string.IsNullOrWhiteSpace(row.Get("iconPath")))
                    AddRowWarning(report, "SKILL004", table, row, "iconPath is empty for '" + id + "'; Manifest fallback will be used.");

                string parent = row.Get("upgradesFrom").Trim();
                if (parent.Length > 0)
                    upgradeEdges.Add(new UpgradeEdge(id, parent, row.Line));
                ValidateOptionalNonNegative(table, row, "cooldown", "SKILL006", report);
                ValidateOptionalNonNegative(table, row, "chargeTime", "SKILL006", report);
                ValidateOptionalNonNegative(table, row, "interval", "SKILL006", report);
                ValidateOptionalUnitInterval(table, row, "chance", "SKILL006", report);
                ValidateOptionalNonNegative(table, row, "weight", "SKILL006", report);
            }

            if (!hasStarter)
                report.AddError("SKILL002", "skills.csv must contain at least one starter skill.", table.AssetPath, table.Asset);

            for (int i = 0; i < upgradeEdges.Count; i++)
            {
                UpgradeEdge edge = upgradeEdges[i];
                if (string.Equals(edge.SkillId, edge.ParentId, StringComparison.Ordinal))
                    report.AddError("SKILL003", "Line " + edge.Line + ": skill '" + edge.SkillId + "' cannot upgrade from itself.", table.AssetPath, table.Asset);
                else if (!ids.SkillIds.Contains(edge.ParentId))
                    report.AddError("SKILL003", "Line " + edge.Line + ": upgradesFrom references unknown skillId '" + edge.ParentId + "'.", table.AssetPath, table.Asset);
            }
        }

        private static void ValidateWaves(TextAsset asset, BabelContentIds ids, BabelValidationReport report)
        {
            string[] required =
            {
                "startTime", "endTime", "mode", "enemyPool",
                "countMin", "countMax", "interval", "spawnPointId"
            };
            CsvTable table = CsvTable.Create(asset, required, "WAVE000", report);
            if (table == null) return;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                float start = 0f;
                float end = 0f;
                float interval = 0f;
                int countMin = 0;
                int countMax = 0;
                string mode = row.Get("mode").Trim();

                bool numeric = TryFiniteFloat(row.Get("startTime"), out start) &&
                               TryFiniteFloat(row.Get("endTime"), out end) &&
                               TryFiniteFloat(row.Get("interval"), out interval) &&
                               TryInt(row.Get("countMin"), out countMin) &&
                               TryInt(row.Get("countMax"), out countMax);
                if (!numeric)
                {
                    AddRowError(report, "WAVE001", table, row, "wave timing and count fields must be finite numeric values.");
                    continue;
                }

                bool knownMode = mode == "Timed" || mode == "Burst" || mode == "Maintain";
                if (!knownMode)
                    AddRowError(report, "WAVE001", table, row, "unknown mode '" + mode + "'.");
                if (start < 0f)
                    AddRowError(report, "WAVE001", table, row, "startTime cannot be negative.");
                if (mode != "Maintain" && end <= start)
                    AddRowError(report, "WAVE001", table, row, "endTime must be greater than startTime for " + mode + ".");
                if (countMin < 0 || countMax < countMin)
                    AddRowError(report, "WAVE001", table, row, "count range is invalid.");
                if ((mode == "Timed" || mode == "Maintain") && interval <= 0f)
                    AddRowError(report, "WAVE001", table, row, "interval must be greater than zero for " + mode + ".");
                if (string.IsNullOrWhiteSpace(row.Get("spawnPointId")))
                    AddRowError(report, "WAVE001", table, row, "spawnPointId is required.");

                string[] entries = row.Get("enemyPool").Split('|');
                if (entries.Length == 0 || string.IsNullOrWhiteSpace(row.Get("enemyPool")))
                {
                    AddRowError(report, "WAVE002", table, row, "enemyPool is required.");
                    continue;
                }

                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    string[] pair = entries[entryIndex].Split(':');
                    float weight;
                    if (pair.Length != 2 || !TryFiniteFloat(pair[1], out weight) || weight <= 0f)
                    {
                        AddRowError(report, "WAVE002", table, row, "invalid enemyPool entry '" + entries[entryIndex] + "'.");
                        continue;
                    }
                    string enemyId = pair[0].Trim();
                    if (!ids.EnemyIds.Contains(enemyId))
                        AddRowError(report, "WAVE002", table, row, "enemyPool references unknown enemyId '" + enemyId + "'.");
                }
            }
        }

        private static void ValidateFloatRange(
            CsvTable table,
            CsvRow row,
            string column,
            float minimum,
            bool inclusive,
            string code,
            BabelValidationReport report)
        {
            float value;
            bool valid = TryFiniteFloat(row.Get(column), out value) &&
                         (inclusive ? value >= minimum : value > minimum);
            if (!valid)
                AddRowError(report, code, table, row, column + " is outside its allowed range.");
        }

        private static void ValidateOptionalNonNegative(
            CsvTable table,
            CsvRow row,
            string column,
            string code,
            BabelValidationReport report)
        {
            if (!table.HasColumn(column)) return;
            string raw = row.Get(column).Trim();
            if (raw.Length == 0) return;
            float value;
            if (!TryFiniteFloat(raw, out value) || value < 0f)
                AddRowError(report, code, table, row, column + " must be finite and non-negative.");
        }

        private static void ValidateOptionalUnitInterval(
            CsvTable table,
            CsvRow row,
            string column,
            string code,
            BabelValidationReport report)
        {
            if (!table.HasColumn(column)) return;
            string raw = row.Get(column).Trim();
            if (raw.Length == 0) return;
            float value;
            if (!TryFiniteFloat(raw, out value) || value < 0f || value > 1f)
                AddRowError(report, code, table, row, column + " must be within [0, 1].");
        }

        private static bool TryInt(string raw, out int value)
        {
            return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryFiniteFloat(string raw, out float value)
        {
            if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryBool(string raw, out bool value)
        {
            string normalized = raw.Trim();
            if (string.Equals(normalized, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }
            if (string.Equals(normalized, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }
            value = false;
            return false;
        }

        private static void AddRowError(BabelValidationReport report, string code, CsvTable table, CsvRow row, string message)
        {
            report.AddError(code, "Line " + row.Line + ": " + message, table.AssetPath, table.Asset);
        }

        private static void AddRowWarning(BabelValidationReport report, string code, CsvTable table, CsvRow row, string message)
        {
            report.AddWarning(code, "Line " + row.Line + ": " + message, table.AssetPath, table.Asset);
        }

        private sealed class UpgradeEdge
        {
            internal UpgradeEdge(string skillId, string parentId, int line)
            {
                SkillId = skillId;
                ParentId = parentId;
                Line = line;
            }
            internal string SkillId;
            internal string ParentId;
            internal int Line;
        }

        private sealed class CsvTable
        {
            private readonly Dictionary<string, int> _columns;
            internal readonly List<CsvRow> Rows;
            internal readonly TextAsset Asset;
            internal readonly string AssetPath;

            private CsvTable(TextAsset asset, string path, Dictionary<string, int> columns, List<CsvRow> rows)
            {
                Asset = asset;
                AssetPath = path;
                _columns = columns;
                Rows = rows;
                for (int i = 0; i < rows.Count; i++) rows[i].Columns = columns;
            }

            internal bool HasColumn(string name) { return _columns.ContainsKey(name); }

            internal static CsvTable Create(
                TextAsset asset,
                string[] requiredColumns,
                string code,
                BabelValidationReport report)
            {
                if (asset == null) return null;
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrWhiteSpace(asset.text))
                {
                    report.AddError(code, "CSV is empty.", path, asset);
                    return null;
                }

                string[] lines = asset.text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    report.AddError(code, "CSV has no header.", path, asset);
                    return null;
                }

                List<string> header;
                string parseError;
                if (!TryParseLine(lines[0], out header, out parseError))
                {
                    report.AddError(code, "Header: " + parseError, path, asset);
                    return null;
                }

                var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Count; i++)
                {
                    string name = header[i].Trim();
                    if (name.Length == 0)
                    {
                        report.AddError(code, "Header contains an empty column at index " + i + ".", path, asset);
                        continue;
                    }
                    if (columns.ContainsKey(name))
                        report.AddError(code, "Header contains duplicate column '" + name + "'.", path, asset);
                    else
                        columns.Add(name, i);
                }

                for (int i = 0; i < requiredColumns.Length; i++)
                {
                    if (!columns.ContainsKey(requiredColumns[i]))
                        report.AddError(code, "Missing required column '" + requiredColumns[i] + "'.", path, asset);
                }
                if (report.HasErrors && columns.Count == 0) return null;

                var rows = new List<CsvRow>();
                for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
                {
                    if (string.IsNullOrWhiteSpace(lines[lineIndex])) continue;
                    List<string> fields;
                    if (!TryParseLine(lines[lineIndex], out fields, out parseError))
                    {
                        report.AddError(code, "Line " + (lineIndex + 1) + ": " + parseError, path, asset);
                        continue;
                    }
                    if (fields.Count != header.Count)
                    {
                        report.AddError(code, "Line " + (lineIndex + 1) + ": expected " + header.Count + " columns but found " + fields.Count + ".", path, asset);
                        continue;
                    }
                    rows.Add(new CsvRow(lineIndex + 1, fields.ToArray()));
                }

                return new CsvTable(asset, path, columns, rows);
            }

            private static bool TryParseLine(string line, out List<string> fields, out string error)
            {
                fields = new List<string>();
                var current = new System.Text.StringBuilder();
                bool inQuotes = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char ch = line[i];
                    if (ch == '"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (ch == ',' && !inQuotes)
                    {
                        fields.Add(current.ToString());
                        current.Length = 0;
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
                if (inQuotes)
                {
                    error = "unclosed quoted field.";
                    return false;
                }
                fields.Add(current.ToString());
                error = null;
                return true;
            }
        }

        private sealed class CsvRow
        {
            internal CsvRow(int line, string[] fields)
            {
                Line = line;
                Fields = fields;
            }

            internal readonly int Line;
            internal readonly string[] Fields;
            internal Dictionary<string, int> Columns;

            internal string Get(string column)
            {
                int index;
                if (!Columns.TryGetValue(column, out index) || index < 0 || index >= Fields.Length)
                    return string.Empty;
                return Fields[index] ?? string.Empty;
            }
        }
    }
}
