using System;
using System.Collections.Generic;
using Babel.Gameplay.Content;
using Babel.Gameplay.World;
using UnityEngine;

namespace Babel.Unity.Infrastructure.Content
{
    [CreateAssetMenu(fileName = "CompiledGameContent", menuName = "Babel/Content/Compiled Game Content")]
    public sealed class CompiledGameContent : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string _sourceHash = string.Empty;
        [SerializeField] private HumanContentRecord[] _humans = Array.Empty<HumanContentRecord>();
        [SerializeField] private WaveContentRecord[] _waves = Array.Empty<WaveContentRecord>();
        [SerializeField] private SkillContentRecord[] _skills = Array.Empty<SkillContentRecord>();
        [SerializeField] private float[] _experienceThresholds = Array.Empty<float>();
        [SerializeField] private int[] _babelBuildPointCounts = { 8, 7, 6, 6, 5, 4 };
        [SerializeField] private int[] _babelGatewayCounts = { 1, 1, 1, 1, 1, 0 };

        public int SchemaVersion { get { return _schemaVersion; } }
        public string SourceHash { get { return _sourceHash; } }
        public int HumanCount { get { return _humans.Length; } }
        public int WaveCount { get { return _waves.Length; } }
        public int SkillCount { get { return _skills.Length; } }
        public int ExperienceThresholdCount { get { return _experienceThresholds.Length; } }
        public int BabelLayerCount { get { return _babelBuildPointCounts.Length; } }

        public HumanCatalog CreateHumanCatalog()
        {
            var definitions = new HumanDefinition[_humans.Length];
            for (int i = 0; i < _humans.Length; i++)
                definitions[i] = _humans[i].ToDefinition();
            return new HumanCatalog(definitions);
        }

        public WaveCatalog CreateWaveCatalog(HumanCatalog humans)
        {
            if (humans == null) throw new ArgumentNullException(nameof(humans));
            var definitions = new WaveDefinition[_waves.Length];
            for (int i = 0; i < _waves.Length; i++)
                definitions[i] = _waves[i].ToDefinition();
            var catalog = new WaveCatalog(definitions);
            catalog.Validate(humans);
            return catalog;
        }

        public SkillCatalog CreateSkillCatalog()
        {
            var definitions = new SkillDefinition[_skills.Length];
            for (int i = 0; i < _skills.Length; i++)
                definitions[i] = _skills[i].ToDefinition();
            var catalog = new SkillCatalog(definitions);
            catalog.Validate();
            return catalog;
        }

        public ExperienceTable CreateExperienceTable()
        {
            return new ExperienceTable((float[])_experienceThresholds.Clone());
        }

        public BabelDefinition CreateBabelDefinition()
        {
            return new BabelDefinition(
                (int[])_babelBuildPointCounts.Clone(),
                (int[])_babelGatewayCounts.Clone());
        }

        public GameRuntimeContent CreateGameRuntimeContent()
        {
            if (_schemaVersion != CurrentSchemaVersion)
                throw new InvalidOperationException(
                    "Compiled content schema " + _schemaVersion +
                    " does not match runtime schema " + CurrentSchemaVersion + ".");
            if (string.IsNullOrWhiteSpace(_sourceHash))
                throw new InvalidOperationException("Compiled content source hash is missing.");

            HumanCatalog humans = CreateHumanCatalog();
            WaveCatalog waves = CreateWaveCatalog(humans);
            SkillCatalog skills = CreateSkillCatalog();
            ExperienceTable experience = CreateExperienceTable();
            BabelDefinition babel = CreateBabelDefinition();
            return new GameRuntimeContent(humans, waves, experience, babel, skills);
        }

        public void ValidateRuntimeCatalogs()
        {
            CreateGameRuntimeContent();
        }

#if UNITY_EDITOR
        public void ReplaceForEditor(
            string sourceHash,
            HumanContentRecord[] humans,
            WaveContentRecord[] waves,
            SkillContentRecord[] skills,
            float[] experienceThresholds,
            int[] babelBuildPointCounts,
            int[] babelGatewayCounts)
        {
            if (string.IsNullOrWhiteSpace(sourceHash))
                throw new ArgumentException("A source hash is required.", nameof(sourceHash));

            _schemaVersion = CurrentSchemaVersion;
            _sourceHash = sourceHash;
            _humans = humans == null ? Array.Empty<HumanContentRecord>() : (HumanContentRecord[])humans.Clone();
            _waves = waves == null ? Array.Empty<WaveContentRecord>() : (WaveContentRecord[])waves.Clone();
            _skills = skills == null ? Array.Empty<SkillContentRecord>() : (SkillContentRecord[])skills.Clone();
            _experienceThresholds = experienceThresholds == null ? Array.Empty<float>() : (float[])experienceThresholds.Clone();
            _babelBuildPointCounts = babelBuildPointCounts == null ? Array.Empty<int>() : (int[])babelBuildPointCounts.Clone();
            _babelGatewayCounts = babelGatewayCounts == null ? Array.Empty<int>() : (int[])babelGatewayCounts.Clone();
            ValidateRuntimeCatalogs();
        }
#endif

        private void OnValidate()
        {
            if (_humans == null) _humans = Array.Empty<HumanContentRecord>();
            if (_waves == null) _waves = Array.Empty<WaveContentRecord>();
            if (_skills == null) _skills = Array.Empty<SkillContentRecord>();
            if (_experienceThresholds == null) _experienceThresholds = Array.Empty<float>();
            if (_babelBuildPointCounts == null) _babelBuildPointCounts = Array.Empty<int>();
            if (_babelGatewayCounts == null) _babelGatewayCounts = Array.Empty<int>();
        }
    }

    [Serializable]
    public sealed class HumanContentRecord
    {
        public string id;
        public string displayName;
        public float maxHealth;
        public float moveSpeed;
        public int buildContribution;
        public int buildCharges;
        public int experienceReward;
        public string abilityType;
        public float abilityRadius;
        public float abilityValue;
        public float abilityCooldownSeconds;
        public float buildTimeSeconds;
        public string moveMode;
        public float senseRadius;

        public HumanDefinition ToDefinition()
        {
            return new HumanDefinition(
                id,
                displayName,
                maxHealth,
                moveSpeed,
                buildContribution,
                buildCharges,
                experienceReward,
                abilityType,
                abilityRadius,
                abilityValue,
                abilityCooldownSeconds,
                buildTimeSeconds,
                moveMode,
                senseRadius);
        }
    }

    [Serializable]
    public sealed class WeightedHumanContentRecord
    {
        public string humanId;
        public float weight;

        public PoolEntry ToDefinition()
        {
            return new PoolEntry(humanId, weight);
        }
    }

    [Serializable]
    public sealed class WaveContentRecord
    {
        public string id;
        public float startSeconds;
        public float endSeconds;
        public WaveSpawnMode mode;
        public WeightedHumanContentRecord[] pool = Array.Empty<WeightedHumanContentRecord>();
        public int countMin;
        public int countMax;
        public float intervalSeconds;
        public string spawnPointId;

        public WaveDefinition ToDefinition()
        {
            var entries = new PoolEntry[pool.Length];
            for (int i = 0; i < pool.Length; i++)
                entries[i] = pool[i].ToDefinition();

            return new WaveDefinition(
                id,
                startSeconds,
                endSeconds,
                mode,
                entries,
                countMin,
                countMax,
                intervalSeconds,
                spawnPointId);
        }
    }

    [Serializable]
    public sealed class EffectContentRecord
    {
        public string effectType;
        public float damage;
        public float damageRatio;
        public float radius;
        public float damagePerSecond;
        public float durationSeconds;
        public string statName;
        public float statValue;

        public EffectDefinition ToDefinition()
        {
            return new EffectDefinition(
                effectType,
                damage,
                damageRatio,
                radius,
                damagePerSecond,
                durationSeconds,
                statName,
                statValue);
        }
    }

    [Serializable]
    public sealed class SkillContentRecord
    {
        public string id;
        public string displayName;
        public string description;
        public string iconId;
        public string triggerType;
        public float cooldownSeconds;
        public float chargeTimeSeconds;
        public float intervalSeconds;
        public float chance;
        public EffectContentRecord[] effects = Array.Empty<EffectContentRecord>();
        public int level;
        public int maxLevel;
        public float weight;
        public bool isStarterSkill;
        public string upgradesFrom;

        public SkillDefinition ToDefinition()
        {
            var effectDefinitions = new EffectDefinition[effects.Length];
            for (int i = 0; i < effects.Length; i++)
                effectDefinitions[i] = effects[i].ToDefinition();

            return new SkillDefinition(
                id,
                displayName,
                description,
                iconId,
                triggerType,
                cooldownSeconds,
                chargeTimeSeconds,
                intervalSeconds,
                chance,
                effectDefinitions,
                level,
                maxLevel,
                weight,
                isStarterSkill,
                upgradesFrom);
        }
    }
}
