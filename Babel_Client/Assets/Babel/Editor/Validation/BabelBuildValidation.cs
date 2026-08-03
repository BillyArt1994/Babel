using System;
using Babel.EditorTools.Content;
using Babel.Unity.Infrastructure.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.EditorTools.Validation
{
    public static class BabelProjectValidation
    {
        [MenuItem("Babel/Validation/Validate Project", false, 100)]
        private static void ValidateProjectMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BabelValidationReport report = ValidateProject(true);
            report.Log();
            EditorUtility.DisplayDialog(
                "Babel Project Validation",
                report.GetSummary(),
                report.HasErrors ? "Review Console" : "OK");
        }

        public static BabelValidationReport ValidateProject(bool includeEnabledScenes)
        {
            var report = new BabelValidationReport();
            BabelDependencyValidator.Validate(report);
            GameContentManifest manifest = BabelContentValidator.ValidateCanonicalManifest(report);
            BabelSceneValidator.ValidateBuildSettings(report);
            if (includeEnabledScenes)
                ValidateEnabledScenes(manifest, report);
            return report;
        }

        public static string ValidateProjectForAutomation()
        {
            BabelValidationReport report = ValidateProject(true);
            report.Log();
            if (report.HasErrors)
                throw new InvalidOperationException(report.ToBuildMessage());
            return report.GetSummary();
        }

        private static void ValidateEnabledScenes(
            GameContentManifest manifest,
            BabelValidationReport report)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                for (int i = 0; i < scenes.Length; i++)
                {
                    if (!scenes[i].enabled) continue;
                    Scene scene = EditorSceneManager.OpenScene(scenes[i].path, OpenSceneMode.Single);
                    BabelSceneValidator.ValidateScene(scene, manifest, report);
                }
            }
            catch (Exception exception)
            {
                report.AddError("SCENE000", "Scene validation could not complete: " + exception.Message);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }

    public sealed class BabelBuildValidationPreprocessor :
        IPreprocessBuildWithReport,
        IProcessSceneWithReport
    {
        public int callbackOrder { get { return -1000; } }

        public void OnPreprocessBuild(BuildReport buildReport)
        {
            // Build policy: the generated catalog is derived data, so a build always refreshes it.
            // Authoring or cross-reference failures are non-recoverable and explicitly block the build.
            try
            {
                GameContentCompiler.Compile(false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw new BuildFailedException(
                    "Babel content compilation failed before build: " + exception.Message);
            }

            var report = new BabelValidationReport();
            BabelDependencyValidator.Validate(report);
            BabelContentValidator.ValidateCanonicalManifest(report);
            BabelSceneValidator.ValidateBuildSettings(report);
            report.Log();
            if (report.HasErrors)
                throw new BuildFailedException(report.ToBuildMessage());
        }

        public void OnProcessScene(Scene scene, BuildReport buildReport)
        {
            var report = new BabelValidationReport();
            GameContentManifest manifest =
                AssetDatabase.LoadAssetAtPath<GameContentManifest>(BabelValidationPaths.Manifest);
            BabelSceneValidator.ValidateScene(scene, manifest, report);
            report.Log();
            if (report.HasErrors)
                throw new BuildFailedException(report.ToBuildMessage());
        }
    }
}
