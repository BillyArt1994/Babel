using System;
using QFramework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Babel
{
    /// <summary>
    /// 管理玩家经验值与等级成长曲线。
    /// 每级所需 XP 由外部注入（InitForTests）或从 experience.csv 加载。
    /// </summary>
    public class XpSystem : ViewController
    {
        public static XpSystem Instance { get; private set; }

        [SerializeField] private TextAsset xpCsvAsset;

        private float[] _xpTable;   // index 0 = 升到 2 级所需, index 1 = 升到 3 级所需, ...
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
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
#if UNITY_EDITOR
            // Editor 自动解析：若 Inspector 未赋值则尝试自动加载
            if (xpCsvAsset == null)
            {
                xpCsvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/experience.csv");
            }
#endif
            if (xpCsvAsset != null)
            {
                float[] table = ParseXpTable(xpCsvAsset.text);
                _xpTable = table;
                _maxLevel = table.Length + 1;
                CurrentLevel = 1;
                CurrentXp = 0f;
                XpForNextLevel = XpForLevel(CurrentLevel);
                Debug.LogWarning($"[BABEL][XpSystem] CSV 加载完成，共 {table.Length} 级曲线，maxLevel={_maxLevel}");
            }
            else
            {
                // fallback：固定 5 XP/级，20 级
                var fallback = new float[20];
                for (int i = 0; i < fallback.Length; i++) fallback[i] = 5f;
                _xpTable = fallback;
                _maxLevel = fallback.Length + 1;
                CurrentLevel = 1;
                CurrentXp = 0f;
                XpForNextLevel = XpForLevel(CurrentLevel);
                Debug.LogWarning("[BABEL][XpSystem] 未找到 experience.csv，使用固定 5 XP/级 fallback");
            }
        }

        /// <summary>解析 CSV 文本，返回每级所需 XP 数组（index 0 = 升到 2 级所需）。</summary>
        public static float[] ParseXpTable(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return Array.Empty<float>();

            var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            // 跳过表头行
            var result = new System.Collections.Generic.List<float>();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var cols = line.Split(',');
                if (cols.Length < 2) continue;
                if (float.TryParse(cols[1].Trim(), out float xp))
                {
                    result.Add(xp);
                }
            }
            return result.ToArray();
        }

        /// <summary>测试用：直接注入 XP 曲线，跳过 CSV 加载。</summary>
        public void InitForTests(float[] xpPerLevel)
        {
            _xpTable = xpPerLevel;
            _maxLevel = xpPerLevel.Length + 1;
            CurrentLevel = 1;
            CurrentXp = 0f;
            XpForNextLevel = XpForLevel(CurrentLevel);
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

            if (levelsGained > 0)
                OnLevelsGained?.Invoke(levelsGained);
        }

        private float XpForLevel(int level)
        {
            int idx = level - 1; // level 1 → index 0
            if (_xpTable == null || idx >= _xpTable.Length) return float.MaxValue;
            return _xpTable[idx];
        }
    }
}
