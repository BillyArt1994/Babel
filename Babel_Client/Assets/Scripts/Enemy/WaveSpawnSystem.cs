using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSV 驱动的波次生成系统，替代 EnemySpawnSystem。
/// 从 Resources/Data/level_waves.csv 加载波次配置，按时间轴依次激活各波次。
/// </summary>
public class WaveSpawnSystem : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // 内部类型
    // -------------------------------------------------------------------------

    private enum SpawnSide { Left, Right, Alternate }

    private class WaveEntry
    {
        public int WaveId;
        public float StartTime;
        public float EndTime;
        public EnemyType EnemyType;
        public int TotalCount;
        public float Interval;
        public SpawnSide Side;

        // 运行时状态
        public int SpawnedCount;
        public float Timer;
        public bool IsActive;
    }

    // -------------------------------------------------------------------------
    // 单例
    // -------------------------------------------------------------------------

    public static WaveSpawnSystem Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector 字段（与 EnemySpawnSystem 完全相同，方便替换）
    // -------------------------------------------------------------------------

    [SerializeField] private EnemyDatabase _enemyDatabase;
    [SerializeField] private TowerConstructionSystem _towerSystem;
    [SerializeField] private Transform _leftSpawnPoint;
    [SerializeField] private Transform _rightSpawnPoint;

    // -------------------------------------------------------------------------
    // 私有字段
    // -------------------------------------------------------------------------

    private bool _isSpawning;
    private bool _spawnFromLeft = true;
    private readonly List<WaveEntry> _waves = new List<WaveEntry>(32);

    // -------------------------------------------------------------------------
    // Unity 生命周期
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnGameStart += StartSpawning;
        GameEvents.OnGamePaused += HandleGamePaused;
        GameEvents.OnGameResumed += HandleGameResumed;
        GameEvents.OnVictory += StopSpawning;
        GameEvents.OnDefeat += StopSpawning;
        EnemyEvents.OnEnemyReachedTower += OnEnemyReachedTower;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart -= StartSpawning;
        GameEvents.OnGamePaused -= HandleGamePaused;
        GameEvents.OnGameResumed -= HandleGameResumed;
        GameEvents.OnVictory -= StopSpawning;
        GameEvents.OnDefeat -= StopSpawning;
        EnemyEvents.OnEnemyReachedTower -= OnEnemyReachedTower;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!_isSpawning)
            return;

        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying())
            return;

        float elapsed = GameLoopManager.Instance.GetElapsedTime();

        for (int i = 0; i < _waves.Count; i++)
        {
            WaveEntry entry = _waves[i];

            // 激活判定
            if (!entry.IsActive
                && elapsed >= entry.StartTime
                && elapsed < entry.EndTime
                && entry.SpawnedCount < entry.TotalCount)
            {
                entry.IsActive = true;
                entry.Timer = 0f; // 立即允许第一次生成
                BabelLogger.AC("CSV", $"Wave {entry.WaveId} activated: {entry.EnemyType} x{entry.TotalCount} every {entry.Interval}s ({entry.Side})");
            }

            if (!entry.IsActive)
                continue;

            // 超时或已生成完毕
            if (elapsed >= entry.EndTime || entry.SpawnedCount >= entry.TotalCount)
            {
                entry.IsActive = false;
                continue;
            }

            // 计时器倒计时
            entry.Timer -= Time.deltaTime;
            if (entry.Timer <= 0f)
            {
                SpawnOne(entry);
                entry.SpawnedCount++;
                entry.Timer = entry.Interval;

                if (entry.SpawnedCount >= entry.TotalCount)
                {
                    entry.IsActive = false;
                    BabelLogger.AC("CSV", $"Wave {entry.WaveId} complete: {entry.EnemyType}");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // 公共接口
    // -------------------------------------------------------------------------

    public void StartSpawning()
    {
        LoadWaves();
        _isSpawning = true;
        BabelLogger.AC("CSV", $"WaveSpawnSystem started, {_waves.Count} wave entries loaded.");
    }

    public void StopSpawning()
    {
        _isSpawning = false;
    }

    // -------------------------------------------------------------------------
    // 私有方法
    // -------------------------------------------------------------------------

    private void LoadWaves()
    {
        _waves.Clear();
        _spawnFromLeft = true;

        TextAsset csv = Resources.Load<TextAsset>("Data/level_waves");
        if (csv == null)
        {
            BabelLogger.AC("CSV", "level_waves.csv not found in Resources/Data/");
            return;
        }

        var (header, rows) = CsvParser.Parse(csv.text);
        if (rows == null || rows.Count == 0)
        {
            BabelLogger.AC("CSV", "level_waves.csv is empty or has no data rows.");
            return;
        }

        int colWaveId   = header.ContainsKey("waveid")     ? header["waveid"]     : -1;
        int colStart    = header.ContainsKey("starttime")  ? header["starttime"]  : -1;
        int colEnd      = header.ContainsKey("endtime")    ? header["endtime"]    : -1;
        int colType     = header.ContainsKey("enemytype")  ? header["enemytype"]  : -1;
        int colCount    = header.ContainsKey("count")      ? header["count"]      : -1;
        int colInterval = header.ContainsKey("interval")   ? header["interval"]   : -1;
        int colSide     = header.ContainsKey("spawnside")  ? header["spawnside"]  : -1;

        for (int r = 0; r < rows.Count; r++)
        {
            string[] row = rows[r];
            if (row == null || row.Length == 0)
                continue;

            // 跳过空行或注释行
            if (row.Length <= 1 && string.IsNullOrWhiteSpace(row[0]))
                continue;

            WaveEntry entry = new WaveEntry();

            entry.WaveId     = CsvParser.GetInt(row,   colWaveId,   0);
            entry.StartTime  = CsvParser.GetFloat(row, colStart,    0f);
            entry.EndTime    = CsvParser.GetFloat(row, colEnd,      float.MaxValue);
            entry.TotalCount = CsvParser.GetInt(row,   colCount,    1);
            entry.Interval   = Mathf.Max(0.1f, CsvParser.GetFloat(row, colInterval, 2f));

            string typeStr = CsvParser.GetString(row, colType, "");
            if (!Enum.TryParse(typeStr, ignoreCase: true, out entry.EnemyType))
            {
                BabelLogger.AC("CSV", $"Row {r}: unknown EnemyType '{typeStr}', skipping.");
                continue;
            }

            string sideStr = CsvParser.GetString(row, colSide, "");
            if (!Enum.TryParse(sideStr, ignoreCase: true, out entry.Side))
                entry.Side = SpawnSide.Alternate;

            _waves.Add(entry);
        }

        BabelLogger.AC("CSV", $"Loaded {_waves.Count} wave entries from level_waves.csv.");
    }

    private void SpawnOne(WaveEntry entry)
    {
        if (_enemyDatabase == null)
            return;

        EnemyData data = _enemyDatabase.GetByType(entry.EnemyType);
        if (data == null)
        {
            BabelLogger.AC("CSV", $"WaveSpawnSystem: no EnemyData for type {entry.EnemyType}");
            return;
        }

        Transform spawnPoint;
        switch (entry.Side)
        {
            case SpawnSide.Left:
                spawnPoint = _leftSpawnPoint != null ? _leftSpawnPoint : transform;
                break;
            case SpawnSide.Right:
                spawnPoint = _rightSpawnPoint != null ? _rightSpawnPoint : transform;
                break;
            case SpawnSide.Alternate:
            default:
                spawnPoint = _spawnFromLeft
                    ? (_leftSpawnPoint != null  ? _leftSpawnPoint  : transform)
                    : (_rightSpawnPoint != null ? _rightSpawnPoint : transform);
                _spawnFromLeft = !_spawnFromLeft;
                break;
        }

        Vector2 targetPos = _towerSystem != null
            ? (Vector2)_towerSystem.transform.position
            : Vector2.zero;

        EnemyController enemy = EnemyPool.Instance.Get(data, spawnPoint.position);
        if (enemy == null)
            return;

        enemy.Initialize(data, targetPos, _towerSystem, startLayer: 0);
    }

    private void OnEnemyReachedTower(EnemyData data)
    {
        if (_towerSystem != null)
            _towerSystem.AddProgress(data);
    }

    private void HandleGamePaused()
    {
        _isSpawning = false;
    }

    private void HandleGameResumed()
    {
        _isSpawning = true;
    }

}
