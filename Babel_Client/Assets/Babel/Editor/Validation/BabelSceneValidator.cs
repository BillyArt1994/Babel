using System;
using System.Collections.Generic;
using System.IO;
using Babel.Bootstrap;
using Babel.Unity.Infrastructure.Content;
using Babel.Unity.Infrastructure.SceneFlow;
using Babel.Unity.Infrastructure.Time;
using Babel.Unity.Presentation.Babel;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.EditorTools.Validation
{
    public static class BabelSceneValidator
    {
        private static readonly int[] ExpectedBuildPointCounts = { 8, 7, 6, 6, 5, 4 };
        private static readonly int[] ExpectedGatewayCounts = { 1, 1, 1, 1, 1, 0 };

        public static void ValidateBuildSettings(BabelValidationReport report)
        {
            if (report == null) throw new ArgumentNullException("report");

            var enabled = new List<EditorBuildSettingsScene>();
            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            for (int i = 0; i < configured.Length; i++)
                if (configured[i].enabled) enabled.Add(configured[i]);

            string[] required = { SceneNames.Boot, SceneNames.Menu, SceneNames.Game };
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < enabled.Count; i++)
            {
                string path = enabled[i].path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                int count;
                counts.TryGetValue(name, out count);
                counts[name] = count + 1;

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    report.AddError("BUILD002", "Enabled Build Settings path is not a valid Scene asset.", path);
            }

            for (int i = 0; i < required.Length; i++)
            {
                int count;
                counts.TryGetValue(required[i], out count);
                if (count != 1)
                    report.AddError("BUILD001", "Enabled Build Settings must contain exactly one '" + required[i] + "' scene; found " + count + ".");
            }

            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 1)
                    report.AddError("BUILD003", "Enabled scene filename '" + pair.Key + "' is ambiguous (" + pair.Value + " entries).");
            }

            if (enabled.Count == 0 ||
                !string.Equals(enabled[0].path.Replace('\\', '/'), BabelValidationPaths.BootScene, StringComparison.Ordinal))
            {
                report.AddError("BUILD002", "BootScene must be enabled at Build Settings index 0.", BabelValidationPaths.BootScene);
            }

            if (enabled.Count > required.Length)
                report.AddWarning("BUILD004", "Build Settings contains " + (enabled.Count - required.Length) + " extra enabled scene(s).");
        }

        public static void ValidateScene(
            Scene scene,
            GameContentManifest canonicalManifest,
            BabelValidationReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.AddError("SCENE000", "Scene is invalid or not loaded.", scene.path);
                return;
            }

            ValidateMissingReferences(scene, report);
            ValidateLegacyComponents(scene, report);

            if (scene.name == SceneNames.Boot)
                ValidateBootScene(scene, canonicalManifest, report);
            else if (scene.name == SceneNames.Menu)
                ValidateMenuScene(scene, report);
            else if (scene.name == SceneNames.Game)
                ValidateGameScene(scene, report);
        }

        private static void ValidateBootScene(
            Scene scene,
            GameContentManifest canonicalManifest,
            BabelValidationReport report)
        {
            List<ProjectRoot> roots = Collect<ProjectRoot>(scene);
            List<BootEntry> entries = Collect<BootEntry>(scene);
            List<RunRoot> runRoots = Collect<RunRoot>(scene);

            if (roots.Count != 1)
                report.AddError("BOOT001", "BootScene must contain exactly one ProjectRoot; found " + roots.Count + ".", scene.path);
            if (entries.Count != 1)
                report.AddError("BOOT001", "BootScene must contain exactly one BootEntry; found " + entries.Count + ".", scene.path);
            if (runRoots.Count > 0)
                report.AddError("BOOT005", "BootScene must not contain a RunRoot.", scene.path, runRoots[0]);

            if (roots.Count == 1)
            {
                ProjectRoot root = roots[0];
                if (root.ContentManifest == null)
                    report.AddError("BOOT003", "ProjectRoot has no GameContentManifest.", scene.path, root);
                else if (canonicalManifest != null && root.ContentManifest != canonicalManifest)
                    report.AddError("BOOT003", "ProjectRoot does not reference the canonical GameContentManifest.", scene.path, root);
            }

            if (roots.Count == 1 && entries.Count == 1)
            {
                var serialized = new SerializedObject(entries[0]);
                ProjectRoot boundRoot =
                    serialized.FindProperty("_projectRoot").objectReferenceValue as ProjectRoot;
                bool loadMenu = serialized.FindProperty("_loadMenuOnStart").boolValue;
                if (entries[0].gameObject != roots[0].gameObject || boundRoot != roots[0])
                    report.AddError("BOOT002", "BootEntry and ProjectRoot must share one GameObject and be explicitly bound.", scene.path, entries[0]);
                if (!loadMenu)
                    report.AddWarning("BOOT004", "BootEntry automatic menu routing is disabled.", scene.path, entries[0]);
            }
        }

        private static void ValidateMenuScene(Scene scene, BabelValidationReport report)
        {
            List<ProjectRoot> projectRoots = Collect<ProjectRoot>(scene);
            List<BootEntry> bootEntries = Collect<BootEntry>(scene);
            List<RunRoot> runRoots = Collect<RunRoot>(scene);
            if (projectRoots.Count > 0 || bootEntries.Count > 0 || runRoots.Count > 0)
                report.AddError("MENU001", "MainMenuScene must not create ProjectRoot, BootEntry, or RunRoot.", scene.path);
            ValidateMainCamera(scene, "MENU002", report);
        }

        private static void ValidateGameScene(Scene scene, BabelValidationReport report)
        {
            List<RunRoot> roots = Collect<RunRoot>(scene);
            List<RunDriver> drivers = Collect<RunDriver>(scene);
            if (roots.Count != 1)
                report.AddError("GAME001", "GameScene must contain exactly one RunRoot; found " + roots.Count + ".", scene.path);
            if (drivers.Count != 1)
                report.AddError("GAME001", "GameScene must contain exactly one RunDriver; found " + drivers.Count + ".", scene.path);

            if (roots.Count == 1 && drivers.Count == 1)
            {
                var serialized = new SerializedObject(roots[0]);
                RunDriver bound =
                    serialized.FindProperty("_driver").objectReferenceValue as RunDriver;
                float duration = serialized.FindProperty("_durationSeconds").floatValue;
                if (roots[0].gameObject != drivers[0].gameObject || bound != drivers[0])
                    report.AddError("GAME002", "RunRoot and RunDriver must share one GameObject and be explicitly bound.", scene.path, roots[0]);
                if (duration < 1f || float.IsNaN(duration) || float.IsInfinity(duration))
                    report.AddError("GAME003", "RunRoot duration must be finite and at least one second.", scene.path, roots[0]);
            }

            if (Collect<ProjectRoot>(scene).Count > 0 || Collect<BootEntry>(scene).Count > 0)
                report.AddError("GAME003", "GameScene must not create ProjectRoot or BootEntry.", scene.path);

            ValidateMainCamera(scene, "GAME004", report);
            ValidateBabelAuthoring(scene, report);
            ValidateLegacyBabelAuthoring(scene, report);
        }

        private static void ValidateMainCamera(
            Scene scene,
            string code,
            BabelValidationReport report)
        {
            List<Camera> cameras = Collect<Camera>(scene);
            int enabledMain = 0;
            Camera first = null;
            for (int i = 0; i < cameras.Count; i++)
            {
                if (!cameras[i].enabled || !cameras[i].gameObject.activeInHierarchy) continue;
                if (!cameras[i].CompareTag("MainCamera")) continue;
                if (first == null) first = cameras[i];
                enabledMain++;
            }
            if (enabledMain == 0)
                report.AddError(code, "Scene has no enabled camera tagged MainCamera.", scene.path);
            else if (enabledMain > 1)
                report.AddWarning(code, "Scene has multiple enabled cameras tagged MainCamera.", scene.path, first);
        }


        private static void ValidateBabelAuthoring(
            Scene scene,
            BabelValidationReport report)
        {
            List<BabelAuthoring> values = Collect<BabelAuthoring>(scene);
            if (values.Count != 1)
            {
                report.AddError("BABEL005", "GameScene must contain exactly one BabelAuthoring; found " + values.Count + ".", scene.path);
                return;
            }

            BabelAuthoring authoring = values[0];
            try
            {
                authoring.ValidateOrThrow();
                var definition = authoring.CreateDefinition();
                if (definition.LayerCount != ExpectedBuildPointCounts.Length)
                {
                    report.AddError("BABEL006", "BabelAuthoring must describe six bottom-to-top layers; found " + definition.LayerCount + ".", scene.path, authoring);
                    return;
                }

                for (int i = 0; i < definition.LayerCount; i++)
                {
                    if (definition.GetBuildPointCount(i) != ExpectedBuildPointCounts[i])
                        report.AddError("BABEL006", "BabelAuthoring layer " + (i + 1) + " must contain " + ExpectedBuildPointCounts[i] + " points; found " + definition.GetBuildPointCount(i) + ".", scene.path, authoring);
                    if (definition.GetGatewayCount(i) != ExpectedGatewayCounts[i])
                        report.AddError("BABEL007", "BabelAuthoring layer " + (i + 1) + " must contain " + ExpectedGatewayCounts[i] + " gateways; found " + definition.GetGatewayCount(i) + ".", scene.path, authoring);
                }
            }
            catch (Exception exception)
            {
                report.AddError("BABEL005", "BabelAuthoring is invalid: " + exception.Message, scene.path, authoring);
            }
        }

        private static void ValidateLegacyBabelAuthoring(
            Scene scene,
            BabelValidationReport report)
        {
            List<MonoBehaviour> towerManagers = CollectByFullName(scene, "Babel.TowerManager");
            if (towerManagers.Count != 1)
            {
                report.AddError("BABEL001", "GameScene must contain exactly one TowerManager; found " + towerManagers.Count + ".", scene.path);
                return;
            }

            var towerSerialized = new SerializedObject(towerManagers[0]);
            SerializedProperty layers = towerSerialized.FindProperty("layers");
            if (layers == null || layers.arraySize != 6)
            {
                int count = layers == null ? 0 : layers.arraySize;
                report.AddError("BABEL002", "TowerManager must bind six bottom-to-top layers; found " + count + ".", scene.path, towerManagers[0]);
                return;
            }

            var uniqueBuildPoints = new HashSet<int>();
            int totalBuildPoints = 0;
            for (int layerIndex = 0; layerIndex < layers.arraySize; layerIndex++)
            {
                MonoBehaviour path = layers.GetArrayElementAtIndex(layerIndex).objectReferenceValue as MonoBehaviour;
                if (path == null)
                {
                    report.AddError("BABEL002", "Tower layer " + (layerIndex + 1) + " is null.", scene.path, towerManagers[0]);
                    continue;
                }

                string expectedName = "Path_" + (layerIndex + 1).ToString("00");
                if (!string.Equals(path.name, expectedName, StringComparison.Ordinal))
                    report.AddError("BABEL002", "Tower layer order mismatch: expected '" + expectedName + "' but found '" + path.name + "'.", scene.path, path);

                var pathSerialized = new SerializedObject(path);
                SerializedProperty points = pathSerialized.FindProperty("wayPointList");
                int actualCount = points == null ? 0 : points.arraySize;
                if (actualCount != ExpectedBuildPointCounts[layerIndex])
                    report.AddError("BABEL003", path.name + " must contain " + ExpectedBuildPointCounts[layerIndex] + " BuildPoints; found " + actualCount + ".", scene.path, path);

                int gateways = 0;
                for (int pointIndex = 0; points != null && pointIndex < points.arraySize; pointIndex++)
                {
                    MonoBehaviour point =
                        points.GetArrayElementAtIndex(pointIndex).objectReferenceValue as MonoBehaviour;
                    if (point == null)
                    {
                        report.AddError("BABEL003", path.name + " contains a null BuildPoint at index " + pointIndex + ".", scene.path, path);
                        continue;
                    }
                    if (!uniqueBuildPoints.Add(point.GetInstanceID()))
                        report.AddError("BABEL003", "BuildPoint '" + point.name + "' is referenced more than once.", scene.path, point);
                    totalBuildPoints++;

                    var pointSerialized = new SerializedObject(point);
                    SerializedProperty gateway = pointSerialized.FindProperty("isGateway");
                    SerializedProperty amount = pointSerialized.FindProperty("buildAmount");
                    if (gateway != null && gateway.boolValue) gateways++;
                    if (amount == null || amount.intValue <= 0)
                        report.AddError("BABEL004", "BuildPoint '" + point.name + "' must have positive buildAmount.", scene.path, point);
                }

                if (gateways != ExpectedGatewayCounts[layerIndex])
                    report.AddError("BABEL004", path.name + " must contain " + ExpectedGatewayCounts[layerIndex] + " Gateway(s); found " + gateways + ".", scene.path, path);
            }

            if (totalBuildPoints != 36 || uniqueBuildPoints.Count != 36)
                report.AddError("BABEL003", "Babel authoring must contain 36 unique BuildPoints; found " + uniqueBuildPoints.Count + ".", scene.path, towerManagers[0]);

            List<MonoBehaviour> spawnPoints = CollectByFullName(scene, "Babel.SpawnPoint");
            if (spawnPoints.Count != 2)
                report.AddError("SPAWN001", "GameScene must contain two SpawnPoints; found " + spawnPoints.Count + ".", scene.path);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                SerializedProperty id = new SerializedObject(spawnPoints[i]).FindProperty("Id");
                if (id == null || string.IsNullOrWhiteSpace(id.stringValue))
                    report.AddError("SPAWN002", "SpawnPoint '" + spawnPoints[i].name + "' has an empty Id.", scene.path, spawnPoints[i]);
            }
        }

        private static void ValidateMissingReferences(
            Scene scene,
            BabelValidationReport report)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    GameObject go = transforms[i].gameObject;
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missing > 0)
                        report.AddError("SCENE001", "GameObject '" + GetHierarchyPath(go) + "' has " + missing + " missing script(s).", scene.path, go);
                    if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.MissingAsset)
                        report.AddError("SCENE002", "GameObject '" + GetHierarchyPath(go) + "' has a missing prefab source.", scene.path, go);
                }
            }
        }

        private static void ValidateLegacyComponents(
            Scene scene,
            BabelValidationReport report)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var warnedTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] == null) continue;
                    Type type = behaviours[i].GetType();
                    string typeNamespace = type.Namespace ?? string.Empty;
                    if (!typeNamespace.StartsWith("QFramework", StringComparison.Ordinal)) continue;
                    string fullName = type.FullName ?? type.Name;
                    if (!warnedTypes.Add(fullName)) continue;
                    report.AddWarning("SCENE003", "Scene still contains legacy QFramework component '" + fullName + "'.", scene.path, behaviours[i]);
                }
            }
        }

        private static List<T> Collect<T>(Scene scene) where T : Component
        {
            var values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                values.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return values;
        }

        private static List<MonoBehaviour> CollectByFullName(Scene scene, string fullName)
        {
            var values = new List<MonoBehaviour>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null && behaviours[i].GetType().FullName == fullName)
                        values.Add(behaviours[i]);
                }
            }
            return values;
        }

        private static string GetHierarchyPath(GameObject value)
        {
            string path = value.name;
            Transform parent = value.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
