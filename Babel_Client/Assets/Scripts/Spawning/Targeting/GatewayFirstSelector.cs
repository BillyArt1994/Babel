using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 斥候策略：候选里若含 gateway（未建未占已由 Path 预筛）则强制选它；
    /// 否则委托默认策略（全候选随机），即退化为普通工人。
    /// </summary>
    public class GatewayFirstSelector : ITargetSelector
    {
        /// <summary>无状态单例，避免每次 Init 重复分配。</summary>
        public static readonly GatewayFirstSelector Instance = new GatewayFirstSelector();

        /// <summary>退化策略复用单例，满足 DIP（依赖接口而非具体类）。</summary>
        private readonly ITargetSelector _fallback = DefaultBuildSelector.Instance;

        public int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos)
        {
            if (candidateIndices == null || candidateIndices.Count == 0) return -1;
            if (path != null && path.wayPointList != null)
            {
                for (int i = 0; i < candidateIndices.Count; i++)
                {
                    int idx = candidateIndices[i];
                    if (idx >= 0 && idx < path.wayPointList.Length)
                    {
                        var bp = path.wayPointList[idx];
                        if (bp != null && bp.isGateway) return idx;
                    }
                }
            }
            return _fallback.Select(candidateIndices, path, fromPos);
        }
    }
}
