using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.Bootstrap
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProjectRoot))]
    public sealed class BootEntry : MonoBehaviour
    {
        [SerializeField] private ProjectRoot _projectRoot;
        [SerializeField] private bool _loadMenuOnStart = true;

        private void Awake()
        {
            if (_projectRoot == null) _projectRoot = GetComponent<ProjectRoot>();
        }

        private void Start()
        {
            if (!_loadMenuOnStart) return;

            if (_projectRoot == null || !_projectRoot.IsPrimary || _projectRoot.SceneFlow == null)
            {
                Debug.LogError("[Babel][Boot] ProjectRoot is unavailable; menu loading was aborted.", this);
                return;
            }

            if (SceneManager.GetActiveScene().name != Babel.Unity.Infrastructure.SceneFlow.SceneNames.Boot)
            {
                Debug.LogWarning("[Babel][Boot] BootEntry is outside BootScene; automatic routing was skipped.", this);
                return;
            }

            _projectRoot.SceneFlow.LoadMenu();
        }

        private void OnValidate()
        {
            if (_projectRoot == null) _projectRoot = GetComponent<ProjectRoot>();
        }
    }
}
