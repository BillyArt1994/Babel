using UnityEngine;

/// <summary>
/// Listens to CombatEvents.OnAttackExecuted and spawns code-driven
/// placeholder VFX for each attack type.
/// Implements Sprint-2 task S2-07 (design/gdd/点击攻击系统.md).
///
/// Attack type to VFX mapping:
///   Single (神罚之指) -> White expanding circle, fast fade (~0.3s)
///   AOE    (陨石)     -> Orange circle showing damage radius, short display
///   Chain  (神雷)     -> Yellow lines connecting chain targets
///
/// All configurable values are exposed via [SerializeField] so designers
/// can tune without code changes.
/// </summary>
public class AttackVFXManager : MonoBehaviour
{
    public static AttackVFXManager Instance { get; private set; }

    [Header("Single (Divine Finger) VFX")]
    [Tooltip("Starting radius of the expanding white circle.")]
    [SerializeField] private float _singleStartRadius = 0.1f;
    [Tooltip("Ending radius of the expanding white circle.")]
    [SerializeField] private float _singleEndRadius = 1.0f;
    [Tooltip("Lifetime in seconds for the single-target VFX.")]
    [SerializeField] private float _singleLifetime = 0.3f;
    [Tooltip("Color of the single-target expanding circle.")]
    [SerializeField] private Color _singleColor = Color.white;
    [Tooltip("Line width for the single-target circle.")]
    [SerializeField] private float _singleLineWidth = 0.08f;

    [Header("AOE (Meteor) VFX")]
    [Tooltip("Lifetime in seconds for the AOE radius indicator.")]
    [SerializeField] private float _aoeLifetime = 0.5f;
    [Tooltip("Color of the AOE radius circle.")]
    [SerializeField] private Color _aoeColor = new Color(1f, 0.6f, 0f, 1f); // Orange
    [Tooltip("Line width for the AOE circle.")]
    [SerializeField] private float _aoeLineWidth = 0.1f;

    [Header("Chain (Divine Thunder) VFX")]
    [Tooltip("Lifetime in seconds for the chain lightning lines.")]
    [SerializeField] private float _chainLifetime = 0.4f;
    [Tooltip("Color of the chain lightning lines.")]
    [SerializeField] private Color _chainColor = new Color(1f, 1f, 0f, 1f); // Yellow
    [Tooltip("Line width for the chain lightning.")]
    [SerializeField] private float _chainLineWidth = 0.1f;

    [Header("Passive Attack VFX")]
    [Tooltip("Whether to show VFX for passive/auto attacks as well.")]
    [SerializeField] private bool _showPassiveVFX = true;

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
        CombatEvents.OnAttackExecuted += OnAttackExecuted;
    }

    private void OnDisable()
    {
        CombatEvents.OnAttackExecuted -= OnAttackExecuted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnAttackExecuted(AttackResult result)
    {
        if (result.hits == null || result.hits.Count == 0)
            return;

        if (!_showPassiveVFX && result.request.isPassiveAttack)
            return;

        switch (result.request.attackType)
        {
            case AttackType.Single:
                SpawnSingleVFX(result);
                break;

            case AttackType.AOE:
                SpawnAoeVFX(result);
                break;

            case AttackType.Chain:
                SpawnChainVFX(result);
                break;

            // Pierce attacks do not have a dedicated VFX in S2-07 spec.
            // They can be added in a future sprint if needed.
        }
    }

    private void SpawnSingleVFX(AttackResult result)
    {
        // Spawn an expanding white circle at the first hit position
        Vector2 hitPos = result.hits[0].hitPosition;

        GameObject vfxObj = new GameObject("VFX_Single");
        CircleVFX circle = vfxObj.AddComponent<CircleVFX>();
        circle.Initialize(
            hitPos,
            _singleStartRadius,
            _singleEndRadius,
            _singleLifetime,
            _singleColor,
            _singleLineWidth
        );
    }

    private void SpawnAoeVFX(AttackResult result)
    {
        // Spawn an orange circle at the attack origin showing the AOE radius.
        // The circle starts at full radius (to show damage zone) and fades out.
        float radius = result.request.radius;
        if (radius <= 0f)
            radius = 1f; // Fallback if radius is somehow zero

        Vector2 attackPos = result.request.worldPos;

        GameObject vfxObj = new GameObject("VFX_AOE");
        CircleVFX circle = vfxObj.AddComponent<CircleVFX>();
        circle.Initialize(
            attackPos,
            radius,
            radius, // Same start and end radius -- the circle does not expand
            _aoeLifetime,
            _aoeColor,
            _aoeLineWidth,
            48 // More segments for larger circles
        );
    }

    private void SpawnChainVFX(AttackResult result)
    {
        if (result.hits.Count < 2)
        {
            // Only one target hit; fall back to a single-style VFX
            SpawnSingleVFX(result);
            return;
        }

        // Build the chain path from the attack origin through all hit positions
        // The first position is the click origin, followed by hit positions in order
        Vector2[] positions = new Vector2[result.hits.Count + 1];
        positions[0] = result.request.worldPos;
        for (int i = 0; i < result.hits.Count; i++)
        {
            positions[i + 1] = result.hits[i].hitPosition;
        }

        GameObject vfxObj = new GameObject("VFX_Chain");
        ChainVFX chain = vfxObj.AddComponent<ChainVFX>();
        chain.Initialize(
            positions,
            _chainLifetime,
            _chainColor,
            _chainLineWidth
        );
    }
}
