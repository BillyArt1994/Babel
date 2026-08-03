using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Babel.EditorTools.Validation
{
    /// <summary>
    /// Blocks the retired QFramework/UIKit stack, the legacy Global state owner,
    /// and serialized components that would become missing when those assets are absent.
    /// </summary>
    public static class BabelDependencyValidator
    {
        private const string LegacyGlobalPath = "Assets/Scripts/Global.cs";
        private const string QFrameworkRoot = "Assets/QFramework";
        private const string QFrameworkDataRoot = "Assets/QFrameworkData";
        private const string ProjectAssetRoot = "Assets/Babel";

        private static readonly string[] ForbiddenFolders =
        {
            QFrameworkRoot,
            QFrameworkDataRoot
        };

        private static readonly string[] ForbiddenSerializedMarkers =
        {
            "ArchitectureFullTypeName:",
            "ViewControllerFullTypeName:",
            "guid: c70aac0028afd42eabc9e0c55824c824",
            "guid: b6b7a3e7dc894eaca1053ef166cdad33",
            "guid: 0d51f3a7c41ab0346b49ae50d456bece"
        };

        public static void Validate(BabelValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            ValidateForbiddenAssets(report);
            ValidateAssemblyDefinitions(report);
            ValidateSerializedDependencyMarkers(report);
            ValidateMonoScripts(report);
            ValidateLoadedAssemblies(report);
            ValidatePrefabAssets(report);
            ValidateSceneAssets(report);
        }

        private static void ValidateForbiddenAssets(BabelValidationReport report)
        {
            for (int i = 0; i < ForbiddenFolders.Length; i++)
            {
                string path = ForbiddenFolders[i];
                if (!AssetDatabase.IsValidFolder(path) && !Directory.Exists(ToAbsolutePath(path)))
                    continue;

                report.AddError(
                    "DEPENDENCY001",
                    "Retired QFramework asset folder must not exist.",
                    path,
                    AssetDatabase.LoadMainAssetAtPath(path));
            }

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(LegacyGlobalPath) != null ||
                File.Exists(ToAbsolutePath(LegacyGlobalPath)))
            {
                report.AddError(
                    "DEPENDENCY001",
                    "Legacy static state owner Babel.Global must not exist.",
                    LegacyGlobalPath,
                    AssetDatabase.LoadAssetAtPath<MonoScript>(LegacyGlobalPath));
            }
        }

        private static void ValidateAssemblyDefinitions(BabelValidationReport report)
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            var definitions = new List<ParsedAssemblyDefinition>();
            var forbiddenDefinitionGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;

                AssemblyDefinitionData data;
                try
                {
                    data = JsonUtility.FromJson<AssemblyDefinitionData>(
                        File.ReadAllText(ToAbsolutePath(path)));
                }
                catch (Exception exception)
                {
                    report.AddError(
                        "DEPENDENCY006",
                        "Could not inspect assembly definition: " + exception.Message,
                        path,
                        AssetDatabase.LoadMainAssetAtPath(path));
                    continue;
                }

                if (data == null) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                definitions.Add(new ParsedAssemblyDefinition(path, data));

                if (!IsForbiddenAssemblyName(data.name)) continue;
                if (!string.IsNullOrEmpty(guid)) forbiddenDefinitionGuids.Add(guid);
                if (IsUnderForbiddenFolder(path)) continue;

                report.AddError(
                    "DEPENDENCY002",
                    "Forbidden QFramework assembly definition '" + data.name + "' remains.",
                    path,
                    AssetDatabase.LoadMainAssetAtPath(path));
            }

            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                ParsedAssemblyDefinition definition = definitions[definitionIndex];
                if (IsUnderForbiddenFolder(definition.Path) || definition.Data.references == null)
                    continue;

                for (int referenceIndex = 0;
                     referenceIndex < definition.Data.references.Length;
                     referenceIndex++)
                {
                    string reference = definition.Data.references[referenceIndex] ?? string.Empty;
                    bool forbidden = IsForbiddenAssemblyName(reference);
                    if (!forbidden && reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                        forbidden = forbiddenDefinitionGuids.Contains(reference.Substring(5));

                    if (!forbidden) continue;
                    report.AddError(
                        "DEPENDENCY002",
                        "Assembly definition '" + definition.Data.name +
                        "' references retired assembly '" + reference + "'.",
                        definition.Path,
                        AssetDatabase.LoadMainAssetAtPath(definition.Path));
                }
            }
        }

        private static void ValidateMonoScripts(BabelValidationReport report)
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            var reportedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (IsUnderForbiddenFolder(path)) continue;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script == null ? null : script.GetClass();
                if (!IsForbiddenType(type) || !reportedPaths.Add(path)) continue;

                report.AddError(
                    "DEPENDENCY003",
                    "Forbidden runtime type '" + type.FullName + "' remains in project source.",
                    path,
                    script);
            }
        }

        private static void ValidateSerializedDependencyMarkers(BabelValidationReport report)
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            for (int assetIndex = 0; assetIndex < assetPaths.Length; assetIndex++)
            {
                string path = assetPaths[assetIndex];
                if (!path.StartsWith(ProjectAssetRoot + "/", StringComparison.Ordinal) ||
                    (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                     !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)))
                    continue;

                string serializedText;
                try
                {
                    serializedText = File.ReadAllText(ToAbsolutePath(path));
                }
                catch (Exception exception)
                {
                    report.AddError(
                        "DEPENDENCY006",
                        "Could not inspect serialized asset dependencies: " + exception.Message,
                        path,
                        AssetDatabase.LoadMainAssetAtPath(path));
                    continue;
                }

                for (int markerIndex = 0;
                     markerIndex < ForbiddenSerializedMarkers.Length;
                     markerIndex++)
                {
                    string marker = ForbiddenSerializedMarkers[markerIndex];
                    if (serializedText.IndexOf(marker, StringComparison.Ordinal) < 0) continue;

                    report.AddError(
                        "DEPENDENCY007",
                        "Serialized asset still contains retired QFramework marker '" + marker + "'.",
                        path,
                        AssetDatabase.LoadMainAssetAtPath(path));
                }
            }
        }

        private static void ValidateLoadedAssemblies(BabelValidationReport report)
        {
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var reportedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < assemblies.Length; i++)
            {
                string name = assemblies[i].GetName().Name ?? string.Empty;
                if (!IsForbiddenAssemblyName(name) || !reportedNames.Add(name)) continue;
                report.AddError(
                    "DEPENDENCY003",
                    "Forbidden QFramework assembly '" + name + "' is loaded.");
            }
        }

        private static void ValidatePrefabAssets(BabelValidationReport report)
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { ProjectAssetRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.AddError(
                        "DEPENDENCY006",
                        "Project prefab could not be loaded for dependency validation.",
                        path);
                    continue;
                }

                ValidateHierarchy(prefab, path, "Prefab", report);
            }
        }

        private static void ValidateSceneAssets(BabelValidationReport report)
        {
            string[] sceneGuids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { ProjectAssetRoot });
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    UnityEngine.SceneManagement.Scene scene =
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                        ValidateHierarchy(roots[rootIndex], path, "Scene", report);
                }
            }
            catch (Exception exception)
            {
                report.AddError(
                    "DEPENDENCY006",
                    "Project scene dependency validation could not complete: " + exception.Message);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ValidateHierarchy(
            GameObject root,
            string assetPath,
            string assetKind,
            BabelValidationReport report)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                GameObject gameObject = transforms[transformIndex].gameObject;
                int missingScripts =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingScripts > 0)
                {
                    report.AddError(
                        "DEPENDENCY004",
                        assetKind + " GameObject '" + GetHierarchyPath(gameObject) +
                        "' has " + missingScripts + " missing script(s).",
                        assetPath,
                        gameObject);
                }

                if (PrefabUtility.GetPrefabInstanceStatus(gameObject) ==
                    PrefabInstanceStatus.MissingAsset)
                {
                    report.AddError(
                        "DEPENDENCY004",
                        assetKind + " GameObject '" + GetHierarchyPath(gameObject) +
                        "' has a missing prefab source.",
                        assetPath,
                        gameObject);
                }

                MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
                for (int behaviourIndex = 0;
                     behaviourIndex < behaviours.Length;
                     behaviourIndex++)
                {
                    MonoBehaviour behaviour = behaviours[behaviourIndex];
                    if (behaviour == null || !IsForbiddenType(behaviour.GetType())) continue;
                    report.AddError(
                        "DEPENDENCY005",
                        assetKind + " GameObject '" + GetHierarchyPath(gameObject) +
                        "' contains retired component '" + behaviour.GetType().FullName + "'.",
                        assetPath,
                        behaviour);
                }
            }
        }

        private static bool IsForbiddenType(Type type)
        {
            if (type == null) return false;
            string typeNamespace = type.Namespace ?? string.Empty;
            string fullName = type.FullName ?? type.Name;
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            return string.Equals(fullName, "Babel.Global", StringComparison.Ordinal) ||
                   typeNamespace.StartsWith("QFramework", StringComparison.Ordinal) ||
                   IsForbiddenAssemblyName(assemblyName);
        }

        private static bool IsForbiddenAssemblyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return string.Equals(name, "QFramework", StringComparison.Ordinal) ||
                   name.StartsWith("QFramework.", StringComparison.Ordinal) ||
                   string.Equals(name, "UIKit", StringComparison.Ordinal) ||
                   name.StartsWith("UIKit.", StringComparison.Ordinal);
        }

        private static bool IsUnderForbiddenFolder(string path)
        {
            for (int i = 0; i < ForbiddenFolders.Length; i++)
            {
                string folder = ForbiddenFolders[i];
                if (string.Equals(path, folder, StringComparison.Ordinal) ||
                    path.StartsWith(folder + "/", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform parent = gameObject.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name;
            public string[] references;
        }

        private sealed class ParsedAssemblyDefinition
        {
            public ParsedAssemblyDefinition(string path, AssemblyDefinitionData data)
            {
                Path = path;
                Data = data;
            }

            public string Path { get; }
            public AssemblyDefinitionData Data { get; }
        }
    }
}
