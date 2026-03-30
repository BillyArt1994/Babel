using UnityEngine;

/// <summary>
/// Code-driven expanding circle VFX using LineRenderer.
/// Implements visual feedback for Single and AOE attacks
/// per Sprint-2 task S2-07 (design/gdd/点击攻击系统.md).
///
/// Usage: Call Initialize() to spawn the effect at a world position.
/// The circle expands from startRadius to endRadius over its lifetime,
/// then the GameObject is destroyed automatically.
/// </summary>
public class CircleVFX : MonoBehaviour
{
    private const int DEFAULT_SEGMENT_COUNT = 32;
    private const float DEFAULT_LINE_WIDTH = 0.06f;

    private LineRenderer _lineRenderer;
    private float _lifetime;
    private float _elapsed;
    private float _startRadius;
    private float _endRadius;
    private Color _startColor;
    private Color _endColor;
    private int _segmentCount;

    /// <summary>
    /// Configure and start the circle effect.
    /// </summary>
    /// <param name="worldPos">Center position in world space.</param>
    /// <param name="startRadius">Radius at spawn time.</param>
    /// <param name="endRadius">Radius at end of lifetime.</param>
    /// <param name="lifetime">Duration in seconds before auto-destroy.</param>
    /// <param name="color">Base color of the circle.</param>
    /// <param name="lineWidth">Width of the LineRenderer stroke.</param>
    /// <param name="segmentCount">Number of line segments forming the circle.</param>
    public void Initialize(
        Vector2 worldPos,
        float startRadius,
        float endRadius,
        float lifetime,
        Color color,
        float lineWidth = DEFAULT_LINE_WIDTH,
        int segmentCount = DEFAULT_SEGMENT_COUNT)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        _startRadius = startRadius;
        _endRadius = endRadius;
        _lifetime = Mathf.Max(0.01f, lifetime);
        _elapsed = 0f;
        _startColor = color;
        _endColor = new Color(color.r, color.g, color.b, 0f);
        _segmentCount = Mathf.Max(8, segmentCount);

        SetupLineRenderer(lineWidth);
        UpdateCircle(0f);
    }

    private void SetupLineRenderer(float lineWidth)
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = _segmentCount;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.numCapVertices = 2;
        _lineRenderer.numCornerVertices = 2;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        // Use the default sprite material for unlit colored lines
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = _startColor;
        _lineRenderer.endColor = _startColor;
        _lineRenderer.sortingOrder = 100;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _lifetime);

        UpdateCircle(t);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateCircle(float t)
    {
        if (_lineRenderer == null) return;

        float currentRadius = Mathf.Lerp(_startRadius, _endRadius, t);
        Color currentColor = Color.Lerp(_startColor, _endColor, t);

        _lineRenderer.startColor = currentColor;
        _lineRenderer.endColor = currentColor;

        float angleStep = 360f / _segmentCount;
        for (int i = 0; i < _segmentCount; i++)
        {
            float angleRad = Mathf.Deg2Rad * (angleStep * i);
            float x = Mathf.Cos(angleRad) * currentRadius;
            float y = Mathf.Sin(angleRad) * currentRadius;
            _lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }
}
