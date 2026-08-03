using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Gameplay.Content;
using Babel.Unity.Infrastructure.Content;
using Babel.Unity.Presentation.Babel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.EditorTools.Content
{
    /// <summary>Compiles canonical authoring CSVs into the player-safe immutable content asset.</summary>
    public static class GameContentCompiler
    {
        public const string ManifestAssetPath = "Assets/Babel/Content/Manifests/GameContentManifest.asset";
        public const string GeneratedAssetPath = "Assets/Babel/Content/Generated/CompiledGameContent.asset";
        public const string GameSceneAssetPath = "Assets/Babel/Scenes/Game/GameScene.unity";

        private static readonly HashSet<string> KnownTriggerTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "OnClick", "OnHit", "OnTimer", "OnKill"
        };

        private static readonly HashSet<string> KnownEffectTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "hit_single", "hit_aoe", "dot_aoe", "stat_buff"
        };

        [MenuItem("Babel/Content/Compile Game Content", priority = 100)]
        private static void CompileFromMenu()
        {
            CompiledGameContent compiled = Compile(true);
            Selection.activeObject = compiled;
        }

        /// <summary>
        /// Compiles all four CSV references from the canonical manifest, validates the resulting
        /// runtime catalogs, updates the generated asset in place, and binds it back to the manifest.
        /// Build preprocessing may call this method directly.
        /// </summary>
        public static CompiledGameContent Compile(bool log)
        {
            GameContentManifest manifest = AssetDatabase.LoadAssetAtPath<GameContentManifest>(ManifestAssetPath);
            if (manifest == null)
                throw new InvalidOperationException("Canonical GameContentManifest is missing at '" + ManifestAssetPath + "'.");

            RequireCsv(manifest.ExperienceCsv, "experience");
            RequireCsv(manifest.EnemiesCsv, "enemies");
            RequireCsv(manifest.WavesCsv, "waves");
            RequireCsv(manifest.SkillsCsv, "skills");

            BabelDefinition babel = CompileBabelDefinition();
            string sourceHash = ComputeCurrentSourceHash(manifest, babel);
            float[] experience = ParseExperience(manifest.ExperienceCsv);
            HumanContentRecord[] humans = ParseHumans(manifest.EnemiesCsv);
            WaveContentRecord[] waves = ParseWaves(manifest.WavesCsv);
            SkillContentRecord[] skills = ParseSkills(manifest.SkillsCsv);
            int[] buildPoints = Copy(babel.BuildPointCounts);
            int[] gateways = Copy(babel.GatewayCounts);

            ValidateBeforeWriting(sourceHash, humans, waves, skills, experience, buildPoints, gateways);
            CompiledGameContent compiled = LoadOrCreateGeneratedAsset();
            compiled.ReplaceForEditor(sourceHash, humans, waves, skills, experience, buildPoints, gateways);
            compiled.ValidateRuntimeCatalogs();
            EditorUtility.SetDirty(compiled);
            BindCompiledContent(manifest, compiled);
            AssetDatabase.SaveAssets();

            if (log)
            {
                Debug.Log(
                    "[Babel][ContentCompiler] Compiled " + compiled.HumanCount + " humans, " +
                    compiled.WaveCount + " waves, " + compiled.SkillCount + " skill levels and " +
                    compiled.ExperienceThresholdCount + " XP thresholds. Source hash: " + sourceHash,
                    compiled);
            }

            return compiled;
        }

        private static void RequireCsv(TextAsset asset, string role)
        {
            if (asset == null)
                throw new InvalidOperationException("Canonical manifest has no " + role + " CSV assigned.");
            if (string.IsNullOrWhiteSpace(asset.text))
                throw new FormatException("CSV '" + AssetDatabase.GetAssetPath(asset) + "' is empty.");
        }

        private static HumanContentRecord[] ParseHumans(TextAsset asset)
        {
            CsvTable table = CsvTable.Parse(asset);
            table.RequireColumns(
                "enemyId", "enemyName", "hp", "moveSpeed", "buildContribution", "buildCharges",
                "expReward", "prefab", "abilityType", "abilityRadius", "abilityValue",
                "abilityCooldown", "buildTime", "moveMode", "senseRadius");

            var records = new HumanContentRecord[table.Rows.Count];
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                var record = new HumanContentRecord
                {
                    id = row.Require("enemyId"),
                    displayName = row.Require("enemyName"),
                    maxHealth = row.RequiredFloat("hp"),
                    moveSpeed = row.RequiredFloat("moveSpeed"),
                    buildContribution = row.RequiredInt("buildContribution"),
                    buildCharges = row.RequiredInt("buildCharges"),
                    experienceReward = row.RequiredInt("expReward"),
                    abilityType = row.Optional("abilityType"),
                    abilityRadius = row.OptionalFloat("abilityRadius", 0f),
                    abilityValue = row.OptionalFloat("abilityValue", 0f),
                    abilityCooldownSeconds = row.OptionalFloat("abilityCooldown", 0f),
                    buildTimeSeconds = row.OptionalFloat("buildTime", 0f),
                    moveMode = row.Require("moveMode"),
                    senseRadius = row.OptionalFloat("senseRadius", 8f)
                };
                ValidateRow(row, record.ToDefinition);
                records[i] = record;
            }

            return records;
        }

        private static WaveContentRecord[] ParseWaves(TextAsset asset)
        {
            CsvTable table = CsvTable.Parse(asset);
            table.RequireColumns(
                "startTime", "endTime", "mode", "enemyPool", "countMin", "countMax",
                "interval", "spawnPointId");

            var records = new WaveContentRecord[table.Rows.Count];
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                var record = new WaveContentRecord
                {
                    id = "wave_" + (i + 1).ToString("D3", CultureInfo.InvariantCulture),
                    startSeconds = row.RequiredFloat("startTime"),
                    endSeconds = row.RequiredFloat("endTime"),
                    mode = ParseWaveMode(row),
                    pool = ParsePool(row),
                    countMin = row.RequiredInt("countMin"),
                    countMax = row.RequiredInt("countMax"),
                    intervalSeconds = row.RequiredFloat("interval"),
                    spawnPointId = row.Require("spawnPointId")
                };
                ValidateRow(row, record.ToDefinition);
                records[i] = record;
            }

            return records;
        }

        private static WaveSpawnMode ParseWaveMode(CsvRow row)
        {
            string value = row.Require("mode");
            if (value.Equals("Burst", StringComparison.OrdinalIgnoreCase)) return WaveSpawnMode.Burst;
            if (value.Equals("Maintain", StringComparison.OrdinalIgnoreCase)) return WaveSpawnMode.Maintain;
            if (value.Equals("Timed", StringComparison.OrdinalIgnoreCase)) return WaveSpawnMode.Timed;
            throw row.Error("mode", "unknown wave mode '" + value + "'.");
        }

        private static WeightedHumanContentRecord[] ParsePool(CsvRow row)
        {
            string raw = row.Require("enemyPool");
            string[] parts = raw.Split('|');
            var records = new WeightedHumanContentRecord[parts.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parts.Length; i++)
            {
                string entry = parts[i].Trim();
                int separator = entry.IndexOf(':');
                if (separator <= 0 || separator != entry.LastIndexOf(':') || separator == entry.Length - 1)
                    throw row.Error("enemyPool", "invalid pool entry '" + entry + "'; expected humanId:weight.");

                string humanId = entry.Substring(0, separator).Trim();
                string weightText = entry.Substring(separator + 1).Trim();
                if (!seen.Add(humanId))
                    throw row.Error("enemyPool", "duplicate human ID '" + humanId + "' in one wave pool.");
                float weight = ParseFiniteFloat(weightText, row, "enemyPool");
                records[i] = new WeightedHumanContentRecord { humanId = humanId, weight = weight };
                ValidateRow(row, records[i].ToDefinition);
            }

            return records;
        }

        private static SkillContentRecord[] ParseSkills(TextAsset asset)
        {
            CsvTable table = CsvTable.Parse(asset);
            table.RequireColumns(
                "skillId", "skillName", "description", "iconPath", "triggerType", "cooldown",
                "chargeTime", "interval", "chance", "effectType", "damage", "damageRatio",
                "radius", "dps", "duration", "statName", "statValue", "effect2Type", "e2Damage",
                "e2DamageRatio", "e2Radius", "e2Dps", "e2Duration", "e2StatName", "e2StatValue",
                "effect3Type", "e3Damage", "e3DamageRatio", "e3Radius", "e3Dps", "e3Duration",
                "e3StatName", "e3StatValue", "level", "maxLevel", "weight", "isStarterSkill",
                "upgradesFrom");

            var records = new SkillContentRecord[table.Rows.Count];
            bool hasStarter = false;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                string id = row.Require("skillId");
                string trigger = row.Require("triggerType");
                if (!KnownTriggerTypes.Contains(trigger))
                    throw row.Error("triggerType", "unknown trigger type '" + trigger + "'.");

                var effects = new List<EffectContentRecord>(3);
                effects.Add(ParseEffect(
                    row, true, "effectType", "damage", "damageRatio", "radius", "dps",
                    "duration", "statName", "statValue"));
                AddOptionalEffect(effects, ParseEffect(
                    row, false, "effect2Type", "e2Damage", "e2DamageRatio", "e2Radius", "e2Dps",
                    "e2Duration", "e2StatName", "e2StatValue"));
                EffectContentRecord third = ParseEffect(
                    row, false, "effect3Type", "e3Damage", "e3DamageRatio", "e3Radius", "e3Dps",
                    "e3Duration", "e3StatName", "e3StatValue");
                if (third != null && effects.Count < 2)
                    throw row.Error("effect3Type", "effect3 cannot be populated when effect2 is empty.");
                AddOptionalEffect(effects, third);

                bool isStarter = row.RequiredBool("isStarterSkill");
                hasStarter |= isStarter;
                var record = new SkillContentRecord
                {
                    id = id,
                    displayName = row.Require("skillName"),
                    description = row.Optional("description"),
                    // Presentation assets are mapped by skill ID in GameContentManifest.
                    iconId = id,
                    triggerType = trigger,
                    cooldownSeconds = row.OptionalFloat("cooldown", 0f),
                    chargeTimeSeconds = row.OptionalFloat("chargeTime", 0f),
                    intervalSeconds = row.OptionalFloat("interval", 0f),
                    chance = row.OptionalFloat("chance", 0f),
                    effects = effects.ToArray(),
                    level = row.RequiredInt("level"),
                    maxLevel = row.RequiredInt("maxLevel"),
                    weight = row.OptionalFloat("weight", 1f),
                    isStarterSkill = isStarter,
                    upgradesFrom = row.Optional("upgradesFrom")
                };
                ValidateRow(row, record.ToDefinition);
                records[i] = record;
            }

            if (!hasStarter)
                throw new FormatException("CSV '" + table.AssetPath + "' must define at least one starter skill.");
            return records;
        }

        private static EffectContentRecord ParseEffect(
            CsvRow row,
            bool required,
            string typeColumn,
            string damageColumn,
            string ratioColumn,
            string radiusColumn,
            string dpsColumn,
            string durationColumn,
            string statNameColumn,
            string statValueColumn)
        {
            string effectType = row.Optional(typeColumn);
            string[] valueColumns =
            {
                damageColumn, ratioColumn, radiusColumn, dpsColumn,
                durationColumn, statNameColumn, statValueColumn
            };

            if (effectType.Length == 0)
            {
                if (required) throw row.Error(typeColumn, "a primary effect type is required.");
                for (int i = 0; i < valueColumns.Length; i++)
                {
                    if (row.Optional(valueColumns[i]).Length > 0)
                        throw row.Error(typeColumn, "effect parameters exist without an effect type.");
                }
                return null;
            }

            if (!KnownEffectTypes.Contains(effectType))
                throw row.Error(typeColumn, "unknown effect type '" + effectType + "'.");

            var record = new EffectContentRecord
            {
                effectType = effectType,
                damage = row.OptionalFloat(damageColumn, 0f),
                damageRatio = row.OptionalFloat(ratioColumn, 0f),
                radius = row.OptionalFloat(radiusColumn, 0f),
                damagePerSecond = row.OptionalFloat(dpsColumn, 0f),
                durationSeconds = row.OptionalFloat(durationColumn, 0f),
                statName = row.Optional(statNameColumn),
                statValue = row.OptionalFloat(statValueColumn, 0f)
            };
            ValidateRow(row, record.ToDefinition);
            return record;
        }

        private static void AddOptionalEffect(List<EffectContentRecord> effects, EffectContentRecord effect)
        {
            if (effect != null) effects.Add(effect);
        }

        private static float[] ParseExperience(TextAsset asset)
        {
            CsvTable table = CsvTable.Parse(asset);
            table.RequireColumns("level", "requiredXp");
            if (table.Rows.Count == 0)
                throw new FormatException("CSV '" + table.AssetPath + "' contains no experience rows.");

            var thresholds = new float[table.Rows.Count];
            int expectedLevel = 2;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvRow row = table.Rows[i];
                int level = row.RequiredInt("level");
                if (level != expectedLevel)
                    throw row.Error("level", "levels must be contiguous from 2; expected " + expectedLevel + ".");
                thresholds[i] = row.RequiredFloat("requiredXp");
                expectedLevel++;
            }

            try
            {
                new ExperienceTable((float[])thresholds.Clone());
            }
            catch (Exception exception)
            {
                throw new FormatException("CSV '" + table.AssetPath + "' contains an invalid experience curve.", exception);
            }
            return thresholds;
        }

        private static void ValidateBeforeWriting(
            string sourceHash,
            HumanContentRecord[] humans,
            WaveContentRecord[] waves,
            SkillContentRecord[] skills,
            float[] experience,
            int[] buildPoints,
            int[] gateways)
        {
            CompiledGameContent temporary = ScriptableObject.CreateInstance<CompiledGameContent>();
            try
            {
                temporary.ReplaceForEditor(sourceHash, humans, waves, skills, experience, buildPoints, gateways);
                temporary.ValidateRuntimeCatalogs();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static CompiledGameContent LoadOrCreateGeneratedAsset()
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(GeneratedAssetPath);
            if (existing != null && !(existing is CompiledGameContent))
                throw new InvalidOperationException("Generated content path is occupied by '" + existing.GetType().Name + "'.");

            var compiled = existing as CompiledGameContent;
            if (compiled != null) return compiled;
            if (!AssetDatabase.IsValidFolder("Assets/Babel/Content/Generated"))
                throw new InvalidOperationException("Generated content folder is missing.");

            compiled = ScriptableObject.CreateInstance<CompiledGameContent>();
            compiled.name = "CompiledGameContent";
            AssetDatabase.CreateAsset(compiled, GeneratedAssetPath);
            return compiled;
        }

        private static void BindCompiledContent(GameContentManifest manifest, CompiledGameContent compiled)
        {
            var serialized = new SerializedObject(manifest);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty("_compiledContent");
            if (property == null)
                throw new InvalidOperationException("GameContentManifest no longer exposes serialized field '_compiledContent'.");
            property.objectReferenceValue = compiled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manifest);
        }

        public static string ComputeCurrentSourceHash(GameContentManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            RequireCsv(manifest.ExperienceCsv, "experience");
            RequireCsv(manifest.EnemiesCsv, "enemies");
            RequireCsv(manifest.WavesCsv, "waves");
            RequireCsv(manifest.SkillsCsv, "skills");
            return ComputeCurrentSourceHash(manifest, CompileBabelDefinition());
        }

        private static string ComputeCurrentSourceHash(
            GameContentManifest manifest,
            BabelDefinition babel)
        {
            if (babel == null) throw new ArgumentNullException(nameof(babel));

            var builder = new StringBuilder();
            builder.Append("schema:").Append(CompiledGameContent.CurrentSchemaVersion).Append('\n');
            AppendSource(builder, "experience", manifest.ExperienceCsv.text);
            AppendSource(builder, "humans", manifest.EnemiesCsv.text);
            AppendSource(builder, "waves", manifest.WavesCsv.text);
            AppendSource(builder, "skills", manifest.SkillsCsv.text);
            builder.Append("babel:").Append(babel.LayerCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (int i = 0; i < babel.LayerCount; i++)
            {
                builder.Append(i.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(babel.GetBuildPointCount(i).ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(babel.GetGatewayCount(i).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static BabelDefinition CompileBabelDefinition()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSceneAssetPath) == null)
                throw new InvalidOperationException(
                    "Canonical GameScene is missing at '" + GameSceneAssetPath + "'.");

            Scene scene = SceneManager.GetSceneByPath(GameSceneAssetPath);
            bool openedForCompilation = !scene.IsValid() || !scene.isLoaded;
            if (openedForCompilation)
                scene = EditorSceneManager.OpenScene(GameSceneAssetPath, OpenSceneMode.Additive);

            try
            {
                var authorings = new List<BabelAuthoring>();
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    authorings.AddRange(roots[i].GetComponentsInChildren<BabelAuthoring>(true));

                if (authorings.Count != 1)
                    throw new InvalidOperationException(
                        "Canonical GameScene must contain exactly one BabelAuthoring; found " +
                        authorings.Count + ".");

                authorings[0].ValidateOrThrow();
                return authorings[0].CreateDefinition();
            }
            finally
            {
                if (openedForCompilation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AppendSource(StringBuilder builder, string role, string source)
        {
            string normalized = NormalizeSource(source);
            builder.Append(role).Append(':').Append(normalized.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(normalized).Append('\n');
        }

        private static string NormalizeSource(string source)
        {
            if (source == null) return string.Empty;
            if (source.Length > 0 && source[0] == '\uFEFF') source = source.Substring(1);
            return source.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static int[] Copy(IReadOnlyList<int> source)
        {
            var result = new int[source.Count];
            for (int i = 0; i < source.Count; i++) result[i] = source[i];
            return result;
        }

        private static float ParseFiniteFloat(string value, CsvRow row, string column)
        {
            float parsed;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                float.IsNaN(parsed) || float.IsInfinity(parsed))
                throw row.Error(column, "'" + value + "' is not a finite invariant-culture number.");
            return parsed;
        }

        private static void ValidateRow<T>(CsvRow row, Func<T> factory)
        {
            try
            {
                factory();
            }
            catch (Exception exception)
            {
                throw new FormatException(
                    "CSV '" + row.AssetPath + "', line " + row.Line + " contains invalid content: " + exception.Message,
                    exception);
            }
        }

        private sealed class CsvTable
        {
            private readonly Dictionary<string, int> _columns;

            private CsvTable(string assetPath, Dictionary<string, int> columns, List<CsvRow> rows)
            {
                AssetPath = assetPath;
                _columns = columns;
                Rows = rows;
                for (int i = 0; i < rows.Count; i++) rows[i].Attach(assetPath, columns);
            }

            public string AssetPath { get; }
            public List<CsvRow> Rows { get; }

            public static CsvTable Parse(TextAsset asset)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                List<RawCsvRow> rawRows = ParseRows(asset.text, path);
                if (rawRows.Count == 0) throw new FormatException("CSV '" + path + "' has no header.");

                RawCsvRow header = rawRows[0];
                var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Fields.Count; i++)
                {
                    string name = header.Fields[i].Trim();
                    if (name.Length == 0) throw new FormatException("CSV '" + path + "' contains an empty header at column " + (i + 1) + ".");
                    if (columns.ContainsKey(name)) throw new FormatException("CSV '" + path + "' contains duplicate header '" + name + "'.");
                    columns.Add(name, i);
                }

                var rows = new List<CsvRow>(Math.Max(0, rawRows.Count - 1));
                for (int i = 1; i < rawRows.Count; i++)
                {
                    RawCsvRow raw = rawRows[i];
                    if (raw.Fields.Count != header.Fields.Count)
                    {
                        throw new FormatException(
                            "CSV '" + path + "', line " + raw.Line + " has " + raw.Fields.Count +
                            " columns; expected " + header.Fields.Count + ".");
                    }
                    rows.Add(new CsvRow(raw.Line, raw.Fields.ToArray()));
                }

                return new CsvTable(path, columns, rows);
            }

            public void RequireColumns(params string[] names)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (!_columns.ContainsKey(names[i]))
                        throw new FormatException("CSV '" + AssetPath + "' is missing required column '" + names[i] + "'.");
                }
            }

            private static List<RawCsvRow> ParseRows(string source, string path)
            {
                source = source ?? string.Empty;
                if (source.Length > 0 && source[0] == '\uFEFF') source = source.Substring(1);

                var rows = new List<RawCsvRow>();
                var fields = new List<string>();
                var field = new StringBuilder();
                bool inQuotes = false;
                bool quoteClosed = false;
                int line = 1;
                int rowLine = 1;

                for (int i = 0; i < source.Length; i++)
                {
                    char character = source[i];
                    if (inQuotes)
                    {
                        if (character == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"')
                            {
                                field.Append('"');
                                i++;
                            }
                            else
                            {
                                inQuotes = false;
                                quoteClosed = true;
                            }
                            continue;
                        }

                        if (character == '\r' || character == '\n')
                        {
                            if (character == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                            field.Append('\n');
                            line++;
                        }
                        else
                        {
                            field.Append(character);
                        }
                        continue;
                    }

                    if (character == ',')
                    {
                        fields.Add(field.ToString());
                        field.Length = 0;
                        quoteClosed = false;
                        continue;
                    }

                    if (character == '\r' || character == '\n')
                    {
                        if (character == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                        fields.Add(field.ToString());
                        AddRow(rows, fields, rowLine);
                        fields.Clear();
                        field.Length = 0;
                        quoteClosed = false;
                        line++;
                        rowLine = line;
                        continue;
                    }

                    if (character == '"')
                    {
                        if (quoteClosed || (field.Length > 0 && !string.IsNullOrWhiteSpace(field.ToString())))
                            throw new FormatException("CSV '" + path + "', line " + line + " contains an unexpected quote.");
                        field.Length = 0;
                        inQuotes = true;
                        continue;
                    }

                    if (quoteClosed)
                    {
                        if (character == ' ' || character == '\t') continue;
                        throw new FormatException("CSV '" + path + "', line " + line + " contains data after a closing quote.");
                    }

                    field.Append(character);
                }

                if (inQuotes) throw new FormatException("CSV '" + path + "', line " + rowLine + " contains an unclosed quoted field.");
                if (field.Length > 0 || fields.Count > 0)
                {
                    fields.Add(field.ToString());
                    AddRow(rows, fields, rowLine);
                }
                return rows;
            }

            private static void AddRow(List<RawCsvRow> rows, List<string> fields, int line)
            {
                if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])) return;
                rows.Add(new RawCsvRow(line, new List<string>(fields)));
            }
        }

        private sealed class RawCsvRow
        {
            public RawCsvRow(int line, List<string> fields)
            {
                Line = line;
                Fields = fields;
            }

            public int Line { get; }
            public List<string> Fields { get; }
        }

        private sealed class CsvRow
        {
            private string[] _fields;
            private Dictionary<string, int> _columns;

            public CsvRow(int line, string[] fields)
            {
                Line = line;
                _fields = fields;
            }

            public int Line { get; }
            public string AssetPath { get; private set; }

            public void Attach(string assetPath, Dictionary<string, int> columns)
            {
                AssetPath = assetPath;
                _columns = columns;
            }

            public string Require(string column)
            {
                string value = Optional(column);
                if (value.Length == 0) throw Error(column, "value is required.");
                return value;
            }

            public string Optional(string column)
            {
                int index;
                if (_columns == null || !_columns.TryGetValue(column, out index))
                    throw Error(column, "column is not present.");
                return (_fields[index] ?? string.Empty).Trim();
            }

            public float RequiredFloat(string column)
            {
                return ParseFiniteFloat(Require(column), this, column);
            }

            public float OptionalFloat(string column, float defaultValue)
            {
                string value = Optional(column);
                return value.Length == 0 ? defaultValue : ParseFiniteFloat(value, this, column);
            }

            public int RequiredInt(string column)
            {
                string value = Require(column);
                int parsed;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    throw Error(column, "'" + value + "' is not an invariant-culture integer.");
                return parsed;
            }

            public bool RequiredBool(string column)
            {
                string value = Require(column);
                if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1") return true;
                if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0") return false;
                throw Error(column, "'" + value + "' is not a boolean (TRUE/FALSE/1/0).");
            }

            public FormatException Error(string column, string message)
            {
                return new FormatException(
                    "CSV '" + (AssetPath ?? "<unknown>") + "', line " + Line + ", column '" + column + "': " + message);
            }
        }
    }
}
