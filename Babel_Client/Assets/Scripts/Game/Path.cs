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
            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].IsBuildCompleted) continue;
                if (wayPointList[i].IsOccupied) continue;
                float dist = Vector3.Distance(wayPointList[i].transform.position, fromPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                wayPointList[bestIndex].SetOccupied(true);
            }
            return bestIndex;
        }

        public void ReleaseBuildPoint(int index)
        {
            if (index >= 0 && index < wayPointList.Length)
            {
                wayPointList[index].SetOccupied(false);
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
