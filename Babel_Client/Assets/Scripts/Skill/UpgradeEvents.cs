using System;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 升级抽卡流程的静态事件入口。
    /// </summary>
    public static class UpgradeEvents
    {
        /// <summary>
        /// 生成升级选项时触发。
        /// </summary>
        public static event Action<IReadOnlyList<SkillConfig>> OnOptionsGenerated;

        /// <summary>
        /// 玩家选择某个升级选项时触发。
        /// </summary>
        public static event Action<int> OnOptionSelected;

        /// <summary>
        /// 广播升级选项生成事件。
        /// </summary>
        /// <param name="options">本次可选择的技能列表。</param>
        public static void RaiseOptionsGenerated(IReadOnlyList<SkillConfig> options)
        {
            if (OnOptionsGenerated == null)
            {
                return;
            }

            foreach (Action<IReadOnlyList<SkillConfig>> handler in OnOptionsGenerated.GetInvocationList())
            {
                if (IsDestroyedUnityTarget(handler.Target))
                {
                    OnOptionsGenerated -= handler;
                    continue;
                }

                handler.Invoke(options);
            }
        }

        /// <summary>
        /// 广播升级选项选择事件。
        /// </summary>
        /// <param name="index">被选择的选项索引。</param>
        public static void RaiseOptionSelected(int index)
        {
            if (OnOptionSelected == null)
            {
                return;
            }

            foreach (Action<int> handler in OnOptionSelected.GetInvocationList())
            {
                if (IsDestroyedUnityTarget(handler.Target))
                {
                    OnOptionSelected -= handler;
                    continue;
                }

                handler.Invoke(index);
            }
        }

        private static bool IsDestroyedUnityTarget(object target)
        {
            return target is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
