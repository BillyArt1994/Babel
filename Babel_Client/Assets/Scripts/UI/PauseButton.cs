using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause button — fires InputEvents.OnPausePressed on click.
/// </summary>
[RequireComponent(typeof(Button))]
public class PauseButton : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(InputEvents.RaisePausePressed);
    }
}
