using System.Collections.Generic;

namespace Babel
{
    public enum SpawnMode { Burst, Maintain, Timed }
    public enum SpawnSide { Left, Right, Both, Random }

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
        public float EndTime;           // 0 = no end (until game ends)
        public SpawnMode Mode;
        public List<PoolEntry> EnemyPool = new();
        public int CountMin;
        public int CountMax;
        public float Interval;
        public SpawnSide Side;
    }
}
