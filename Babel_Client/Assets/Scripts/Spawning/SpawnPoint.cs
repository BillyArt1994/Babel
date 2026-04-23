using UnityEngine;

namespace Babel
{
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("出生点 ID，CSV 中 spawnPointId 引用此值")]
        public string Id = "default";

        [Tooltip("出生点 X 轴随机散布宽度")]
        [Min(0f)]
        public float SpreadWidth = 1.0f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            var pos = transform.position;
            var halfWidth = SpreadWidth / 2f;
            Gizmos.DrawLine(pos + Vector3.left * halfWidth, pos + Vector3.right * halfWidth);
            Gizmos.DrawWireSphere(pos, 0.2f);
        }
    }
}
