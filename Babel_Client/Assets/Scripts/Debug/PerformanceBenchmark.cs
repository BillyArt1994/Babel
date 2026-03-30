#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// S2-08: 100-unit on-screen performance benchmark.
/// Attach to any GameObject in the scene. Use the Inspector context menu
/// (right-click the component header) to trigger the benchmark.
///
/// The script spawns 100 enemy units, runs for a configurable duration with
/// the frame rate cap removed, records per-frame delta times, and outputs
/// statistics to both Debug.Log and a markdown report file.
/// </summary>
public class PerformanceBenchmark : MonoBehaviour
{
    [Header("Benchmark Settings")]
    [SerializeField] private int _unitCount = 100;
    [SerializeField] private float _benchmarkDuration = 5f;
    [SerializeField] private float _spawnRadius = 8f;
    [SerializeField] private Vector2 _spawnCenter = Vector2.zero;

    [Header("Enemy Prefab (fallback if EnemyPool unavailable)")]
    [SerializeField] private GameObject _fallbackEnemyPrefab;

    [Header("Output")]
    [SerializeField] private string _reportFileName = "s2-08-performance-report-runtime.md";

    private readonly List<float> _frameTimes = new List<float>(2048);
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>(128);
    private bool _isRunning;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (_isRunning)
        {
            Debug.LogWarning("[PerformanceBenchmark] Benchmark is already running.");
            return;
        }

        StartCoroutine(BenchmarkCoroutine());
    }

    [ContextMenu("Run Benchmark (200 units)")]
    public void RunBenchmark200()
    {
        _unitCount = 200;
        RunBenchmark();
    }

    // ------------------------------------------------------------------
    // Core benchmark coroutine
    // ------------------------------------------------------------------

    private IEnumerator BenchmarkCoroutine()
    {
        _isRunning = true;
        _frameTimes.Clear();

        Debug.Log($"[PerformanceBenchmark] Starting benchmark: {_unitCount} units, {_benchmarkDuration}s duration.");

        // Remove frame rate cap for accurate measurement
        int previousTargetFrameRate = Application.targetFrameRate;
        int previousVSyncCount = QualitySettings.vSyncCount;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        // Spawn units
        SpawnUnits();

        // Wait one frame for physics and rendering to initialize
        yield return null;

        Debug.Log($"[PerformanceBenchmark] {_spawnedEnemies.Count} units spawned. Recording frames...");

        // Record frames for the specified duration
        float elapsed = 0f;
        while (elapsed < _benchmarkDuration)
        {
            _frameTimes.Add(Time.unscaledDeltaTime);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log($"[PerformanceBenchmark] Recording complete. {_frameTimes.Count} frames captured.");

        // Analyze results
        BenchmarkResult result = AnalyzeResults();

        // Output
        LogResults(result);
        WriteReport(result);

        // Cleanup
        CleanupSpawnedUnits();

        // Restore frame rate settings
        Application.targetFrameRate = previousTargetFrameRate;
        QualitySettings.vSyncCount = previousVSyncCount;

        _isRunning = false;
        Debug.Log("[PerformanceBenchmark] Benchmark complete.");
    }

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    private void SpawnUnits()
    {
        _spawnedEnemies.Clear();

        // Try using EnemyPool first
        bool usePool = TrySpawnViaPool();
        if (usePool)
            return;

        // Fallback: direct instantiation
        SpawnViaInstantiate();
    }

    private bool TrySpawnViaPool()
    {
        if (EnemyPool.Instance == null)
            return false;

        // We need an EnemyData to use the pool. Try to find one from the
        // EnemyDatabase via EnemySpawnSystem, or from an existing EnemyController.
        EnemyData workerData = FindWorkerEnemyData();
        if (workerData == null)
        {
            Debug.Log("[PerformanceBenchmark] Could not find EnemyData for pool spawning. Falling back to Instantiate.");
            return false;
        }

        Vector2 targetPos = Vector2.zero;
        int spawned = 0;
        for (int i = 0; i < _unitCount; i++)
        {
            Vector2 pos = GetRandomSpawnPosition(i);
            EnemyController enemy = EnemyPool.Instance.Get(workerData, pos);
            if (enemy == null)
            {
                Debug.LogWarning($"[PerformanceBenchmark] Pool returned null at unit {i}. Remaining units will use Instantiate fallback.");
                break;
            }

            enemy.Initialize(workerData, targetPos);
            _spawnedEnemies.Add(enemy.gameObject);
            spawned++;
        }

        // If pool could not provide enough, fill the rest via Instantiate
        if (spawned < _unitCount)
        {
            int remaining = _unitCount - spawned;
            Debug.Log($"[PerformanceBenchmark] Pool provided {spawned} units. Instantiating {remaining} more.");
            SpawnViaInstantiate(remaining, spawned);
        }

        return true;
    }

    private void SpawnViaInstantiate(int count = -1, int startIndex = 0)
    {
        if (count < 0)
            count = _unitCount;

        GameObject prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogError("[PerformanceBenchmark] No enemy prefab available. Cannot spawn units. " +
                           "Assign _fallbackEnemyPrefab in the Inspector, or ensure Worker.prefab exists at Prefabs/Enemies/Worker.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = GetRandomSpawnPosition(startIndex + i);
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);
            go.name = $"BenchmarkEnemy_{startIndex + i}";
            _spawnedEnemies.Add(go);
        }
    }

    private GameObject ResolvePrefab()
    {
        if (_fallbackEnemyPrefab != null)
            return _fallbackEnemyPrefab;

        // Try loading from a known path
        GameObject loaded = Resources.Load<GameObject>("Prefabs/Enemies/Worker");
        if (loaded != null)
            return loaded;

        // Try finding an EnemyData with a prefab reference
        EnemyData data = FindWorkerEnemyData();
        if (data != null && data.Prefab != null)
            return data.Prefab;

        return null;
    }

    private EnemyData FindWorkerEnemyData()
    {
        // Try to find via EnemySpawnSystem's database
        EnemySpawnSystem spawnSystem = FindFirstObjectByType<EnemySpawnSystem>();
        if (spawnSystem != null)
        {
            // Use reflection or a known database reference
            // Since EnemyDatabase is a serialized field, try FindFirstObjectByType on the database
        }

        // Search all loaded EnemyData ScriptableObjects
        EnemyData[] allData = Resources.FindObjectsOfTypeAll<EnemyData>();
        if (allData != null)
        {
            // Prefer Worker type
            for (int i = 0; i < allData.Length; i++)
            {
                if (allData[i] != null && allData[i].EnemyType == EnemyType.Worker)
                    return allData[i];
            }

            // Any type will do
            for (int i = 0; i < allData.Length; i++)
            {
                if (allData[i] != null)
                    return allData[i];
            }
        }

        return null;
    }

    private Vector2 GetRandomSpawnPosition(int index)
    {
        // Distribute units in a circle with some jitter for realism
        float angle = (index / (float)_unitCount) * Mathf.PI * 2f;
        float radiusJitter = Random.Range(0.5f, 1.0f) * _spawnRadius;
        return _spawnCenter + new Vector2(
            Mathf.Cos(angle) * radiusJitter,
            Mathf.Sin(angle) * radiusJitter
        );
    }

    // ------------------------------------------------------------------
    // Cleanup
    // ------------------------------------------------------------------

    private void CleanupSpawnedUnits()
    {
        // Return pooled enemies first
        if (EnemyPool.Instance != null)
        {
            EnemyPool.Instance.ReturnAll();
        }

        // Destroy any manually instantiated objects that remain
        for (int i = 0; i < _spawnedEnemies.Count; i++)
        {
            if (_spawnedEnemies[i] != null && _spawnedEnemies[i].activeInHierarchy)
            {
                Destroy(_spawnedEnemies[i]);
            }
        }

        _spawnedEnemies.Clear();
    }

    // ------------------------------------------------------------------
    // Analysis
    // ------------------------------------------------------------------

    private struct BenchmarkResult
    {
        public int UnitCount;
        public int FrameCount;
        public float DurationSeconds;
        public float AvgFps;
        public float MinFps;
        public float MaxFps;
        public float P5Fps;       // worst 5% of frames
        public float P1Fps;       // worst 1% of frames
        public float MedianFps;
        public float AvgFrameTimeMs;
        public float MaxFrameTimeMs;
        public float P95FrameTimeMs;
        public float P99FrameTimeMs;
        public bool MeetsBudget;   // >= 60 fps average
    }

    private BenchmarkResult AnalyzeResults()
    {
        BenchmarkResult result = new BenchmarkResult();
        result.UnitCount = _spawnedEnemies.Count > 0 ? _spawnedEnemies.Count : _unitCount;
        result.FrameCount = _frameTimes.Count;

        if (_frameTimes.Count == 0)
        {
            Debug.LogWarning("[PerformanceBenchmark] No frame data recorded.");
            return result;
        }

        // Convert to arrays for sorting
        float[] times = _frameTimes.ToArray();
        float totalTime = 0f;
        float minTime = float.MaxValue;
        float maxTime = 0f;

        for (int i = 0; i < times.Length; i++)
        {
            float t = times[i];
            totalTime += t;
            if (t < minTime) minTime = t;
            if (t > maxTime) maxTime = t;
        }

        result.DurationSeconds = totalTime;
        result.AvgFrameTimeMs = (totalTime / times.Length) * 1000f;
        result.MaxFrameTimeMs = maxTime * 1000f;
        result.AvgFps = times.Length / totalTime;
        result.MinFps = 1f / maxTime;     // slowest frame = lowest fps
        result.MaxFps = 1f / minTime;     // fastest frame = highest fps

        // Sort for percentile calculations (ascending = fastest frames first)
        System.Array.Sort(times);

        result.MedianFps = 1f / times[times.Length / 2];

        // P5: worst 5% (top 5% of frame times, which are at the end after sorting)
        int p5StartIndex = Mathf.Max(0, Mathf.FloorToInt(times.Length * 0.95f));
        float p5Total = 0f;
        int p5Count = times.Length - p5StartIndex;
        for (int i = p5StartIndex; i < times.Length; i++)
            p5Total += times[i];
        result.P5Fps = p5Count > 0 ? p5Count / p5Total : 0f;

        // P1: worst 1%
        int p1StartIndex = Mathf.Max(0, Mathf.FloorToInt(times.Length * 0.99f));
        float p1Total = 0f;
        int p1Count = times.Length - p1StartIndex;
        for (int i = p1StartIndex; i < times.Length; i++)
            p1Total += times[i];
        result.P1Fps = p1Count > 0 ? p1Count / p1Total : 0f;

        // Percentile frame times
        result.P95FrameTimeMs = times[Mathf.Min(p5StartIndex, times.Length - 1)] * 1000f;
        result.P99FrameTimeMs = times[Mathf.Min(p1StartIndex, times.Length - 1)] * 1000f;

        result.MeetsBudget = result.AvgFps >= 60f;

        return result;
    }

    // ------------------------------------------------------------------
    // Output
    // ------------------------------------------------------------------

    private void LogResults(BenchmarkResult r)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("========== PERFORMANCE BENCHMARK RESULTS ==========");
        sb.AppendLine($"  Units:           {r.UnitCount}");
        sb.AppendLine($"  Duration:        {r.DurationSeconds:F2}s ({r.FrameCount} frames)");
        sb.AppendLine($"  Avg FPS:         {r.AvgFps:F1}");
        sb.AppendLine($"  Min FPS:         {r.MinFps:F1}");
        sb.AppendLine($"  Max FPS:         {r.MaxFps:F1}");
        sb.AppendLine($"  Median FPS:      {r.MedianFps:F1}");
        sb.AppendLine($"  P5 Low FPS:      {r.P5Fps:F1}  (worst 5%)");
        sb.AppendLine($"  P1 Low FPS:      {r.P1Fps:F1}  (worst 1%)");
        sb.AppendLine($"  Avg Frame Time:  {r.AvgFrameTimeMs:F2}ms");
        sb.AppendLine($"  Max Frame Time:  {r.MaxFrameTimeMs:F2}ms");
        sb.AppendLine($"  P95 Frame Time:  {r.P95FrameTimeMs:F2}ms");
        sb.AppendLine($"  P99 Frame Time:  {r.P99FrameTimeMs:F2}ms");
        sb.AppendLine($"  Budget (>=60fps): {(r.MeetsBudget ? "PASS" : "FAIL")}");
        sb.AppendLine("====================================================");

        Debug.Log(sb.ToString());
    }

    private void WriteReport(BenchmarkResult r)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string reportDir = Path.Combine(projectRoot, "production", "reports");

        if (!Directory.Exists(reportDir))
            Directory.CreateDirectory(reportDir);

        string reportPath = Path.Combine(reportDir, _reportFileName);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# S2-08 Performance Benchmark -- Runtime Results");
        sb.AppendLine();
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Unity Version: {Application.unityVersion}");
        sb.AppendLine($"Platform: {Application.platform}");
        sb.AppendLine();
        sb.AppendLine("## Test Configuration");
        sb.AppendLine();
        sb.AppendLine($"| Parameter | Value |");
        sb.AppendLine($"|-----------|-------|");
        sb.AppendLine($"| Unit Count | {r.UnitCount} |");
        sb.AppendLine($"| Duration | {r.DurationSeconds:F2}s |");
        sb.AppendLine($"| VSync | Disabled |");
        sb.AppendLine($"| Frame Rate Cap | Unlimited |");
        sb.AppendLine();
        sb.AppendLine("## Results");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value | Budget | Status |");
        sb.AppendLine($"|--------|-------|--------|--------|");
        sb.AppendLine($"| Average FPS | {r.AvgFps:F1} | >= 60 | {(r.MeetsBudget ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| Min FPS | {r.MinFps:F1} | -- | -- |");
        sb.AppendLine($"| Max FPS | {r.MaxFps:F1} | -- | -- |");
        sb.AppendLine($"| Median FPS | {r.MedianFps:F1} | -- | -- |");
        sb.AppendLine($"| P5 Low FPS | {r.P5Fps:F1} | >= 30 | {(r.P5Fps >= 30f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| P1 Low FPS | {r.P1Fps:F1} | -- | -- |");
        sb.AppendLine($"| Avg Frame Time | {r.AvgFrameTimeMs:F2}ms | <= 16.67ms | {(r.AvgFrameTimeMs <= 16.67f ? "PASS" : "FAIL")} |");
        sb.AppendLine($"| Max Frame Time | {r.MaxFrameTimeMs:F2}ms | -- | -- |");
        sb.AppendLine($"| P95 Frame Time | {r.P95FrameTimeMs:F2}ms | -- | -- |");
        sb.AppendLine($"| P99 Frame Time | {r.P99FrameTimeMs:F2}ms | -- | -- |");
        sb.AppendLine();
        sb.AppendLine($"## Verdict: {(r.MeetsBudget ? "PASS" : "FAIL")}");
        sb.AppendLine();

        File.WriteAllText(reportPath, sb.ToString());
        Debug.Log($"[PerformanceBenchmark] Report written to: {reportPath}");
    }
}
#endif
