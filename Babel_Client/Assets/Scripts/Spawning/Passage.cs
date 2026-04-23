using UnityEngine;

namespace Babel
{
    public class Passage : MonoBehaviour
    {
        [Tooltip("从哪层（编号，从 1 开始）")]
        public int FromLayer;

        [Tooltip("到哪层")]
        public int ToLayer;

        [Tooltip("到达上层后的出口位置")]
        public Transform ExitPoint;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
            if (ExitPoint != null)
            {
                Gizmos.DrawLine(transform.position, ExitPoint.position);
                Gizmos.DrawWireSphere(ExitPoint.position, 0.2f);
            }
        }
    }
}
