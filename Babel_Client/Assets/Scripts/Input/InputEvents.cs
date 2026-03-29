using System;
using UnityEngine;

public static class InputEvents
{
    public static event Action<Vector2> OnMouseDown;
    public static event Action<Vector2, float> OnMouseHeld;
    public static event Action<Vector2, float> OnMouseUp;
    public static event Action OnPausePressed;

    public static void RaiseMouseDown(Vector2 worldPos) => OnMouseDown?.Invoke(worldPos);
    public static void RaiseMouseHeld(Vector2 worldPos, float holdDuration) => OnMouseHeld?.Invoke(worldPos, holdDuration);
    public static void RaiseMouseUp(Vector2 worldPos, float holdDuration) => OnMouseUp?.Invoke(worldPos, holdDuration);
    public static void RaisePausePressed() => OnPausePressed?.Invoke();
}
