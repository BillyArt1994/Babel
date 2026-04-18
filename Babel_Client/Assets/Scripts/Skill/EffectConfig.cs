using System;

namespace Babel
{
    /// <summary>
    /// 单个效果槽位的配置数据，对应 CSV 中一组 Effect 列。
    /// </summary>
    public class EffectConfig
    {
        /// <summary>效果类型名（hit_single / hit_aoe / hit_chain / dot_aoe / stat_buff / spawn_projectile / apply_status / execute）</summary>
        public string EffectType = "";

        /// <summary>基础伤害值</summary>
        public float Damage;

        /// <summary>伤害比例系数（如 0.3 = 主伤害的 30%）</summary>
        public float DamageRatio;

        /// <summary>AOE 半径</summary>
        public float Radius;

        /// <summary>每秒伤害（仅 dot_aoe）</summary>
        public float Dps;

        /// <summary>持续时间（秒），-1 表示永久</summary>
        public float Duration;

        /// <summary>目标属性名（仅 stat_buff，如 cooldownMult / damageMult）</summary>
        public string StatName = "";

        /// <summary>属性修正量（正=增益，负=减益）</summary>
        public float StatValue;
    }
}
