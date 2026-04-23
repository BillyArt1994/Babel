using UnityEngine;

namespace Babel
{
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("此出生点所属的方向（Left/Right），CSV 中 spawnSide 引用")]
        public SpawnSide Side;

        [Tooltip("出生点附近的随机散布半径")]
        [Min(0f)]
        public float SpreadRadius = 0.5f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Side == SpawnSide.Left ? Color.blue : Color.green;
            Gizmos.DrawWireSphere(transform.position, SpreadRadius);
            Gizmos.DrawIcon(transform.position, "d_Animation.Play", true);
        }
    }
}
