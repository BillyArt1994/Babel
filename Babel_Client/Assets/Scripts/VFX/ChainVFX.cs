using UnityEngine;

/// <summary>
/// Code-driven chain lightning VFX using LineRenderer.
/// Implements visual feedback for Chain attacks (神雷/Lightning Chain)
/// per Sprint-2 task S2-07 (design/gdd/点击攻击系统.md).
///
/// Usage: Call Initialize() with an array of hit positions.
/// Draws lines between consecutive hit positions, then fades out
/// and auto-destroys after the specified lifetime.
/// </summary>
public class ChainVFX : MonoBehaviour
{
    private const float DEFAULT_LINE_WIDTH = 0.08f;

    private LineRenderer _lineRenderer;
    private float _lifetime;
    private float _elapsed;
    private Color _startColor;
    private Color _endColor;

    /// <summary>
    /// Configure and start the chain line effect.
    /// </summary>
    /// <param name="positions">World-space positions of chain targets, in order.</param>
    /// <param name="lifetime">Duration in seconds before auto-destroy.</param>
    /// <param name="color">Base color of the chain lines.</param>
    /// <param name="lineWidth">Width of the LineRenderer stroke.</param>
    public void Initialize(
        Vector2[] positions,
        float lifetime,
        Color color,
        float lineWidth = DEFAULT_LINE_WIDTH)
    {
        if (positions == null || positions.Length < 2)
        {
            Destroy(gameObject);
            return;
        }

        _lifetime = Mathf.Max(0.01f, lifetime);
        _elapsed = 0f;
        _startColor = color;
        _endColor = new Color(color.r, color.g, color.b, 0f);

        SetupLineRenderer(positions, lineWidth);
    }

    private void SetupLineRenderer(Vector2[] positions, float lineWidth)
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = false;
        _lineRenderer.positionCount = positions.Length;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.numCapVertices = 2;
        _lineRenderer.numCornerVertices = 0;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = _startColor;
        _lineRenderer.endColor = _startColor;
        _lineRenderer.sortingOrder = 100;

        for (int i = 0; i < positions.Length; i++)
        {
            _lineRenderer.SetPosition(i, new Vector3(positions[i].x, positions[i].y, 0f));
        }
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _lifetime);

        if (_lineRenderer != null)
        {
            Color currentColor = Color.Lerp(_startColor, _endColor, t);
            _lineRenderer.startColor = currentColor;
            _lineRenderer.endColor = currentColor;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
