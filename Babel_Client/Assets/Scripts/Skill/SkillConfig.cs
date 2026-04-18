using System.Collections.Generic;

namespace Babel
{
    /// <summary>
    /// 技能配置数据，对应 CSV 中一行记录。纯数据容器，不包含逻辑。
    /// </summary>
    public class SkillConfig
    {
        // ── 元数据 ──
        /// <summary>唯一标识符（如 "divine_finger"）</summary>
        public string SkillId = "";

        /// <summary>显示名称（如"神罚之指"）</summary>
        public string SkillName = "";

        /// <summary>升级界面描述文字</summary>
        public string Description = "";

        /// <summary>图标资源路径</summary>
        public string IconPath = "";

        // ── 触发器 ──
        /// <summary>触发器类型名（OnClick / OnHit / OnTimer / OnKill）</summary>
        public string TriggerType = "";

        /// <summary>触发冷却时间（秒）</summary>
        public float Cooldown;

        /// <summary>蓄力时间（秒），> 0 时启用蓄力机制</summary>
        public float ChargeTime;

        /// <summary>自动触发间隔（秒），仅 OnTimer 使用</summary>
        public float Interval;

        /// <summary>触发概率 [0,1]，仅 OnHit / OnKill 使用</summary>
        public float Chance;

        // ── 效果列表 ──
        /// <summary>效果配置列表（1~3 个），按 CSV 中 effect/effect2/effect3 顺序</summary>
        public readonly List<EffectConfig> Effects = new List<EffectConfig>();

        // ── 升级系统 ──
        /// <summary>技能等级</summary>
        public int Level = 1;

        /// <summary>升级权重（越高越频繁出现）</summary>
        public float Weight = 1.0f;

        /// <summary>是否为起始可选技能</summary>
        public bool IsStarterSkill;

        /// <summary>进化来源技能 ID（空 = 初始技能）</summary>
        public string UpgradesFrom = "";
    }
}
