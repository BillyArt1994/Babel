using System;

/// <summary>
/// Per-layer tower progress events. Separate from TowerEvents.OnTowerCompleted
/// which signals game-over. These are used for HUD and visual feedback.
/// </summary>
public static class TowerProgressEvents
{
    /// <summary>Fired whenever a layer's completion percent changes. (layerIndex, percent 0-100)</summary>
    public static event Action<int, float> OnLayerProgressChanged;

    /// <summary>Fired when a layer reaches 100% completion.</summary>
    public static event Action<int> OnLayerCompleted;

    public static void RaiseLayerProgressChanged(int layerIndex, float percent)
        => OnLayerProgressChanged?.Invoke(layerIndex, percent);

    public static void RaiseLayerCompleted(int layerIndex)
        => OnLayerCompleted?.Invoke(layerIndex);

    /// <summary>Fired when all passage slots on a layer are occupied — triggers stair climbing.</summary>
    public static event Action<int> OnLayerPassageSlotsFull;

    public static void RaiseLayerPassageSlotsFull(int layerIndex)
        => OnLayerPassageSlotsFull?.Invoke(layerIndex);
}
