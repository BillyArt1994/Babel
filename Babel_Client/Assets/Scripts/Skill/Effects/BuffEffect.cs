namespace Babel
{
    public class BuffEffect : IEffect
    {
        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;

        public BuffEffect(EffectConfig config, EffectManager effectManager)
        {
            _config = config;
            _effectManager = effectManager;
        }

        public void Execute(TriggerContext context)
        {
            _effectManager.AddBuff(_config.StatName, _config.StatValue, _config.Duration);
        }
    }
}
