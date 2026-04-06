using System.Collections.Generic;
using UnityEngine;


namespace Babel
{
    public class Path : MonoBehaviour
    {
        public BuildPoint[] wayPointList;
        public Babel.Path nextLayerPath;
        // Start is called before the first frame update

        private void OnDrawGizmos()
        {
            if (wayPointList.Length > 0)
            {
                for (int i = 0; i < wayPointList.Length; i++)
                {
                    if (i < wayPointList.Length - 1)
                    {
                        Gizmos.color = Color.gray;
                        Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
                    }
                }
            }
        }

        public bool IsCurrentLayerBuildCompleted()
        {
            bool isCurrentLayerBuildCompleted = true;
            for (int i = 0; i < wayPointList.Length; i++)
            {
                if (wayPointList[i].IsBuildCompleted == false)
                {
                    isCurrentLayerBuildCompleted = false;
                    break;
                }
            }
            return isCurrentLayerBuildCompleted;
        }

        public int getGatewayIndex()
        {
            var index = 0;
            for (int i = 0;i< wayPointList.Length; i++)
            {
                if (wayPointList[i].isGateway == true)
                {
                    index = i; break;
                }
            }

            return index;
        }
    }
}

