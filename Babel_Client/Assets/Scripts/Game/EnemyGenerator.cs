using UnityEngine;
using QFramework;
using System;
using System.Collections.Generic;

namespace Babel
{
    [Serializable]
    public class EnemyWave
    {
        public float generateDuration  =1;
        public GameObject enemyPrefab;
        public int currentWaveSeconds = 10;
    }

    public partial class EnemyGenerator : ViewController
    {
        private float currentGenerateSecontds = 0f;
        private float currentWaveSeconds = 0f;
        [SerializeField]
        public List<EnemyWave> enemyWaves = new List<EnemyWave>();
        private Queue<EnemyWave> enemyWaveQueue = new Queue<EnemyWave>();

        private void Start()
        {
            foreach (var enemyWave in enemyWaves)
            {
                enemyWaveQueue.Enqueue(enemyWave);
            }
        }

        private EnemyWave currentWave = null;

        void Update()
        {
            if (currentWave == null)
            {
                if (enemyWaveQueue.Count > 0)
                {
                    currentWave = enemyWaveQueue.Dequeue();
                    currentGenerateSecontds = 0;
                    currentWaveSeconds = 0;
                }
            }

            if (currentWave != null)
            {
                currentGenerateSecontds += Time.deltaTime;
                currentWaveSeconds += Time.deltaTime;

            }

            //currentSecontds += Time.deltaTime;
            //if (currentSecontds > 1f)
            //{
            //    currentSecontds = 0;
            //    Enemy.Instantiate()
            //    .Position(transform.position)
            //    .Show();
            //}
        }
    }
}
