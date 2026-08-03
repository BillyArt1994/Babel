using Babel.Bootstrap;
using Babel.Gameplay.RunFlow;
using Babel.Unity.Infrastructure.Time;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// Temporary WP1 adapter. It keeps legacy views/gameplay alive while RunFlow owns time,
    /// pause, speed, upgrade freeze, outcomes, and scene exits. Delete during WP4.
    /// </summary>
    [DefaultExecutionOrder(-750)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunRoot))]
    public sealed class LegacyRunBridge : MonoBehaviour, IUpgradeSelectionHandler
    {
        private static LegacyRunBridge _active;

        [SerializeField] private RunRoot _runRoot;
        private UpgradeSystem _pendingUpgradeSystem;

        public static bool IsAvailable =>
            _active != null &&
            _active._runRoot != null &&
            _active._runRoot.Driver != null &&
            _active._runRoot.Driver.IsInitialized;

        private void Awake()
        {
            if (_runRoot == null) _runRoot = GetComponent<RunRoot>();
            if (_active != null && _active != this)
            {
                Debug.LogError("[Babel][LegacyBridge] More than one bridge is active.", this);
                enabled = false;
                return;
            }
            _active = this;
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
            if (_active == this) _active = null;
            _pendingUpgradeSystem = null;
        }

        public static bool TryGetReadModel(out RunReadModel model)
        {
            if (IsAvailable)
            {
                model = _active._runRoot.Context.ReadModel.Current;
                return true;
            }

            model = default;
            return false;
        }

        public static bool TryGetRunPhase(out RunPhase phase)
        {
            if (IsAvailable)
            {
                phase = _active._runRoot.Context.Phase;
                return true;
            }

            phase = RunPhase.Booting;
            return false;
        }

        public static bool TryTogglePause()
        {
            if (!IsAvailable) return false;
            _active._runRoot.Driver.Enqueue(RunControlCommand.TogglePause());
            return true;
        }

        public static bool TrySetSpeed(RunSpeed speed)
        {
            if (!IsAvailable) return false;
            _active._runRoot.Driver.Enqueue(RunControlCommand.SetSpeed(speed));
            return true;
        }

        public static bool TryBeginUpgradeChoice(UpgradeSystem upgradeSystem)
        {
            if (!IsAvailable || upgradeSystem == null) return false;
            _active._pendingUpgradeSystem = upgradeSystem;
            _active._runRoot.Driver.Enqueue(RunControlCommand.BeginUpgradeChoice());
            return true;
        }

        public static bool TryRequestUpgradeSelection(UpgradeSystem upgradeSystem, int optionIndex)
        {
            if (!IsAvailable || upgradeSystem == null || _active._pendingUpgradeSystem != upgradeSystem) return false;
            _active._runRoot.Driver.Enqueue(RunControlCommand.SelectUpgrade(optionIndex));
            return true;
        }

        public static bool TryResolveOutcome(GameEndReason reason)
        {
            if (!IsAvailable || reason == GameEndReason.None) return false;
            RunOutcome outcome = reason == GameEndReason.Victory ? RunOutcome.Victory : RunOutcome.Defeat;
            _active._runRoot.Driver.Enqueue(RunControlCommand.ResolveOutcome(outcome));
            return true;
        }

        public static bool TryRequestExit(RunExitRequest request)
        {
            if (!IsAvailable || request == RunExitRequest.None) return false;
            _active._runRoot.Driver.Enqueue(
                request == RunExitRequest.Restart
                    ? RunControlCommand.RequestRestart()
                    : RunControlCommand.RequestReturnToMenu());
            return true;
        }

        public bool TrySelectUpgrade(int optionIndex, RunContext context)
        {
            UpgradeSystem system = _pendingUpgradeSystem;
            if (system == null || !system.ApplySelectedOptionFromRun(optionIndex)) return false;
            _pendingUpgradeSystem = null;
            return true;
        }

        private void HandleFrameAdvanced(RunFrameResult result)
        {
            if (_runRoot == null || _runRoot.Context == null) return;
            RunReadModel model = _runRoot.Context.ReadModel.Current;
            GameSession.ApplyAuthoritativeTime(model.RemainingSeconds);

            if (model.Phase == RunPhase.Won)
                GameSession.ApplyAuthoritativeOutcome(GameEndReason.Victory, model.RemainingSeconds);
            else if (model.Phase == RunPhase.Lost)
                GameSession.ApplyAuthoritativeOutcome(GameEndReason.Defeat, model.RemainingSeconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _active = null;
        }
    }
}
