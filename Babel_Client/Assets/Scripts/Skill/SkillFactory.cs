using System;
using System.Linq;

namespace Babel
{
    public static class SkillFactory
    {
        public static Skill Create(SkillConfig config, EffectManager effectManager, Func<UnityEngine.Vector2> getBasePosition)
        {
            var trigger = CreateTrigger(config, getBasePosition);
            var effect = CreateEffect(config, effectManager);
            trigger.Bind(effect.Execute);
            return new Skill(config, trigger, effect);
        }

        private static TriggerBase CreateTrigger(SkillConfig config, Func<UnityEngine.Vector2> getBasePosition)
        {
            return config.TriggerType switch
            {
                "OnClick" => new OnClickTrigger(config.Cooldown, config.ChargeTime),
                "OnHit" => new OnHitTrigger(config.Chance),
                "OnTimer" => CreateTimerTrigger(config, getBasePosition),
                "OnKill" => new OnKillTrigger(config.Chance),
                _ => throw new ArgumentException($"[BABEL][SkillFactory] Unknown trigger type: '{config.TriggerType}' in skill '{config.SkillId}'")
            };
        }

        private static OnTimerTrigger CreateTimerTrigger(SkillConfig config, Func<UnityEngine.Vector2> getBasePosition)
        {
            var trigger = new OnTimerTrigger(config.Interval);
            trigger.GetBasePosition = getBasePosition;
            return trigger;
        }

        private static IEffect CreateEffect(SkillConfig config, EffectManager effectManager)
        {
            if (config.Effects.Count == 0)
            {
                throw new ArgumentException($"[BABEL][SkillFactory] Skill '{config.SkillId}' has no effects");
            }

            if (config.Effects.Count == 1)
            {
                return CreateSingleEffect(config.Effects[0], effectManager);
            }

            var effects = config.Effects.Select(ec => CreateSingleEffect(ec, effectManager)).ToArray();
            return new CompositeEffect(effects);
        }

        private static IEffect CreateSingleEffect(EffectConfig ec, EffectManager effectManager)
        {
            return ec.EffectType switch
            {
                "hit_single" => new DamageEffect(ec, effectManager, isAoe: false),
                "hit_aoe" => new DamageEffect(ec, effectManager, isAoe: true),
                "dot_aoe" => new DotEffect(ec, effectManager),
                "stat_buff" => new BuffEffect(ec, effectManager),
                _ => throw new ArgumentException($"[BABEL][SkillFactory] Unknown effect type: '{ec.EffectType}'")
            };
        }
    }
}
