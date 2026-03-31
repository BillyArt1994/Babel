using System.Collections.Generic;
using Babel.Map;
using UnityEngine;

/// <summary>
/// Manages slot occupancy for a single tower layer.
/// Tracks which normal/passage slots are occupied by which EnemyController.
/// Thread-safety note: Unity is single-threaded; no locking needed.
/// Implements S3-14 Phase 1 — see design/gdd/塔建造系统.md 通道槽设计.
/// </summary>
public class LayerSlotManager
{
    private readonly int _layerIndex;
    private readonly List<SlotData> _normalSlots;
    private readonly List<SlotData> _passageSlots;

    // slot → occupying enemy (null = free)
    private readonly Dictionary<SlotData, EnemyController> _occupancy;

    private readonly HashSet<SlotData> _builtSlots = new HashSet<SlotData>();
    // Reverse lookup: enemy → slot (for MarkBuilt call from outside)
    private readonly Dictionary<EnemyController, SlotData> _reverseOccupancy = new Dictionary<EnemyController, SlotData>();

    public int LayerIndex => _layerIndex;
    public int NormalSlotCount => _normalSlots.Count;
    public int PassageSlotCount => _passageSlots.Count;

    public LayerSlotManager(int layerIndex, List<SlotData> normalSlots, List<SlotData> passageSlots)
    {
        _layerIndex = layerIndex;
        _normalSlots = normalSlots ?? new List<SlotData>();
        _passageSlots = passageSlots ?? new List<SlotData>();
        _occupancy = new Dictionary<SlotData, EnemyController>();

        // pre-populate occupancy dict with all slots = free
        foreach (var s in _normalSlots)  _occupancy[s] = null;
        foreach (var s in _passageSlots) _occupancy[s] = null;

        BabelLogger.AC("S3-14", string.Concat(
            "LayerSlotManager initialized: layer=", layerIndex.ToString(),
            " normalSlots=", _normalSlots.Count.ToString(),
            " passageSlots=", _passageSlots.Count.ToString()));
    }

    /// <summary>
    /// Try to occupy the nearest free normal slot from fromPos.
    /// Returns true and sets slotPos if successful.
    /// </summary>
    public bool TryOccupyNormalSlot(EnemyController enemy, Vector2 fromPos, out Vector2 slotPos)
        => TryOccupyFrom(_normalSlots, enemy, fromPos, out slotPos);

    /// <summary>
    /// Find the nearest unbuilt passage slot. Passage slots are NOT exclusively occupied —
    /// multiple enemies may pass through the same slot. Returns false when all are built.
    /// </summary>
    public bool TryGetNearestUnbuiltPassageSlot(Vector2 fromPos, out Vector2 slotPos)
    {
        SlotData best = null;
        float bestDist = float.MaxValue;
        foreach (var slot in _passageSlots)
        {
            if (_builtSlots.Contains(slot)) continue; // already built
            float dist = Vector2.SqrMagnitude(slot.position - fromPos);
            if (dist < bestDist) { bestDist = dist; best = slot; }
        }
        if (best == null) { slotPos = Vector2.zero; return false; }
        slotPos = best.position;
        return true;
    }

    /// <summary>
    /// Release the slot held by this enemy (called on death or tower entry).
    /// </summary>
    public void ReleaseSlot(EnemyController enemy)
    {
        // find any slot occupied by this enemy and free it
        var keys = new List<SlotData>(_occupancy.Keys); // avoid modifying during iteration
        foreach (var slot in keys)
        {
            if (_occupancy[slot] == enemy)
            {
                _occupancy[slot] = null;
                _reverseOccupancy.Remove(enemy);
                return;
            }
        }
    }

    /// <summary>Returns true when every passage slot is occupied.</summary>
    public bool ArePassageSlotsFull()
    {
        if (_passageSlots.Count == 0) return false;
        foreach (var s in _passageSlots)
        {
            if (_occupancy[s] == null) return false;
        }
        return true;
    }

    /// <summary>Returns true when every normal slot is occupied.</summary>
    public bool AreNormalSlotsFull()
    {
        if (_normalSlots.Count == 0) return true; // no normal slots → treat as full so enemy moves to passage
        foreach (var s in _normalSlots)
        {
            if (_occupancy[s] == null) return false;
        }
        return true;
    }

    /// <summary>Reset all slots to free (called on game restart).</summary>
    public void Reset()
    {
        var keys = new List<SlotData>(_occupancy.Keys);
        foreach (var k in keys) _occupancy[k] = null;
        _builtSlots.Clear();
        _reverseOccupancy.Clear();
    }

    /// <summary>Returns the world X of the passage slot nearest to fromPos, or null if no passage slots.</summary>
    public float? GetNearestPassageX(Vector2 fromPos)
    {
        if (_passageSlots.Count == 0) return null;
        SlotData best = null;
        float bestDist = float.MaxValue;
        foreach (var slot in _passageSlots)
        {
            float dist = Mathf.Abs(slot.position.x - fromPos.x);
            if (dist < bestDist) { bestDist = dist; best = slot; }
        }
        return best?.position.x;
    }

    /// <summary>Returns the SlotData currently occupied by this enemy, or null.</summary>
    public SlotData GetSlotForOccupant(EnemyController enemy)
    {
        _reverseOccupancy.TryGetValue(enemy, out SlotData slot);
        return slot;
    }

    /// <summary>Mark a slot as built (enemy arrived and "solidified" into a block).</summary>
    public void MarkBuilt(SlotData slot)
    {
        if (slot != null) _builtSlots.Add(slot);
    }

    /// <summary>
    /// Mark a passage slot as built by position (used when enemy passes through without occupying).
    /// </summary>
    /// <summary>
    /// Mark passage slot at slotPos as built.
    /// Returns true only if it was NOT already built (first time).
    /// Returns false if already built or not found.
    /// </summary>
    public bool TryMarkPassageBuiltAt(Vector2 slotPos)
    {
        foreach (var slot in _passageSlots)
        {
            if (Vector2.SqrMagnitude(slot.position - slotPos) < 0.04f)
            {
                if (_builtSlots.Contains(slot)) return false; // already built
                _builtSlots.Add(slot);
                return true; // first time built
            }
        }
        return false;
    }

    /// <summary>Returns true when every NORMAL slot in this layer is built.
    /// Passage slots are auto-revealed and do not need to be occupied.</summary>
    public bool AreAllNormalSlotsBuilt()
    {
        if (_normalSlots.Count == 0) return true;
        int builtNormal = 0;
        foreach (var s in _normalSlots)
            if (_builtSlots.Contains(s)) builtNormal++;
        return builtNormal >= _normalSlots.Count;
    }

    /// <summary>Returns true when every slot (normal + passage) in this layer is built.</summary>
    public bool AreAllSlotsBuilt()
    {
        int total = _normalSlots.Count + _passageSlots.Count;
        if (total == 0) return false;
        return _builtSlots.Count >= total;
    }

    public int BuiltNormalCount
    {
        get
        {
            int n = 0;
            foreach (var s in _normalSlots)
                if (_builtSlots.Contains(s)) n++;
            return n;
        }
    }

    /// <summary>Returns the list of passage slots for this layer (read-only).</summary>
    public IReadOnlyList<SlotData> PassageSlots => _passageSlots;

    // ── Private helpers ───────────────────────────────────────────────────────

    private bool TryOccupyFrom(List<SlotData> slots, EnemyController enemy, Vector2 fromPos, out Vector2 slotPos)
    {
        SlotData best = null;
        float bestDist = float.MaxValue;

        foreach (var slot in slots)
        {
            if (_occupancy[slot] != null) continue; // already occupied
            if (_builtSlots.Contains(slot)) continue; // already built — slot is permanently filled
            float dist = Vector2.SqrMagnitude(slot.position - fromPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = slot;
            }
        }

        if (best == null)
        {
            slotPos = Vector2.zero;
            return false;
        }

        _occupancy[best] = enemy;
        _reverseOccupancy[enemy] = best;
        slotPos = best.position;
        return true;
    }
}
