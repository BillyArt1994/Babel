using System.Linq;
using Babel.EditorTools.Validation;
using Babel.Unity.Infrastructure.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Babel.Tests
{
    public sealed class ProjectValidationTests
    {
        [Test]
        public void ShippedProject_HasNoValidationErrors()
        {
            BabelValidationReport report = BabelProjectValidation.ValidateProject(true);

            Assert.That(
                report.ErrorCount,
                Is.Zero,
                report.ToBuildMessage());
        }

        [Test]
        public void ShippedProject_HasNoQFrameworkDependencies()
        {
            var report = new BabelValidationReport();

            BabelDependencyValidator.Validate(report);

            Assert.That(
                report.ErrorCount,
                Is.Zero,
                report.ToBuildMessage());
        }

        [Test]
        public void EmptyManifest_ReportsRequiredContentErrors()
        {
            GameContentManifest manifest = ScriptableObject.CreateInstance<GameContentManifest>();
            try
            {
                var report = new BabelValidationReport();

                BabelContentValidator.Validate(manifest, report);

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.ErrorCount, Is.GreaterThanOrEqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void ManifestWithoutCompiledContent_ReportsContent005()
        {
            GameContentManifest manifest = CloneCanonicalManifest();
            try
            {
                var serialized = new SerializedObject(manifest);
                serialized.FindProperty("_compiledContent").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var report = new BabelValidationReport();

                BabelContentValidator.Validate(manifest, report);

                Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("CONTENT005"));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void ManifestWithStaleCompiledHash_ReportsContent006()
        {
            GameContentManifest canonical =
                AssetDatabase.LoadAssetAtPath<GameContentManifest>(
                    "Assets/Babel/Content/Manifests/GameContentManifest.asset");
            Assert.That(canonical, Is.Not.Null);
            Assert.That(canonical.CompiledContent, Is.Not.Null);

            GameContentManifest manifest = Object.Instantiate(canonical);
            CompiledGameContent stale = Object.Instantiate(canonical.CompiledContent);
            try
            {
                var staleSerialized = new SerializedObject(stale);
                staleSerialized.FindProperty("_sourceHash").stringValue = new string('0', 64);
                staleSerialized.ApplyModifiedPropertiesWithoutUndo();

                var manifestSerialized = new SerializedObject(manifest);
                manifestSerialized.FindProperty("_compiledContent").objectReferenceValue = stale;
                manifestSerialized.ApplyModifiedPropertiesWithoutUndo();
                var report = new BabelValidationReport();

                BabelContentValidator.Validate(manifest, report);

                Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("CONTENT006"));
            }
            finally
            {
                Object.DestroyImmediate(stale);
                Object.DestroyImmediate(manifest);
            }
        }

        private static GameContentManifest CloneCanonicalManifest()
        {
            GameContentManifest canonical =
                AssetDatabase.LoadAssetAtPath<GameContentManifest>(
                    "Assets/Babel/Content/Manifests/GameContentManifest.asset");
            Assert.That(canonical, Is.Not.Null);
            return Object.Instantiate(canonical);
        }

    }
}
