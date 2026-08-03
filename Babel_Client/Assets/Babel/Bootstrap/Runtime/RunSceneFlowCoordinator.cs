using Babel.Gameplay.RunFlow;
using UnityEngine;

namespace Babel.Bootstrap
{
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunRoot))]
    public sealed class RunSceneFlowCoordinator : MonoBehaviour
    {
        [SerializeField] private RunRoot _runRoot;
        private bool _handled;

        private void Awake()
        {
            if (_runRoot == null) _runRoot = GetComponent<RunRoot>();
        }

        private void OnEnable()
        {
            if (_runRoot == null) _runRoot = GetComponent<RunRoot>();
            if (_runRoot != null && _runRoot.Driver != null)
                _runRoot.Driver.FrameAdvanced += HandleFrameAdvanced;
        }

        private void OnDisable()
        {
            if (_runRoot != null && _runRoot.Driver != null)
                _runRoot.Driver.FrameAdvanced -= HandleFrameAdvanced;
        }

        private void HandleFrameAdvanced(RunFrameResult result)
        {
            if (_handled || result.ExitRequest == RunExitRequest.None) return;
            _handled = true;

            ProjectRoot projectRoot = ProjectRoot.Active;
            if (projectRoot == null || projectRoot.SceneFlow == null)
            {
                Debug.LogError("[Babel][SceneFlow] ProjectRoot is unavailable; run exit cannot be routed.", this);
                _handled = false;
                return;
            }

            if (result.ExitRequest == RunExitRequest.Restart)
                projectRoot.SceneFlow.RestartGame();
            else
                projectRoot.SceneFlow.LoadMenu();
        }

        private void OnValidate()
        {
            if (_runRoot == null) _runRoot = GetComponent<RunRoot>();
        }
    }
}
