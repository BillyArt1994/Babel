using UnityEngine;
using QFramework;

namespace Babel
{
    /// <summary>
    /// 塔层统一管理器。Awake 时自动建立层链表、设置 BuildPoint.OwnerPath。
    /// 提供全局查询：起始层、完成度、Game Over 判定。
    /// </summary>
    public class TowerManager : MonoBehaviour
    {
        [Tooltip("按从底到顶的顺序排列所有层的 Path")]
        [SerializeField] private Path[] layers;

        /// <summary>最底层 Path（敌人出生后的起始目标）。</summary>
        public Path StartPath => layers != null && layers.Length > 0 ? layers[0] : null;

        /// <summary>总层数。</summary>
        public int LayerCount => layers != null ? layers.Length : 0;

        /// <summary>
        /// 塔的整体建造完成度 [0, 1]。
        /// </summary>
        public float CompletionPercent
        {
            get
            {
                if (layers == null || layers.Length == 0) return 0f;
                int totalSlots = 0;
                int completedSlots = 0;
                for (int i = 0; i < layers.Length; i++)
                {
                    var wpl = layers[i].wayPointList;
                    totalSlots += wpl.Length;
                    for (int j = 0; j < wpl.Length; j++)
                    {
                        if (wpl[j].IsBuildCompleted)
                            completedSlots++;
                    }
                }
                return totalSlots > 0 ? (float)completedSlots / totalSlots : 0f;
            }
        }

        /// <summary>
        /// 最顶层是否已建完（Game Over 条件）。
        /// </summary>
        public bool IsGameOver
        {
            get
            {
                if (layers == null || layers.Length == 0) return false;
                return layers[layers.Length - 1].IsCompleted;
            }
        }

        private void Awake()
        {
            if (layers == null || layers.Length == 0)
            {
                Debug.LogWarning("[BABEL][TowerManager] No layers assigned");
                return;
            }

            // Auto-build linked list
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].LayerIndex = i + 1;
                layers[i].nextLayerPath = (i + 1 < layers.Length) ? layers[i + 1] : null;

                // Auto-set BuildPoint.OwnerPath
                var wpl = layers[i].wayPointList;
                if (wpl == null) continue;
                for (int j = 0; j < wpl.Length; j++)
                {
                    if (wpl[j] != null)
                        wpl[j].OwnerPath = layers[i];
                }
            }

            Debug.Log($"[BABEL][TowerManager] Initialized {layers.Length} layers, auto-linked Path chain and OwnerPath references");
        }

        /// <summary>
        /// 获取指定索引的层（0 = 最底层）。
        /// </summary>
        public Path GetLayer(int index)
        {
            if (layers == null || index < 0 || index >= layers.Length) return null;
            return layers[index];
        }
    }
}
