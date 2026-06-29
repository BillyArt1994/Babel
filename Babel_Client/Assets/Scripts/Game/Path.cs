using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Babel
{
    public class Path : MonoBehaviour
    {
        public BuildPoint[] wayPointList;
        public Babel.Path nextLayerPath;

        [HideInInspector] public int LayerIndex;

        private int _completedCount;
        private static readonly ITargetSelector DefaultSelector = DefaultBuildSelector.Instance;
        private readonly List<int> _candidateIndices = new List<int>(16);

        public bool IsCompleted => wayPointList != null && _completedCount >= wayPointList.Length;

        public void OnBuildPointCompleted()
        {
            _completedCount++;
            if (IsCompleted)
            {
                BuildEvents.RaiseLayerCompleted(this);
                if (nextLayerPath == null)
                {
                    GameSession.EndGame(GameEndReason.Defeat);
                }
            }
        }

        public int GetGatewayIndex()
        {
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].isGateway)
                    return i;
            }
            return 0;
        }

        /// <summary>本层 gateway 是否已建完（可作为公共梯子）。</summary>
        public bool IsGatewayBuilt()
        {
            if (wayPointList == null) return false;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                var bp = wayPointList[i];
                if (bp != null && bp.isGateway)
                    return bp.IsBuildCompleted;
            }
            return false;
        }

        public int ReserveBuildPoint(Vector3 fromPos)
        {
            return ReserveBuildPoint(fromPos, DefaultSelector);
        }

        public int ReserveBuildPoint(Vector3 fromPos, ITargetSelector selector)
        {
            _candidateIndices.Clear();
            if (wayPointList == null) return -1;

            for (int i = 0; i < wayPointList.Length; i++)
            {
                BuildPoint point = wayPointList[i];
                if (point == null) continue;
                if (point.IsBuildCompleted) continue;
                // 不再排除 IsOccupied：多建者可同时选同一点
                _candidateIndices.Add(i);
            }

            if (_candidateIndices.Count == 0) return -1;

            ITargetSelector chooser = selector ?? DefaultSelector;
            int selectedBuildPointIndex = chooser.Select(_candidateIndices, this, fromPos);
            if (selectedBuildPointIndex < 0 || selectedBuildPointIndex >= wayPointList.Length)
                return -1;

            // 不再调 SetOccupied：占用语义由 BuilderMovement 的 AttachBuilder 承担
            return selectedBuildPointIndex;
        }

        public void ReleaseBuildPoint(int index)
        {
            // 预约不再独占（多建者并发），故无需释放占用状态。
            // 方法保留：BuilderMovement 仍调用它；占用语义已由 AttachBuilder/DetachBuilder 承担。
        }

        private void OnDrawGizmos()
        {
            if (wayPointList == null || wayPointList.Length == 0) return;

            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i] == null) continue;

                if (wayPointList[i].isGateway)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(wayPointList[i].transform.position, 0.4f);
                }
                else
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(wayPointList[i].transform.position, 0.2f);
                }

                if (i < wayPointList.Length - 1 && wayPointList[i + 1] != null)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
                }
            }

            if (nextLayerPath != null && nextLayerPath.wayPointList != null && nextLayerPath.wayPointList.Length > 0)
            {
                int gwIdx = GetGatewayIndex();
                if (gwIdx >= 0 && gwIdx < wayPointList.Length && wayPointList[gwIdx] != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(
                        wayPointList[gwIdx].transform.position,
                        nextLayerPath.wayPointList[0].transform.position
                    );
                }
            }

#if UNITY_EDITOR
            var style = new GUIStyle();
            style.normal.textColor = Color.cyan;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;
            style.alignment = TextAnchor.MiddleCenter;
            Handles.Label(transform.position + Vector3.up * 1.0f, $"Layer {LayerIndex}", style);
#endif
        }
    }
}
