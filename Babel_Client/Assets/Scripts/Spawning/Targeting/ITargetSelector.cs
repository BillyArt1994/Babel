using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 敌人选点策略：从候选建造点下标列表中挑一个。
    /// 只负责"挑选"，不负责筛选/占用（那些由 Path.ReserveBuildPoint 持有）。
    /// 返回 -1 表示无可选。
    /// </summary>
    public interface ITargetSelector
    {
        int Select(IReadOnlyList<int> candidateIndices, Path path, Vector3 fromPos);
    }
}
