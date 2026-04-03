using System.Collections.Generic;
using UnityEngine;


namespace Babel
{
    public class Path : MonoBehaviour
    {

        public BuildPoint[] wayPointList ;
        // Start is called before the first frame update

        private void OnDrawGizmos()
        {
            if ( wayPointList.Length >0)
            {
                for (int i = 0; i< wayPointList.Length; i++)
                {
                    if( i < wayPointList.Length -1)
                    {
                        Gizmos.color =Color.gray;
                        Gizmos.DrawLine(wayPointList[i].transform.position, wayPointList[i + 1].transform.position);
                    }
                }
            }
        }
    }
}

