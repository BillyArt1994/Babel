using System;
using System.Collections.Generic;

namespace Babel
{
    /// <summary>
    /// 技能装备状态的静态事件入口。
    /// </summary>
    public static class SkillEvents
    {
        /// <summary>
        /// 装备技能列表变化时触发。
        /// </summary>
        public static event Action<IReadOnlyList<SkillConfig>> OnEquippedSkillsChanged;

        /// <summary>
        /// 广播装备技能列表变化事件。
        /// </summary>
        /// <param name="skills">当前装备技能配置列表。</param>
        public static void RaiseEquippedSkillsChanged(IReadOnlyList<SkillConfig> skills)
        {
            if (OnEquippedSkillsChanged == null)
            {
                return;
            }

            foreach (Action<IReadOnlyList<SkillConfig>> handler in OnEquippedSkillsChanged.GetInvocationList())
            {
                if (IsDestroyedUnityTarget(handler.Target))
                {
                    OnEquippedSkillsChanged -= handler;
                    continue;
                }

                handler.Invoke(skills);
            }
        }

        private static bool IsDestroyedUnityTarget(object target)
        {
            return target is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
