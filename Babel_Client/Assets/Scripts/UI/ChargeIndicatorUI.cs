using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Charge-ring UI overlay.
/// Attach to a GameObject under a ScreenSpaceOverlay Canvas.
///
/// Responsibilities:
///   - Show a radial fill ring that tracks charge progress (white → gold at full charge)
///   - Show a semi-transparent AOE range preview circle scaled to current charge ratio
///   - Follow the cursor world position converted to screen space each frame
/// </summary>
public class ChargeIndicatorUI : MonoBehaviour
{
    private static readonly Color GoldColor = new Color(1f, 0.85f, 0.2f);

    /// <summary>Filled Radial360 image used as the charge-progress ring.</summary>
    [SerializeField] private Image _ringFill;

    /// <summary>Semi-transparent circle image used as the AOE range preview.</summary>
    [SerializeField] private Image _rangePreview;

    /// <summary>Main camera — required to convert world position to screen position.</summary>
    [SerializeField] private Camera _camera;

    private RectTransform _rectTransform;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        SkillEvents.OnChargeStarted += OnChargeStarted;
        SkillEvents.OnChargeUpdated += OnChargeUpdated;
        InputEvents.OnMouseUp       += OnMouseUp;
    }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        SkillEvents.OnChargeStarted -= OnChargeStarted;
        SkillEvents.OnChargeUpdated -= OnChargeUpdated;
        InputEvents.OnMouseUp       -= OnMouseUp;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnChargeStarted(Vector2 worldPos)
    {
        gameObject.SetActive(true);
        SetScreenPosition(worldPos);
    }

    private void OnChargeUpdated(Vector2 worldPos, float ratio)
    {
        // Update ring fill and tint
        if (_ringFill != null)
        {
            _ringFill.fillAmount = ratio;
            _ringFill.color = ratio >= 1f ? GoldColor : Color.white;
        }

        // Update AOE range preview size
        UpdateRangePreview(ratio);

        // Keep indicator locked to cursor
        SetScreenPosition(worldPos);
    }

    private void OnMouseUp(Vector2 worldPos, float holdDuration)
    {
        gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a world-space position to screen space and moves this RectTransform there.
    /// Assumes the Canvas render mode is Screen Space - Overlay.
    /// </summary>
    private void SetScreenPosition(Vector2 worldPos)
    {
        if (_camera == null || _rectTransform == null) return;

        Vector3 screenPos = _camera.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        _rectTransform.position = screenPos;
    }

    /// <summary>
    /// Resizes the range-preview image to match the current charge ratio.
    /// At ratio 0 the preview shows at 50 % of base radius; at ratio 1 it shows the full radius.
    /// If the active skill has no AOE radius (single / chain) the preview is hidden.
    /// </summary>
    private void UpdateRangePreview(float ratio)
    {
        if (_rangePreview == null) return;

        SkillData skill = SkillSystem.Instance != null
            ? SkillSystem.Instance.GetActiveClickForm()
            : null;

        float baseRadius = skill != null ? skill.AoeRadius : 0f;

        if (baseRadius <= 0f)
        {
            _rangePreview.gameObject.SetActive(false);
            return;
        }

        _rangePreview.gameObject.SetActive(true);

        float currentRadius = baseRadius * Mathf.Lerp(0.5f, 1f, ratio);

        // Convert world-unit radius to screen pixels.
        // WorldToScreenPoint gives us the screen-space position of the origin;
        // shifting by (radius, 0) world units and taking the difference gives pixel size.
        float pixelsPerUnit = GetPixelsPerUnit();
        float diameter = currentRadius * 2f * pixelsPerUnit;

        _rangePreview.rectTransform.sizeDelta = Vector2.one * diameter;
    }

    /// <summary>
    /// Returns how many screen pixels correspond to one Unity world unit at z = 0,
    /// using the main camera's current projection.
    /// </summary>
    private float GetPixelsPerUnit()
    {
        if (_camera == null) return 100f; // safe fallback

        // For an orthographic camera the conversion is straightforward:
        // the camera covers (orthographicSize * 2) world units vertically,
        // which maps to Screen.height pixels.
        if (_camera.orthographic)
        {
            return Screen.height / (_camera.orthographicSize * 2f);
        }

        // For a perspective camera, approximate using the depth of the world origin.
        float depth = Mathf.Abs(_camera.transform.position.z);
        if (depth <= 0f) depth = 10f;

        Vector3 worldOrigin  = _camera.ScreenToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 worldOffset  = _camera.ScreenToWorldPoint(new Vector3(1f, 0f, depth));
        float worldPerPixel  = Vector3.Distance(worldOrigin, worldOffset);
        return worldPerPixel > 0f ? 1f / worldPerPixel : 100f;
    }
}
