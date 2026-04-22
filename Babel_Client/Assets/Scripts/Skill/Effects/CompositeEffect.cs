namespace Babel
{
    public class CompositeEffect : IEffect
    {
        private readonly IEffect[] _effects;

        public CompositeEffect(IEffect[] effects)
        {
            _effects = effects;
        }

        public void Execute(TriggerContext context)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i].Execute(context);
            }
        }
    }
}
