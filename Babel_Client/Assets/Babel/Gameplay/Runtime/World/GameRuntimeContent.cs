using System;
using Babel.Gameplay.Content;

namespace Babel.Gameplay.World
{
    /// <summary>Validated, immutable content bundle consumed by one simulation world.</summary>
    public sealed class GameRuntimeContent
    {
        public const int DefaultBuildPointRequiredProgress = 50;

        public GameRuntimeContent(
            HumanCatalog humans,
            WaveCatalog waves,
            ExperienceTable experience,
            BabelDefinition babel,
            SkillCatalog skills = null,
            int buildPointRequiredProgress = DefaultBuildPointRequiredProgress)
        {
            Humans = humans ?? throw new ArgumentNullException(nameof(humans));
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            Experience = experience ?? throw new ArgumentNullException(nameof(experience));
            Babel = babel ?? throw new ArgumentNullException(nameof(babel));
            Skills = skills ?? new SkillCatalog(Array.Empty<SkillDefinition>());
            if (buildPointRequiredProgress <= 0)
                throw new ArgumentOutOfRangeException(nameof(buildPointRequiredProgress));

            Waves.Validate(Humans);
            Skills.Validate();
            BuildPointRequiredProgress = buildPointRequiredProgress;
        }

        public HumanCatalog Humans { get; }
        public WaveCatalog Waves { get; }
        public SkillCatalog Skills { get; }
        public ExperienceTable Experience { get; }
        public BabelDefinition Babel { get; }
        public int BuildPointRequiredProgress { get; }
    }
}
