using UnityEngine;

[DefaultExecutionOrder(-90)]
public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }

    [SerializeField] private bool _isHoldingMouse;
    [SerializeField] private float _holdDuration;
    [SerializeField] private Vector2 _holdStartWorldPos;
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _wasPlaying;

    public bool IsHoldingMouse => _isHoldingMouse;
    public float HoldDuration => _holdDuration;
    public Vector2 HoldStartWorldPos => _holdStartWorldPos;
    public Camera CachedCamera => _camera;
    public bool WasPlaying => _wasPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GameLoopManager.Instance != null)
        {
            _wasPlaying = GameLoopManager.Instance.IsPlaying();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            InputEvents.RaisePausePressed();
        }

        GameLoopManager gameLoopManager = GameLoopManager.Instance;
        bool isPlaying = gameLoopManager != null && gameLoopManager.IsPlaying();

        if (_wasPlaying && !isPlaying)
        {
            ForceReleaseMouse();
        }

        _wasPlaying = isPlaying;

        if (!isPlaying)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!TryGetMouseWorldPos(out Vector2 worldPos))
            {
                return;
            }

            _isHoldingMouse = true;
            _holdDuration = 0f;
            _holdStartWorldPos = worldPos;

            InputEvents.RaiseMouseDown(worldPos);
        }

        if (Input.GetMouseButton(0) && _isHoldingMouse)
        {
            if (!TryGetMouseWorldPos(out Vector2 worldPos))
            {
                return;
            }

            _holdDuration += Time.deltaTime;
            _holdStartWorldPos = worldPos;

            InputEvents.RaiseMouseHeld(worldPos, _holdDuration);
        }

        if (Input.GetMouseButtonUp(0) && _isHoldingMouse)
        {
            Vector2 worldPos = _holdStartWorldPos;

            if (TryGetMouseWorldPos(out Vector2 currentWorldPos))
            {
                worldPos = currentWorldPos;
            }

            InputEvents.RaiseMouseUp(worldPos, _holdDuration);
            ResetMouseState();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ForceReleaseMouse();
        }
    }

    private void OnDisable()
    {
        ForceReleaseMouse();
        ResetMouseState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private Vector2 GetMouseWorldPos()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            return _holdStartWorldPos;
        }

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = 0f;

        Vector3 worldPos = _camera.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;

        return worldPos;
    }

    private bool TryGetMouseWorldPos(out Vector2 worldPos)
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            worldPos = default;
            return false;
        }

        worldPos = GetMouseWorldPos();
        return true;
    }

    private void ForceReleaseMouse()
    {
        if (!_isHoldingMouse)
        {
            return;
        }

        Vector2 worldPos = _holdStartWorldPos;

        if (TryGetMouseWorldPos(out Vector2 currentWorldPos))
        {
            worldPos = currentWorldPos;
            _holdStartWorldPos = currentWorldPos;
        }

        InputEvents.RaiseMouseUp(worldPos, _holdDuration);
        ResetMouseState();
    }

    private void ResetMouseState()
    {
        _isHoldingMouse = false;
        _holdDuration = 0f;
        _holdStartWorldPos = Vector2.zero;
    }
}
