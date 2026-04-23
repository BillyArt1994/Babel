using System.Collections.Generic;

namespace Babel
{
    public enum SpawnMode { Burst, Maintain, Timed }

    public struct PoolEntry
    {
        public string EnemyId;
        public float Weight;

        public PoolEntry(string enemyId, float weight)
        {
            EnemyId = enemyId;
            Weight = weight;
        }
    }

    public class WaveEvent
    {
        public float StartTime;
        public float EndTime;
        public SpawnMode Mode;
        public List<PoolEntry> EnemyPool = new();
        public int CountMin;
        public int CountMax;
        public float Interval;
        public string SpawnPointId = "default";
    }
}
