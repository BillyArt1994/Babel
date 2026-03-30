using System.Collections.Generic;
using UnityEngine;

namespace Babel.Map
{
    /// <summary>
    /// 单个槽位数据。
    /// </summary>
    [System.Serializable]
    public class SlotData
    {
        public Vector2 position;        // 世界坐标
        public bool isPassage;          // true = 通道槽，false = 普通槽
        public int requiredCount = 1;   // 完成所需敌人数（预留扩展）
    }

    /// <summary>
    /// 每层数据。
    /// </summary>
    [System.Serializable]
    public class LayerData
    {
        public int layerIndex;                              // 层编号 0-9
        public List<SlotData> slots = new List<SlotData>(); // 该层所有槽位（普通+通道混合）
    }

    /// <summary>
    /// 整张地图配置 ScriptableObject。
    /// 保存通天塔所有层的槽位布局信息。
    /// </summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "Babel/Map Config")]
    public class MapConfig : ScriptableObject
    {
        public const int MAX_LAYERS = 10;

        public List<LayerData> layers = new List<LayerData>(); // 10 层数据

        /// <summary>
        /// 查询某层的所有普通槽。
        /// </summary>
        public List<SlotData> GetNormalSlots(int layerIndex)
        {
            LayerData layer = GetLayer(layerIndex);
            if (layer == null) return new List<SlotData>();

            var result = new List<SlotData>();
            foreach (var slot in layer.slots)
            {
                if (!slot.isPassage)
                    result.Add(slot);
            }
            return result;
        }

        /// <summary>
        /// 查询某层的所有通道槽。
        /// </summary>
        public List<SlotData> GetPassageSlots(int layerIndex)
        {
            LayerData layer = GetLayer(layerIndex);
            if (layer == null) return new List<SlotData>();

            var result = new List<SlotData>();
            foreach (var slot in layer.slots)
            {
                if (slot.isPassage)
                    result.Add(slot);
            }
            return result;
        }

        /// <summary>
        /// 查询某层某位置最近的可用普通槽。
        /// </summary>
        public SlotData GetNearestNormalSlot(int layerIndex, Vector2 fromPos)
        {
            return FindNearest(GetNormalSlots(layerIndex), fromPos);
        }

        /// <summary>
        /// 查询某层某位置最近的可用通道槽。
        /// </summary>
        public SlotData GetNearestPassageSlot(int layerIndex, Vector2 fromPos)
        {
            return FindNearest(GetPassageSlots(layerIndex), fromPos);
        }

        /// <summary>
        /// 按 layerIndex 查找层数据。
        /// </summary>
        public LayerData GetLayer(int layerIndex)
        {
            if (layers == null) return null;
            foreach (var layer in layers)
            {
                if (layer.layerIndex == layerIndex)
                    return layer;
            }
            return null;
        }

        private static SlotData FindNearest(List<SlotData> slots, Vector2 fromPos)
        {
            if (slots == null || slots.Count == 0) return null;

            SlotData nearest = null;
            float minDist = float.MaxValue;
            foreach (var slot in slots)
            {
                float dist = Vector2.SqrMagnitude(slot.position - fromPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = slot;
                }
            }
            return nearest;
        }
    }
}
