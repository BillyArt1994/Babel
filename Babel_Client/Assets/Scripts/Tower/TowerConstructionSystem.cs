using UnityEngine;

/// <summary>
/// Implements the tower construction system from design/gdd/塔建造系统.md.
/// Manages 10 tower layers, per-layer progress, visual representation,
/// and fires TowerEvents.RaiseTowerCompleted() when the top layer finishes.
/// </summary>
public class TowerConstructionSystem : MonoBehaviour
{
    private const int LAYER_COUNT = 10;
    private const float BASE_WIDTH = 10f;
    private const float TOP_WIDTH_RATIO = 0.2f;
    private const float LAYER_HEIGHT = 1.2f;
    private const float REQUIRED_POINTS_BOTTOM = 100f;
    private const float REQUIRED_POINTS_TOP = 20f;

    [SerializeField] private Transform _towerRoot;
    [SerializeField] private Sprite _layerSprite;

    private TowerLayer[] _layers;
    private SpriteRenderer[] _layerRenderers;
    private int _currentActiveLayer;

    // ── Public query API ──────────────────────────────────────────────────────

    /// <summary>Returns overall tower completion as a value in [0, 100].</summary>
    public float GetTotalCompletionPercent()
    {
        float currentTotal = 0f;
        float requiredTotal = 0f;
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            currentTotal += _layers[i].CurrentPoints;
            requiredTotal += _layers[i].RequiredPoints;
        }
        return requiredTotal > 0f ? currentTotal / requiredTotal * 100f : 0f;
    }

    public int GetCurrentLayer() => _currentActiveLayer;

    public float GetActiveLayerWorldY()
    {
        float rootY = _towerRoot != null ? _towerRoot.position.y : 0f;
        return rootY + _currentActiveLayer * LAYER_HEIGHT;
    }

    public float GetLayerCompletion(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= LAYER_COUNT) return 0f;
        return _layers[layerIndex].CompletionPercent;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_towerRoot == null) _towerRoot = transform;
        InitializeLayers();
        BuildLayerVisuals();
        RefreshAllVisuals();
    }

    private void OnEnable()  => GameEvents.OnGameStart += OnGameStart;
    private void OnDisable() => GameEvents.OnGameStart -= OnGameStart;

    private void InitializeLayers()
    {
        _layers = new TowerLayer[LAYER_COUNT];
        _layerRenderers = new SpriteRenderer[LAYER_COUNT];
        _currentActiveLayer = 0;

        for (int i = 0; i < LAYER_COUNT; i++)
        {
            float t = LAYER_COUNT > 1 ? (float)i / (LAYER_COUNT - 1) : 0f;
            float required = Mathf.Lerp(REQUIRED_POINTS_BOTTOM, REQUIRED_POINTS_TOP, t);
            _layers[i] = new TowerLayer(i, required, unlocked: i == 0);
        }
    }

    private void BuildLayerVisuals()
    {
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            var go = new GameObject($"TowerLayer_{i + 1}");
            go.transform.SetParent(_towerRoot, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, i * LAYER_HEIGHT, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _layerSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            _layerRenderers[i] = sr;
        }
    }

    private void OnGameStart()
    {
        _currentActiveLayer = 0;
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            _layers[i].Reset();
            _layers[i].SetUnlocked(i == 0);   // only layer 0 starts unlocked
        }
        RefreshAllVisuals();
    }

    // ── Core gameplay ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by EnemySpawnSystem when an enemy reaches the tower.
    /// Adds enemyData.BuildContribution points to the current active layer.
    /// </summary>
    public void AddProgress(EnemyData enemyData)
    {
        if (enemyData == null) return;
        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying()) return;
        if (_currentActiveLayer < 0 || _currentActiveLayer >= LAYER_COUNT) return;

        TowerLayer active = _layers[_currentActiveLayer];
        if (!active.IsUnlocked || active.IsCompleted) return;

        active.AddPoints(enemyData.BuildContribution);
        TowerProgressEvents.RaiseLayerProgressChanged(_currentActiveLayer, active.CompletionPercent);
        UpdateLayerVisual(_currentActiveLayer);

        if (!active.IsCompleted) return;

        TowerProgressEvents.RaiseLayerCompleted(_currentActiveLayer);

        if (_currentActiveLayer == LAYER_COUNT - 1)
        {
            RefreshAllVisuals();
            TowerEvents.RaiseTowerCompleted();  // ← triggers GameLoopManager defeat
            return;
        }

        int justCompleted = _currentActiveLayer;
        _currentActiveLayer++;
        _layers[_currentActiveLayer].Unlock();

        UpdateLayerVisual(justCompleted);
        UpdateLayerVisual(_currentActiveLayer);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    private void RefreshAllVisuals()
    {
        for (int i = 0; i < LAYER_COUNT; i++)
            UpdateLayerVisual(i);
    }

    private void UpdateLayerVisual(int layerIndex)
    {
        if (_layerRenderers == null || layerIndex < 0 || layerIndex >= _layerRenderers.Length) return;

        SpriteRenderer sr = _layerRenderers[layerIndex];
        if (sr == null) return;

        TowerLayer layer = _layers[layerIndex];
        float targetWidth = GetLayerTargetWidth(layerIndex);
        float visibleWidth = Mathf.Max(0.001f, targetWidth * Mathf.Clamp01(layer.CompletionPercent / 100f));

        sr.transform.localScale = new Vector3(visibleWidth, LAYER_HEIGHT, 1f);

        Color c = Color.white;
        if (layer.IsCompleted)
            c.a = 0.7f;
        else if (layerIndex == _currentActiveLayer && layer.IsUnlocked)
            c.a = 1.0f;
        else
            c.a = 0.3f;

        sr.color = c;
    }

    private float GetLayerTargetWidth(int layerIndex)
    {
        float step = (1f - TOP_WIDTH_RATIO) / (LAYER_COUNT - 1f);
        return BASE_WIDTH * (1f - layerIndex * step);
    }
}
