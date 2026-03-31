using System.Collections.Generic;
using Babel.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tower construction system with TileMap-based visuals.
/// Each SlotData maps to a Tilemap cell; enemies arriving reveal tiles in place.
/// EnemyController and LayerSlotManager are unchanged — SlotData.position remains world-space Vector2.
/// </summary>
public class TowerConstructionSystem : MonoBehaviour
{
    public const int LAYER_COUNT = 10;
    private const float LAYER_HEIGHT = 1.2f;

    private static readonly Color TILE_HIDDEN  = Color.clear;
    private static readonly Color TILE_NORMAL  = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color TILE_PASSAGE = new Color(0.4f,  0.4f,  0.5f,  1f);

    [SerializeField] private Transform _towerRoot;
    [SerializeField] private MapConfig _mapConfig;
    [SerializeField] private Tilemap   _tilemap;   // assign the Tilemap child in Inspector
    [SerializeField] private TileBase  _baseTile;  // assign Assets/Tiles/BlockTile.asset

    private TowerLayer[]        _layers;
    private int                 _currentActiveLayer;
    private LayerSlotManager[]  _slotManagers;

    // slot → cell coord cache (avoids repeated WorldToCell calls)
    private readonly Dictionary<SlotData, Vector3Int> _slotCells = new Dictionary<SlotData, Vector3Int>();

    // ── Public query API ─────────────────────────────────────────────────────

    public int GetCurrentLayer() => _currentActiveLayer;

    public LayerSlotManager GetSlotManager(int layerIndex)
    {
        if (_slotManagers == null || layerIndex < 0 || layerIndex >= LAYER_COUNT) return null;
        return _slotManagers[layerIndex];
    }

    public LayerSlotManager GetCurrentSlotManager() => GetSlotManager(_currentActiveLayer);

    public float GetActiveLayerWorldY()
    {
        float rootY = _towerRoot != null ? _towerRoot.position.y : 0f;
        return rootY + _currentActiveLayer * LAYER_HEIGHT;
    }

    public float GetLayerCompletion(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= LAYER_COUNT) return 0f;
        LayerSlotManager mgr = GetSlotManager(layerIndex);
        return (mgr != null && mgr.AreAllSlotsBuilt()) ? 100f : 0f;
    }

    public float GetTotalCompletionPercent()
    {
        int built = 0;
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            LayerSlotManager mgr = GetSlotManager(i);
            if (mgr != null && mgr.AreAllSlotsBuilt()) built++;
        }
        return (float)built / LAYER_COUNT * 100f;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_towerRoot == null) _towerRoot = transform;
        InitializeLayers();
        InitializeSlotManagers();
        BuildSlotVisuals();
    }

    private void OnEnable()  => GameEvents.OnGameStart += OnGameStart;
    private void OnDisable() => GameEvents.OnGameStart -= OnGameStart;

    private void InitializeLayers()
    {
        _layers = new TowerLayer[LAYER_COUNT];
        _currentActiveLayer = 0;
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            float t = LAYER_COUNT > 1 ? (float)i / (LAYER_COUNT - 1) : 0f;
            float required = Mathf.Lerp(100f, 20f, t);
            _layers[i] = new TowerLayer(i, required, unlocked: i == 0);
        }
    }

    private void InitializeSlotManagers()
    {
        _slotManagers = new LayerSlotManager[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            List<SlotData> normal  = _mapConfig != null ? _mapConfig.GetNormalSlots(i)  : new List<SlotData>();
            List<SlotData> passage = _mapConfig != null ? _mapConfig.GetPassageSlots(i) : new List<SlotData>();
            _slotManagers[i] = new LayerSlotManager(i, normal, passage);
        }
    }

    /// <summary>
    /// Pre-populate the Tilemap with hidden tiles at every slot position.
    /// Also caches the cell coordinate for each SlotData.
    /// </summary>
    private void BuildSlotVisuals()
    {
        _slotCells.Clear();

        if (_tilemap == null || _baseTile == null || _mapConfig == null) return;

        _tilemap.ClearAllTiles();

        for (int i = 0; i < LAYER_COUNT; i++)
        {
            var layerData = _mapConfig.GetLayer(i);
            if (layerData == null) continue;

            foreach (var slot in layerData.slots)
            {
                Vector3Int cell = WorldToCell(slot.position);
                _slotCells[slot] = cell;

                _tilemap.SetTile(cell, _baseTile);
                _tilemap.SetTileFlags(cell, TileFlags.None); // allow runtime color
                _tilemap.SetColor(cell, TILE_HIDDEN);
            }
        }
    }

    private void OnGameStart()
    {
        _currentActiveLayer = 0;
        for (int i = 0; i < LAYER_COUNT; i++)
        {
            _layers[i].Reset();
            _layers[i].SetUnlocked(i == 0);
            _slotManagers[i]?.Reset();
        }
        // Reset all tiles to hidden
        foreach (var kvp in _slotCells)
        {
            _tilemap.SetColor(kvp.Value, TILE_HIDDEN);
        }
    }

    // ── Core gameplay ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by EnemyController on arrival at its slot.
    /// Reveals the corresponding Tilemap cell and checks layer/tower completion.
    /// </summary>
    public void BuildSlotAtPosition(EnemyController enemy, int layerIndex)
    {
        if (enemy == null) return;
        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying()) return;

        LayerSlotManager mgr = GetSlotManager(layerIndex);
        if (mgr == null) return;

        SlotData slot = mgr.GetSlotForOccupant(enemy);
        if (slot == null) return;

        // Reveal tile
        if (_tilemap != null && _slotCells.TryGetValue(slot, out Vector3Int cell))
        {
            _tilemap.SetColor(cell, slot.isPassage ? TILE_PASSAGE : TILE_NORMAL);
        }

        mgr.MarkBuilt(slot);

        BabelLogger.AC("S3-14", string.Concat(
            "Block revealed at layer=", layerIndex.ToString(),
            " pos=", slot.position.ToString(),
            " isPassage=", slot.isPassage.ToString()));

        TowerProgressEvents.RaiseLayerProgressChanged(layerIndex, GetLayerCompletion(layerIndex));

        if (!mgr.AreAllSlotsBuilt()) return;

        TowerProgressEvents.RaiseLayerCompleted(layerIndex);
        BabelLogger.AC("S3-14", string.Concat("Layer complete: layer=", layerIndex.ToString()));

        if (layerIndex == LAYER_COUNT - 1)
        {
            TowerEvents.RaiseTowerCompleted();
            return;
        }

        _currentActiveLayer = layerIndex + 1;
        _layers[_currentActiveLayer].Unlock();
    }

    /// <summary>
    /// Called by EnemyController when it arrives at a passage slot.
    /// Reveals the tile and marks it built. Does NOT check layer completion here —
    /// the enemy continues climbing immediately after.
    /// </summary>
    /// <summary>Returns true if this was the first build (enemy should be consumed). False if already built.</summary>
    public bool BuildPassageAtPosition(Vector2 passageWorldPos, int layerIndex)
    {
        if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsPlaying()) return false;

        LayerSlotManager mgr = GetSlotManager(layerIndex);
        if (mgr == null) return false;

        bool built = mgr.TryMarkPassageBuiltAt(passageWorldPos);
        if (!built) return false; // already built by another enemy

        // Reveal the tile at this world position
        foreach (var kvp in _slotCells)
        {
            if (kvp.Key.isPassage && Vector2.SqrMagnitude(kvp.Key.position - passageWorldPos) < 0.04f)
            {
                if (_tilemap != null)
                    _tilemap.SetColor(kvp.Value, TILE_PASSAGE);
                break;
            }
        }

        BabelLogger.AC("S3-14", string.Concat(
            "Passage built at layer=", layerIndex.ToString(),
            " pos=", passageWorldPos.ToString()));

        TowerProgressEvents.RaiseLayerProgressChanged(layerIndex, GetLayerCompletion(layerIndex));

        if (!mgr.AreAllSlotsBuilt()) return true;

        TowerProgressEvents.RaiseLayerCompleted(layerIndex);
        BabelLogger.AC("S3-14", string.Concat("Layer complete: layer=", layerIndex.ToString()));

        if (layerIndex == LAYER_COUNT - 1)
        {
            TowerEvents.RaiseTowerCompleted();
            return true;
        }

        _currentActiveLayer = layerIndex + 1;
        _layers[_currentActiveLayer].Unlock();
        return true;
    }

    /// <summary>Legacy: kept for EnemySpawnSystem compatibility, no longer drives visuals.</summary>
    public void AddProgress(EnemyData enemyData) { }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector3Int WorldToCell(Vector2 worldPos)
    {
        if (_tilemap == null) return Vector3Int.zero;
        return _tilemap.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0f));
    }
}
