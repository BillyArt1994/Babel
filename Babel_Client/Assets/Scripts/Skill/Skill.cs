namespace Babel
{
    public class Skill
    {
        public SkillConfig Config { get; }
        public TriggerBase Trigger { get; }
        public IEffect Effect { get; }

        public Skill(SkillConfig config, TriggerBase trigger, IEffect effect)
        {
            Config = config;
            Trigger = trigger;
            Effect = effect;
        }
    }
}
