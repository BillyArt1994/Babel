using System;
using System.Collections.Generic;
using Babel.EditorTools.Content;
using Babel.Unity.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Babel.EditorTools.Validation
{
    public static class BabelContentValidator
    {
        public static GameContentManifest ValidateCanonicalManifest(BabelValidationReport report)
        {
            if (report == null) throw new ArgumentNullException("report");

            GameContentManifest manifest =
                AssetDatabase.LoadAssetAtPath<GameContentManifest>(BabelValidationPaths.Manifest);
            if (manifest == null)
            {
                report.AddError(
                    "CONTENT001",
                    "Canonical GameContentManifest is missing or has the wrong type.",
                    BabelValidationPaths.Manifest);
                return null;
            }

            string[] allManifestGuids = AssetDatabase.FindAssets("t:GameContentManifest");
            for (int i = 0; i < allManifestGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(allManifestGuids[i]);
                if (string.Equals(path, BabelValidationPaths.Manifest, StringComparison.Ordinal)) continue;
                report.AddWarning(
                    "CONTENT002",
                    "Additional GameContentManifest found; runtime uses the canonical manifest.",
                    path,
                    AssetDatabase.LoadMainAssetAtPath(path));
            }

            Validate(manifest, report);
            return manifest;
        }

        public static void Validate(GameContentManifest manifest, BabelValidationReport report)
        {
            if (manifest == null)
            {
                report.AddError("CONTENT001", "GameContentManifest is null.");
                return;
            }

            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            ValidateRequiredReference(manifest.ExperienceCsv, "_experienceCsv", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.EnemiesCsv, "_enemiesCsv", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.WavesCsv, "_wavesCsv", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.SkillsCsv, "_skillsCsv", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.DefaultFont, "_defaultFont", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.FallbackHumanView, "_fallbackHumanView", manifestPath, manifest, report);
            ValidateRequiredReference(manifest.FallbackSkillIcon, "_fallbackSkillIcon", manifestPath, manifest, report);
            ValidateCompiledContent(manifest, manifestPath, report);

            ValidateDistinctCsvRoles(manifest, manifestPath, report);
            ValidateMigrationLocation(manifest.ExperienceCsv, manifest, report);
            ValidateMigrationLocation(manifest.EnemiesCsv, manifest, report);
            ValidateMigrationLocation(manifest.WavesCsv, manifest, report);
            ValidateMigrationLocation(manifest.SkillsCsv, manifest, report);
            ValidateMigrationLocation(manifest.DefaultFont, manifest, report);
            ValidateMigrationLocation(manifest.FallbackHumanView, manifest, report);
            ValidateMigrationLocation(manifest.FallbackSkillIcon, manifest, report);

            BabelContentIds ids = BabelCsvValidator.Validate(
                manifest.ExperienceCsv,
                manifest.EnemiesCsv,
                manifest.WavesCsv,
                manifest.SkillsCsv,
                report);

            var validatedPrefabs = new HashSet<string>(StringComparer.Ordinal);
            ValidateHumanMappings(manifest, ids.EnemyIds, validatedPrefabs, report);
            ValidateSkillMappings(manifest, ids.SkillIds, report);
            ValidatePoolMappings(manifest, ids.EnemyIds, report);
            ValidateFont(manifest.DefaultFont, manifest, report);
        }

        private static void ValidateCompiledContent(
            GameContentManifest manifest,
            string manifestPath,
            BabelValidationReport report)
        {
            CompiledGameContent compiled = manifest.CompiledContent;
            if (compiled == null)
            {
                report.AddError(
                    "CONTENT005",
                    "Manifest must bind the generated CompiledGameContent asset. Run Babel/Content/Compile Game Content.",
                    manifestPath,
                    manifest);
                return;
            }

            try
            {
                compiled.ValidateRuntimeCatalogs();
            }
            catch (Exception exception)
            {
                report.AddError(
                    "CONTENT007",
                    "Compiled content is invalid: " + exception.Message,
                    AssetDatabase.GetAssetPath(compiled),
                    compiled);
                return;
            }

            if (manifest.ExperienceCsv == null || manifest.EnemiesCsv == null ||
                manifest.WavesCsv == null || manifest.SkillsCsv == null)
                return;

            string currentHash;
            try
            {
                currentHash = GameContentCompiler.ComputeCurrentSourceHash(manifest);
            }
            catch (Exception exception)
            {
                report.AddError(
                    "CONTENT006",
                    "Could not calculate current content source hash: " + exception.Message,
                    manifestPath,
                    manifest);
                return;
            }

            if (!string.Equals(compiled.SourceHash, currentHash, StringComparison.Ordinal))
            {
                report.AddError(
                    "CONTENT006",
                    "Compiled content is stale. Expected source hash " + currentHash +
                    " but found " + compiled.SourceHash + ". Recompile game content.",
                    AssetDatabase.GetAssetPath(compiled),
                    compiled);
            }
        }

        private static void ValidateRequiredReference(
            UnityEngine.Object value,
            string field,
            string path,
            UnityEngine.Object context,
            BabelValidationReport report)
        {
            if (value != null) return;
            report.AddError("CONTENT003", "Required Manifest field " + field + " is not assigned.", path, context);
        }

        private static void ValidateDistinctCsvRoles(
            GameContentManifest manifest,
            string path,
            BabelValidationReport report)
        {
            TextAsset[] csvAssets =
            {
                manifest.ExperienceCsv,
                manifest.EnemiesCsv,
                manifest.WavesCsv,
                manifest.SkillsCsv
            };
            string[] roleNames = { "experience", "enemies", "waves", "skills" };
            for (int i = 0; i < csvAssets.Length; i++)
            {
                if (csvAssets[i] == null) continue;
                for (int j = i + 1; j < csvAssets.Length; j++)
                {
                    if (csvAssets[j] == null || csvAssets[i] != csvAssets[j]) continue;
                    report.AddError(
                        "CONTENT004",
                        "The same TextAsset is assigned to both " + roleNames[i] + " and " + roleNames[j] + ".",
                        path,
                        manifest);
                }
            }
        }

        private static void ValidateMigrationLocation(
            UnityEngine.Object asset,
            UnityEngine.Object context,
            BabelValidationReport report)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (path.StartsWith("Assets/Babel/", StringComparison.Ordinal)) return;
            report.AddWarning(
                "MIGRATION001",
                "Referenced asset has not yet been moved under Assets/Babel.",
                path,
                context);
        }

        private static void ValidateHumanMappings(
            GameContentManifest manifest,
            HashSet<string> enemyIds,
            HashSet<string> validatedPrefabs,
            BabelValidationReport report)
        {
            var serialized = new SerializedObject(manifest);
            SerializedProperty entries = serialized.FindProperty("_humanViews");
            var explicitIds = new HashSet<string>(StringComparer.Ordinal);

            ValidatePrefab(manifest.FallbackHumanView, "fallbackHumanView", validatedPrefabs, report);

            if (entries == null)
            {
                report.AddError("MAP001", "Manifest _humanViews array is unavailable.", AssetDatabase.GetAssetPath(manifest), manifest);
                return;
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                string id = element.FindPropertyRelative("_id").stringValue.Trim();
                GameObject prefab =
                    element.FindPropertyRelative("_prefab").objectReferenceValue as GameObject;
                if (id.Length == 0)
                {
                    report.AddError("MAP001", "Human view entry " + i + " has an empty id.", AssetDatabase.GetAssetPath(manifest), manifest);
                    continue;
                }
                if (!explicitIds.Add(id))
                    report.AddError("MAP001", "Duplicate human view id '" + id + "'.", AssetDatabase.GetAssetPath(manifest), manifest);
                if (prefab == null)
                    report.AddError("MAP001", "Human view '" + id + "' has no prefab.", AssetDatabase.GetAssetPath(manifest), manifest);
                else
                {
                    ValidatePrefab(prefab, "human view '" + id + "'", validatedPrefabs, report);
                    ValidateMigrationLocation(prefab, manifest, report);
                }
                if (!enemyIds.Contains(id))
                    report.AddWarning("MAP004", "Human view id '" + id + "' is not present in enemies.csv.", AssetDatabase.GetAssetPath(manifest), manifest);
            }

            foreach (string enemyId in enemyIds)
            {
                if (explicitIds.Contains(enemyId)) continue;
                if (manifest.FallbackHumanView != null)
                    report.AddWarning("MAP002", "Enemy '" + enemyId + "' uses fallbackHumanView; add a dedicated prefab mapping.", AssetDatabase.GetAssetPath(manifest), manifest);
                else
                    report.AddError("MAP002", "Enemy '" + enemyId + "' has no view mapping or fallback.", AssetDatabase.GetAssetPath(manifest), manifest);
            }
        }

        private static void ValidateSkillMappings(
            GameContentManifest manifest,
            HashSet<string> skillIds,
            BabelValidationReport report)
        {
            var serialized = new SerializedObject(manifest);
            SerializedProperty entries = serialized.FindProperty("_skillIcons");
            var explicitIds = new HashSet<string>(StringComparer.Ordinal);

            ValidatePersistentSprite(manifest.FallbackSkillIcon, "fallbackSkillIcon", report);

            if (entries == null)
            {
                report.AddError("MAP001", "Manifest _skillIcons array is unavailable.", AssetDatabase.GetAssetPath(manifest), manifest);
                return;
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                string id = element.FindPropertyRelative("_id").stringValue.Trim();
                Sprite icon = element.FindPropertyRelative("_icon").objectReferenceValue as Sprite;
                if (id.Length == 0)
                {
                    report.AddError("MAP001", "Skill icon entry " + i + " has an empty id.", AssetDatabase.GetAssetPath(manifest), manifest);
                    continue;
                }
                if (!explicitIds.Add(id))
                    report.AddError("MAP001", "Duplicate skill icon id '" + id + "'.", AssetDatabase.GetAssetPath(manifest), manifest);
                if (icon == null)
                    report.AddError("MAP001", "Skill icon '" + id + "' has no Sprite.", AssetDatabase.GetAssetPath(manifest), manifest);
                else
                {
                    ValidatePersistentSprite(icon, "skill icon '" + id + "'", report);
                    ValidateMigrationLocation(icon, manifest, report);
                }
                if (!skillIds.Contains(id))
                    report.AddWarning("MAP004", "Skill icon id '" + id + "' is not present in skills.csv.", AssetDatabase.GetAssetPath(manifest), manifest);
            }

            foreach (string skillId in skillIds)
            {
                if (explicitIds.Contains(skillId)) continue;
                if (manifest.FallbackSkillIcon != null)
                    report.AddWarning("MAP003", "Skill '" + skillId + "' uses fallbackSkillIcon.", AssetDatabase.GetAssetPath(manifest), manifest);
                else
                    report.AddError("MAP003", "Skill '" + skillId + "' has no icon mapping or fallback.", AssetDatabase.GetAssetPath(manifest), manifest);
            }
        }

        private static void ValidatePoolMappings(
            GameContentManifest manifest,
            HashSet<string> enemyIds,
            BabelValidationReport report)
        {
            var serialized = new SerializedObject(manifest);
            SerializedProperty entries = serialized.FindProperty("_poolConfigs");
            var explicitIds = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null)
            {
                report.AddError("POOL001", "Manifest _poolConfigs array is unavailable.", AssetDatabase.GetAssetPath(manifest), manifest);
                return;
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty element = entries.GetArrayElementAtIndex(i);
                string id = element.FindPropertyRelative("_viewId").stringValue.Trim();
                int prewarm = element.FindPropertyRelative("_prewarm").intValue;
                int capacity = element.FindPropertyRelative("_expectedCapacity").intValue;
                if (id.Length == 0)
                {
                    report.AddError("POOL001", "Pool config entry " + i + " has an empty viewId.", AssetDatabase.GetAssetPath(manifest), manifest);
                    continue;
                }
                if (!explicitIds.Add(id))
                    report.AddError("POOL001", "Duplicate pool config for '" + id + "'.", AssetDatabase.GetAssetPath(manifest), manifest);
                if (prewarm < 0 || capacity < 1 || prewarm > capacity)
                    report.AddError("POOL002", "Pool config '" + id + "' has invalid prewarm/capacity values.", AssetDatabase.GetAssetPath(manifest), manifest);
                if (!enemyIds.Contains(id))
                    report.AddWarning("POOL003", "Pool config '" + id + "' is not present in enemies.csv.", AssetDatabase.GetAssetPath(manifest), manifest);
            }

            foreach (string enemyId in enemyIds)
            {
                if (!explicitIds.Contains(enemyId))
                    report.AddError("POOL001", "Enemy '" + enemyId + "' has no pool config.", AssetDatabase.GetAssetPath(manifest), manifest);
            }
        }

        private static void ValidatePrefab(
            GameObject prefab,
            string label,
            HashSet<string> validatedPaths,
            BabelValidationReport report)
        {
            if (prefab == null) return;
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) ||
                PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                report.AddError("ASSET001", label + " is not a persistent prefab asset.", path, prefab);
                return;
            }
            if (!validatedPaths.Add(path)) return;

            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
                if (missing > 0)
                    report.AddError("ASSET002", label + " contains " + missing + " missing script(s) on '" + transforms[i].name + "'.", path, transforms[i].gameObject);
            }

            MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            var warnedTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                Type type = behaviours[i].GetType();
                string fullName = type.FullName ?? type.Name;
                string typeNamespace = type.Namespace ?? string.Empty;
                if (fullName != "Babel.Enemy" && !typeNamespace.StartsWith("QFramework", StringComparison.Ordinal)) continue;
                if (!warnedTypes.Add(fullName)) continue;
                report.AddWarning("ASSET003", label + " still contains legacy runtime component '" + fullName + "'.", path, prefab);
            }
        }

        private static void ValidatePersistentSprite(Sprite sprite, string label, BabelValidationReport report)
        {
            if (sprite == null) return;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path) || !EditorUtility.IsPersistent(sprite))
                report.AddError("ASSET001", label + " is not a persistent Sprite asset.", path, sprite);
        }

        private static void ValidateFont(Font font, UnityEngine.Object context, BabelValidationReport report)
        {
            if (font == null) return;
            char[] smoke =
            {
                (char)0x5DF4, (char)0x522B, (char)0x5854, (char)0x4EBA,
                (char)0x7C7B, (char)0x5929, (char)0x795E, (char)0x6280,
                (char)0x80FD, (char)0x5347, (char)0x7EA7
            };
            for (int i = 0; i < smoke.Length; i++)
            {
                if (font.HasCharacter(smoke[i])) continue;
                report.AddWarning(
                    "FONT001",
                    "Default font is missing a Chinese smoke-test character U+" + ((int)smoke[i]).ToString("X4") + ".",
                    AssetDatabase.GetAssetPath(font),
                    context);
                break;
            }
        }
    }
}
