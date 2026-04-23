using UnityEngine;

namespace Babel
{
    public class Path : MonoBehaviour
    {
        public BuildPoint[] wayPointList;
        public Babel.Path nextLayerPath;

        private int _completedCount;

        public bool IsCompleted => _completedCount >= wayPointList.Length;

        public void OnBuildPointCompleted()
        {
            _completedCount++;
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

        public int FindNearestEmptyBuildPoint(Vector3 fromPosition)
        {
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].IsBuildCompleted) continue;
                if (wayPointList[i].IsBilding) continue;
                float dist = Vector3.Distance(wayPointList[i].transform.position, fromPosition);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private void OnDrawGizmos()
        {
            if (wayPointList == null || wayPointList.Length == 0) return;
            for (int i = 0; i < wayPointList.Length - 1; i++)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
            }
        }
    }
}
