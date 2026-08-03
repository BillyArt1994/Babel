using UnityEngine;

namespace Babel.Unity.Presentation.Babel
{
    public enum BabelPointVisualState
    {
        Hidden = 0,
        Building = 1,
        Completed = 2
    }

    /// <summary>Scene-authored identity and presentation adapter for one Babel build point.</summary>
    [DisallowMultipleComponent]
    public sealed class BabelPointView : MonoBehaviour
    {
        [SerializeField] private string _stableId;
        [SerializeField] private bool _isGateway;
        [SerializeField] private SpriteRenderer _renderer;

        public string StableId => _stableId;
        public bool IsGateway => _isGateway;

        public void Apply(BabelPointVisualState state)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            bool visible = state != BabelPointVisualState.Hidden;
            if (_renderer != null)
            {
                _renderer.enabled = visible;
                _renderer.color = state == BabelPointVisualState.Completed ? Color.red : Color.white;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(string stableId, bool isGateway)
        {
            _stableId = stableId;
            _isGateway = isGateway;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }
#endif

        private void OnValidate()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }
    }
}
