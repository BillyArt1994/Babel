using System;
using System.Collections.Generic;
using System.Linq;
using Babel.Gameplay.Content;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class ContentCatalogTests
    {
        [Test]
        public void HumanCatalog_DefensivelyCopiesEntriesAndProvidesOrdinalLookup()
        {
            HumanDefinition worker = MakeHuman("worker");
            var source = new List<HumanDefinition> { worker };
            var catalog = new HumanCatalog(source);

            source.Clear();

            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.GetRequired("worker"), Is.SameAs(worker));
            Assert.That(catalog.TryGet("Worker", out _), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<HumanDefinition>)catalog.All).Clear());
        }

        [Test]
        public void HumanCatalog_RejectsDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => new HumanCatalog(new[] { MakeHuman("worker"), MakeHuman("worker") }));
        }

        [Test]
        public void HumanDefinition_RejectsInvalidIdsAndNonFiniteValues()
        {
            Assert.Throws<ArgumentException>(() => MakeHuman(" worker"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HumanDefinition(
                "worker", "Worker", float.NaN, 1f, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HumanDefinition(
                "worker", "Worker", 10f, float.PositiveInfinity, 1, 1, 1));
        }

        [Test]
        public void WaveDefinition_DefensivelyCopiesPool()
        {
            var source = new List<PoolEntry> { new PoolEntry("worker", 1f) };
            WaveDefinition wave = MakeWave("opening", source);

            source.Clear();

            Assert.That(wave.Pool.Select(entry => entry.HumanId), Is.EqualTo(new[] { "worker" }));
            Assert.Throws<NotSupportedException>(() => ((IList<PoolEntry>)wave.Pool).Clear());
        }

        [Test]
        public void WaveCatalog_RejectsDuplicatesAndInvalidWaveNumbers()
        {
            WaveDefinition wave = MakeWave("opening", new[] { new PoolEntry("worker", 1f) });
            Assert.Throws<ArgumentException>(() => new WaveCatalog(new[] { wave, wave }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoolEntry("worker", float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveDefinition(
                "bad", 10f, 5f, WaveSpawnMode.Timed,
                new[] { new PoolEntry("worker", 1f) }, 1, 1, 1f, "default"));
        }

        [Test]
        public void WaveCatalog_ValidateRejectsUnknownHumanReference()
        {
            var humans = new HumanCatalog(new[] { MakeHuman("worker") });
            var valid = new WaveCatalog(new[] { MakeWave("valid", new[] { new PoolEntry("worker", 1f) }) });
            var invalid = new WaveCatalog(new[] { MakeWave("invalid", new[] { new PoolEntry("elite", 1f) }) });

            Assert.DoesNotThrow(() => valid.Validate(humans));
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => invalid.Validate(humans));
            StringAssert.Contains("elite", error.Message);
        }

        [Test]
        public void SkillCatalog_UsesSkillIdAndLevelAsImmutableKey()
        {
            SkillDefinition levelOne = MakeSkill("meteor", 1, 2);
            SkillDefinition levelTwo = MakeSkill("meteor", 2, 2);
            var source = new List<SkillDefinition> { levelTwo, levelOne };
            var catalog = new SkillCatalog(source);

            source.Clear();

            Assert.That(catalog.GetRequired("meteor", 2), Is.SameAs(levelTwo));
            Assert.That(catalog.GetLevels("meteor").Select(skill => skill.Level), Is.EqualTo(new[] { 1, 2 }));
            Assert.Throws<NotSupportedException>(() => ((IList<EffectDefinition>)levelOne.Effects).Clear());
            Assert.DoesNotThrow(catalog.Validate);
        }

        [Test]
        public void SkillCatalog_RejectsDuplicateSkillLevelKey()
        {
            Assert.Throws<ArgumentException>(() => new SkillCatalog(new[]
            {
                MakeSkill("meteor", 1, 1),
                MakeSkill("meteor", 1, 1)
            }));
        }

        [Test]
        public void SkillCatalog_ValidateChecksEvolutionAndLevelReferences()
        {
            var missingEvolutionSource = new SkillCatalog(new[]
            {
                MakeSkill("meteor_evolved", upgradesFrom: "meteor")
            });
            var missingLevel = new SkillCatalog(new[]
            {
                MakeSkill("meteor", 1, 2)
            });
            var valid = new SkillCatalog(new[]
            {
                MakeSkill("meteor"),
                MakeSkill("meteor_evolved", upgradesFrom: "meteor")
            });

            Assert.Throws<InvalidOperationException>(missingEvolutionSource.Validate);
            Assert.Throws<InvalidOperationException>(missingLevel.Validate);
            Assert.DoesNotThrow(valid.Validate);
        }

        [Test]
        public void SkillAndEffectDefinitions_RejectInvalidNumerics()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EffectDefinition("hit_single", damage: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EffectDefinition("dot_aoe", durationSeconds: -2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeSkill("bad", chance: 1.01f));
        }

        [Test]
        public void ExperienceTable_ResolvesThresholdsCumulativeXpAndLevels()
        {
            var authored = new[] { 5f, 12f, 21f };
            var table = new ExperienceTable(authored);
            authored[0] = 999f;

            Assert.That(table.MinLevel, Is.EqualTo(1));
            Assert.That(table.MaxLevel, Is.EqualTo(4));
            Assert.That(table.GetRequiredXpForNextLevel(1), Is.EqualTo(5f));
            Assert.That(table.GetCumulativeXpToReachLevel(1), Is.Zero);
            Assert.That(table.GetCumulativeXpToReachLevel(4), Is.EqualTo(38f));
            Assert.That(table.ResolveLevel(4.99f), Is.EqualTo(1));
            Assert.That(table.ResolveLevel(5f), Is.EqualTo(2));
            Assert.That(table.ResolveLevel(16.99f), Is.EqualTo(2));
            Assert.That(table.ResolveLevel(17f), Is.EqualTo(3));
            Assert.That(table.ResolveLevel(38f), Is.EqualTo(4));
        }

        [Test]
        public void ExperienceTable_RejectsInvalidOrDescendingCurve()
        {
            Assert.Throws<ArgumentException>(() => new ExperienceTable(Array.Empty<float>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceTable(5f, float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new ExperienceTable(5f, 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceTable(5f).ResolveLevel(float.NaN));
        }

        [Test]
        public void BabelDefinition_DefaultIsTheAuthoredSixLayerBottomToTopShape()
        {
            var definition = new BabelDefinition();

            Assert.That(definition.LayerCount, Is.EqualTo(6));
            Assert.That(definition.BuildPointCounts, Is.EqualTo(new[] { 8, 7, 6, 6, 5, 4 }));
            Assert.That(definition.GatewayCounts, Is.EqualTo(new[] { 1, 1, 1, 1, 1, 0 }));
            Assert.That(definition.GetBuildPointCount(0), Is.EqualTo(8));
            Assert.That(definition.GetGatewayCount(5), Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)definition.BuildPointCounts)[0] = 99);
        }

        [Test]
        public void BabelDefinition_RejectsMismatchedOrInvalidLayers()
        {
            Assert.Throws<ArgumentException>(() => new BabelDefinition(new[] { 8, 7 }, new[] { 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BabelDefinition(new[] { 8, 0 }, new[] { 1, 0 }));
            Assert.Throws<ArgumentException>(() => new BabelDefinition(new[] { 8, 7 }, new[] { 1, 1 }));
        }

        private static HumanDefinition MakeHuman(string id)
        {
            return new HumanDefinition(
                id, "Worker", 30f, 1f, 25, 1, 1,
                buildTimeSeconds: 2f, moveMode: "builder", senseRadius: 8f);
        }

        private static WaveDefinition MakeWave(string id, IEnumerable<PoolEntry> pool)
        {
            return new WaveDefinition(id, 0f, 10f, WaveSpawnMode.Timed, pool, 1, 3, 1f, "default");
        }

        private static SkillDefinition MakeSkill(
            string id,
            int level = 1,
            int maxLevel = 1,
            string upgradesFrom = "",
            float chance = 0f)
        {
            return new SkillDefinition(
                id,
                "Skill",
                "Description",
                id,
                "OnClick",
                1f,
                0f,
                0f,
                chance,
                new[] { new EffectDefinition("hit_single", damage: 10f) },
                level,
                maxLevel,
                1f,
                false,
                upgradesFrom);
        }
    }
}
