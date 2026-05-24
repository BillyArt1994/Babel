using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 记录本局结算所需的轻量统计数据。
    /// </summary>
    public static class StatsTracker
    {
        /// <summary>
        /// 本局已确认击杀数。
        /// </summary>
        public static int KillCount { get; private set; }

        /// <summary>
        /// 记录一次敌人击杀。
        /// </summary>
        public static void RecordKill()
        {
            KillCount++;
        }

        /// <summary>
        /// 重置本局统计。
        /// </summary>
        public static void Reset()
        {
            KillCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Reset();
        }
    }
}
