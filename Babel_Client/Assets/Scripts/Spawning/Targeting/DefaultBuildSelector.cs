using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>工人默认策略：从全部候选中等概率随机一个。</summary>
    public class DefaultBuildSelector : ITargetSelector
    {
        /// <summary>无状态单例，避免每次 Init 重复分配。</summary>
        public static readonly DefaultBuildSelector Instance = new DefaultBuildSelector();

        public int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos)
        {
            if (candidateIndices == null || candidateIndices.Count == 0) return -1;
            int pick = Random.Range(0, candidateIndices.Count);
            return candidateIndices[pick];
        }
    }
}
