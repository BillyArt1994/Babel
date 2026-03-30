using UnityEngine;

/// <summary>
/// Tower layer data model implementing the layer progress rules from
/// design/gdd/塔建造系统.md.
/// Each layer has an independent requiredPoints derived from its width (layer index).
/// </summary>
[System.Serializable]
public class TowerLayer
{
    public int LayerIndex { get; private set; }
    public float CurrentPoints { get; private set; }
    public float RequiredPoints { get; private set; }
    public float CompletionPercent => RequiredPoints > 0f ? Mathf.Clamp01(CurrentPoints / RequiredPoints) * 100f : 0f;
    public bool IsCompleted => CurrentPoints >= RequiredPoints;
    public bool IsUnlocked { get; private set; }

    public TowerLayer(int index, float requiredPoints, bool unlocked)
    {
        LayerIndex = index;
        RequiredPoints = requiredPoints;
        CurrentPoints = 0f;
        IsUnlocked = unlocked;
    }

    /// <summary>Adds build points to this layer. Clamps to requiredPoints; no overflow.</summary>
    public void AddPoints(float points)
    {
        if (!IsUnlocked || IsCompleted || points <= 0f)
            return;

        CurrentPoints = Mathf.Min(CurrentPoints + points, RequiredPoints);
    }

    public void Unlock() => IsUnlocked = true;

    /// <summary>Resets progress to 0. Unlock state unchanged.</summary>
    public void Reset() => CurrentPoints = 0f;

    /// <summary>Sets the unlock state explicitly (used during game reset).</summary>
    public void SetUnlocked(bool unlocked) => IsUnlocked = unlocked;
}
