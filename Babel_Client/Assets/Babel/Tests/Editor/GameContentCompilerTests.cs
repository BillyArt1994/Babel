using Babel.EditorTools.Content;
using Babel.Gameplay.Content;
using Babel.Gameplay.World;
using Babel.Unity.Infrastructure.Content;
using Babel.Unity.Presentation.Babel;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.Tests
{
    public sealed class GameContentCompilerTests
    {
        [Test]
        public void Compile_ShippedCanonicalContent_GeneratesAndBindsAsset()
        {
            CompiledGameContent compiled = GameContentCompiler.Compile(false);
            GameContentManifest manifest =
                AssetDatabase.LoadAssetAtPath<GameContentManifest>(GameContentCompiler.ManifestAssetPath);

            Assert.That(compiled, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(compiled), Is.EqualTo(GameContentCompiler.GeneratedAssetPath));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.CompiledContent, Is.SameAs(compiled));
            Assert.That(compiled.SchemaVersion, Is.EqualTo(CompiledGameContent.CurrentSchemaVersion));
        }

        [Test]
        public void Compile_WhenSourcesAreUnchanged_ProducesStableHashAndUpdatesInPlace()
        {
            CompiledGameContent first = GameContentCompiler.Compile(false);
            string firstHash = first.SourceHash;
            CompiledGameContent second = GameContentCompiler.Compile(false);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.SourceHash, Is.EqualTo(firstHash));
            Assert.That(firstHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void CompiledShippedCatalogs_HaveExpectedCountsAndValidCrossReferences()
        {
            CompiledGameContent compiled = GameContentCompiler.Compile(false);

            Assert.That(compiled.HumanCount, Is.EqualTo(6));
            Assert.That(compiled.WaveCount, Is.EqualTo(8));
            Assert.That(compiled.SkillCount, Is.EqualTo(9));
            Assert.That(compiled.ExperienceThresholdCount, Is.EqualTo(19));
            Assert.That(compiled.BabelLayerCount, Is.EqualTo(6));

            HumanCatalog humans = compiled.CreateHumanCatalog();
            WaveCatalog waves = compiled.CreateWaveCatalog(humans);
            SkillCatalog skills = compiled.CreateSkillCatalog();
            ExperienceTable experience = compiled.CreateExperienceTable();
            BabelDefinition babel = compiled.CreateBabelDefinition();

            Assert.That(humans.Count, Is.EqualTo(6));
            Assert.That(waves.Count, Is.EqualTo(8));
            Assert.That(skills.Count, Is.EqualTo(9));
            Assert.That(experience.MaxLevel, Is.EqualTo(20));
            Assert.That(babel.BuildPointCounts, Is.EqualTo(new[] { 8, 7, 6, 6, 5, 4 }));
            Assert.That(babel.GatewayCounts, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 0 }));
            Assert.DoesNotThrow(compiled.ValidateRuntimeCatalogs);
        }

        [Test]
        public void CompiledContent_CreatesValidatedGameRuntimeContent()
        {
            CompiledGameContent compiled = GameContentCompiler.Compile(false);
            GameRuntimeContent runtime = compiled.CreateGameRuntimeContent();

            Assert.That(runtime.Humans.Count, Is.EqualTo(6));
            Assert.That(runtime.Waves.Count, Is.EqualTo(8));
            Assert.That(runtime.Skills.Count, Is.EqualTo(9));
            Assert.That(runtime.Experience.MaxLevel, Is.EqualTo(20));
            Assert.That(runtime.Babel.LayerCount, Is.EqualTo(6));
        }

        [Test]
        public void Compile_BabelCatalogMatchesExplicitGameSceneAuthoring()
        {
            CompiledGameContent compiled = GameContentCompiler.Compile(false);
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    GameContentCompiler.GameSceneAssetPath,
                    OpenSceneMode.Single);
                BabelAuthoring authoring = null;
                int count = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    BabelAuthoring[] values =
                        roots[i].GetComponentsInChildren<BabelAuthoring>(true);
                    count += values.Length;
                    if (values.Length > 0) authoring = values[0];
                }

                Assert.That(count, Is.EqualTo(1));
                Assert.That(authoring, Is.Not.Null);
                BabelDefinition authored = authoring.CreateDefinition();
                BabelDefinition generated = compiled.CreateBabelDefinition();
                Assert.That(generated.BuildPointCounts, Is.EqualTo(authored.BuildPointCounts));
                Assert.That(generated.GatewayCounts, Is.EqualTo(authored.GatewayCounts));
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

    }
}
