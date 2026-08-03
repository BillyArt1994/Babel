using Babel.Unity.Infrastructure.Content;
using Babel.Unity.Infrastructure.SceneFlow;
using UnityEngine;

namespace Babel.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class ProjectRoot : MonoBehaviour
    {
        private static ProjectRoot _active;

        [SerializeField] private GameContentManifest _contentManifest;

        public static ProjectRoot Active => _active;
        public bool IsPrimary => _active == this;
        public GameContentManifest ContentManifest => _contentManifest;
        public SceneFlowService SceneFlow { get; private set; }

        private void Awake()
        {
            if (_active != null && _active != this)
            {
                Destroy(gameObject);
                return;
            }

            _active = this;
            DontDestroyOnLoad(gameObject);
            SceneFlow = new SceneFlowService();

            if (_contentManifest == null)
            {
                Debug.LogError("[Babel][ProjectRoot] GameContentManifest is not assigned.", this);
                return;
            }

            GameContentRegistry.Register(_contentManifest);
        }

        private void OnDestroy()
        {
            if (_active != this) return;
            GameContentRegistry.Unregister(_contentManifest);
            _active = null;
            SceneFlow = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _active = null;
        }
    }
}
