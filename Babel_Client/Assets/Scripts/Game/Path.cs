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
        private readonly List<BuildPointCandidate> _reserveCandidates = new List<BuildPointCandidate>(16);
        private static readonly BuildPointCandidateDistanceComparer CandidateDistanceComparer =
            new BuildPointCandidateDistanceComparer();

        public bool IsCompleted => _completedCount >= wayPointList.Length;

        public void OnBuildPointCompleted()
        {
            _completedCount++;
            if (IsCompleted)
            {
                BuildEvents.RaiseLayerCompleted(this);
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

        public int ReserveBuildPoint(Vector3 fromPos)
        {
            _reserveCandidates.Clear();
            if (wayPointList == null)
            {
                return -1;
            }

            for (int i = 0; i < wayPointList.Length; i++)
            {
                BuildPoint point = wayPointList[i];
                if (point == null) continue;
                if (point.IsBuildCompleted) continue;
                if (point.IsOccupied) continue;

                float distance = Vector3.Distance(point.transform.position, fromPos);
                _reserveCandidates.Add(new BuildPointCandidate(i, distance));
            }

            if (_reserveCandidates.Count <= 0)
            {
                return -1;
            }

            _reserveCandidates.Sort(CandidateDistanceComparer);
            int selectableCount = GetFarthestSelectionCount(_reserveCandidates.Count);
            int selectedCandidateIndex = UnityEngine.Random.Range(0, selectableCount);
            int selectedBuildPointIndex = _reserveCandidates[selectedCandidateIndex].Index;
            wayPointList[selectedBuildPointIndex].SetOccupied(true);
            return selectedBuildPointIndex;
        }

        public void ReleaseBuildPoint(int index)
        {
            if (index >= 0 && index < wayPointList.Length)
            {
                wayPointList[index].SetOccupied(false);
            }
        }

        private static int GetFarthestSelectionCount(int candidateCount)
        {
            if (candidateCount <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, candidateCount / 2);
        }

        private readonly struct BuildPointCandidate
        {
            public BuildPointCandidate(int index, float distance)
            {
                Index = index;
                Distance = distance;
            }

            public readonly int Index;
            public readonly float Distance;
        }

        private sealed class BuildPointCandidateDistanceComparer : IComparer<BuildPointCandidate>
        {
            public int Compare(BuildPointCandidate left, BuildPointCandidate right)
            {
                return right.Distance.CompareTo(left.Distance);
            }
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
