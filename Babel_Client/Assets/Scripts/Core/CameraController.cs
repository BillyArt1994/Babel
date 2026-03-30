using UnityEngine;

/// <summary>
/// Implements camera behavior from design/gdd/摄像机系统.md.
/// Smoothly tracks the active tower layer height on Y axis.
/// X and Z positions are fixed; only Y moves.
/// </summary>
public class CameraController : MonoBehaviour
{
    private const float CAMERA_LERP_SPEED = 2.0f;
    private const float SNAP_THRESHOLD = 0.01f;

    // How many world units of bottom margin to keep below the active layer.
    // Camera Y = activeLayerY + orthographicSize - GROUND_MARGIN
    // keeps ground/tower near the bottom of the screen.
    [SerializeField] private float _groundMargin = 1.2f;

    [SerializeField] private TowerConstructionSystem _towerSystem;

    private Camera _camera;
    private bool _isActive;
    private float _targetY;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        GameEvents.OnGameStart += OnGameStart;
        GameEvents.OnGamePaused += OnGamePaused;
        GameEvents.OnGameResumed += OnGameResumed;
        GameEvents.OnVictory += OnGameStopped;
        GameEvents.OnDefeat += OnGameStopped;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStart -= OnGameStart;
        GameEvents.OnGamePaused -= OnGamePaused;
        GameEvents.OnGameResumed -= OnGameResumed;
        GameEvents.OnVictory -= OnGameStopped;
        GameEvents.OnDefeat -= OnGameStopped;
    }

    private void Update()
    {
        if (!_isActive) return;

        _targetY = GetTargetCameraY();

        Vector3 pos = transform.position;
        float gap = _targetY - pos.y;

        if (Mathf.Abs(gap) < SNAP_THRESHOLD)
            pos.y = _targetY;
        else
            pos.y = Mathf.Lerp(pos.y, _targetY, CAMERA_LERP_SPEED * Time.deltaTime);

        transform.position = pos;
    }

    private void OnGameStart()
    {
        Vector3 pos = transform.position;
        pos.y = GetTargetCameraY();
        transform.position = pos;
        _isActive = true;
    }

    private void OnGamePaused() => _isActive = false;
    private void OnGameResumed() => _isActive = true;
    private void OnGameStopped() => _isActive = false;

    // Place the active layer near the bottom of the screen:
    // cameraY = layerY + (halfHeight - groundMargin)
    private float GetTargetCameraY()
    {
        float layerY = _towerSystem != null ? _towerSystem.GetActiveLayerWorldY() : 0f;
        float halfHeight = _camera != null ? _camera.orthographicSize : 6f;
        return layerY + halfHeight - _groundMargin;
    }
}
