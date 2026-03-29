using UnityEngine;

/// <summary>
/// Tower layer data model implementing the layer progress rules from
/// design/gdd/塔建造系统.md.
/// </summary>
[System.Serializable]
public class TowerLayer
{
    public int LayerIndex { get; private set; }
    public float CompletionPercent { get; private set; }
    public bool IsCompleted => CompletionPercent >= 100f;
    public bool IsUnlocked { get; private set; }

    public TowerLayer(int index, bool unlocked)
    {
        LayerIndex = index;
        CompletionPercent = 0f;
        IsUnlocked = unlocked;
    }

    /// <summary>Adds progress to this layer. Returns overflow amount.</summary>
    public float AddProgress(float amount)
    {
        if (!IsUnlocked || IsCompleted || amount <= 0f)
            return amount <= 0f ? 0f : amount;

        float previous = CompletionPercent;
        CompletionPercent = Mathf.Clamp(CompletionPercent + amount, 0f, 100f);
        float applied = CompletionPercent - previous;
        return Mathf.Max(0f, amount - applied);
    }

    public void Unlock() => IsUnlocked = true;

    /// <summary>Resets progress to 0. Unlock state unchanged.</summary>
    public void Reset() => CompletionPercent = 0f;

    /// <summary>Sets the lock state explicitly (used during game reset).</summary>
    public void SetLocked(bool unlocked) => IsUnlocked = unlocked;
}
