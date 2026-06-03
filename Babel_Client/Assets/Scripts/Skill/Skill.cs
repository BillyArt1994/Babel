namespace Babel
{
    public class Skill
    {
        public SkillConfig Config { get; private set; }
        public TriggerBase Trigger { get; }
        public IEffect Effect { get; }

        public Skill(SkillConfig config, TriggerBase trigger, IEffect effect)
        {
            Config = config;
            Trigger = trigger;
            Effect = effect;
        }

        /// <summary>就地替换 Config，Trigger 和 Effect 对象保持不变（冷却状态不丢失）。</summary>
        public void UpdateConfig(SkillConfig newConfig)
        {
            Config = newConfig;
        }
    }
}
