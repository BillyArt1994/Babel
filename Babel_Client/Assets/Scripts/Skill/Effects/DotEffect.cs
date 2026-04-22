namespace Babel
{
    public class DotEffect : IEffect
    {
        private readonly EffectConfig _config;
        private readonly EffectManager _effectManager;

        public DotEffect(EffectConfig config, EffectManager effectManager)
        {
            _config = config;
            _effectManager = effectManager;
        }

        public void Execute(TriggerContext context)
        {
            _effectManager.AddDot(context.WorldPos, _config.Radius, _config.Dps, _config.Duration);
        }
    }
}
