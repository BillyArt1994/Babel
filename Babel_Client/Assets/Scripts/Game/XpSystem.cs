using System;
using Babel.Unity.Infrastructure.Content;
using UnityEngine;

namespace Babel
{
    public class XpSystem : MonoBehaviour
    {
        public static XpSystem Instance { get; private set; }

        [SerializeField] private TextAsset xpCsvAsset;

        private float[] _xpTable;
        private int _maxLevel;

        public int CurrentLevel { get; private set; } = 1;
        public float CurrentXp { get; private set; }
        public float XpForNextLevel { get; private set; }
        public float XpProgress => XpForNextLevel > 0f ? Mathf.Clamp01(CurrentXp / XpForNextLevel) : 0f;

        public event Action<int> OnLevelsGained;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OnLevelsGained = null;
        }

        private void Start()
        {
            if (xpCsvAsset == null && GameContentRegistry.Current != null)
                xpCsvAsset = GameContentRegistry.Current.ExperienceCsv;

            if (xpCsvAsset != null)
            {
                InitializeTable(ParseXpTable(xpCsvAsset.text));
                Debug.Log("[Babel][XpSystem] Loaded " + _xpTable.Length + " XP thresholds from manifest.");
                return;
            }

            var fallback = new float[20];
            for (int i = 0; i < fallback.Length; i++) fallback[i] = 5f;
            InitializeTable(fallback);
            Debug.LogWarning("[Babel][XpSystem] Manifest XP data unavailable; using deterministic test fallback.");
        }

        public static float[] ParseXpTable(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return Array.Empty<float>();

            var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new System.Collections.Generic.List<float>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                string[] columns = line.Split(',');
                if (columns.Length < 2) continue;
                if (float.TryParse(columns[1].Trim(), out float xp)) result.Add(xp);
            }
            return result.ToArray();
        }

        public void InitForTests(float[] xpPerLevel)
        {
            InitializeTable(xpPerLevel ?? Array.Empty<float>());
        }

        public void GainXp(float amount)
        {
            if (amount <= 0f || CurrentLevel >= _maxLevel) return;

            CurrentXp += amount;
            int levelsGained = 0;
            while (CurrentXp >= XpForNextLevel && CurrentLevel < _maxLevel)
            {
                CurrentXp -= XpForNextLevel;
                CurrentLevel++;
                levelsGained++;
                XpForNextLevel = XpForLevel(CurrentLevel);
            }

            if (levelsGained > 0) OnLevelsGained?.Invoke(levelsGained);
        }

        private void InitializeTable(float[] table)
        {
            _xpTable = table;
            _maxLevel = table.Length + 1;
            CurrentLevel = 1;
            CurrentXp = 0f;
            XpForNextLevel = XpForLevel(CurrentLevel);
        }

        private float XpForLevel(int level)
        {
            int index = level - 1;
            if (_xpTable == null || index >= _xpTable.Length) return float.MaxValue;
            return _xpTable[index];
        }
    }
}
