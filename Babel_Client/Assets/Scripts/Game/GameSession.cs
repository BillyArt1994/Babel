using System;
using Babel.Bootstrap;
using Babel.Gameplay.RunFlow;
using Babel.Unity.Infrastructure.Time;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel
{
    public enum GameEndReason
    {
        None,
        Victory,
        Defeat
    }

    public readonly struct GameSessionResult
    {
        public static readonly GameSessionResult None = new GameSessionResult(GameEndReason.None, 0, 900f);

        public GameSessionResult(GameEndReason reason, int killCount, float remainingTime)
        {
            Reason = reason;
            KillCount = killCount;
            RemainingTime = remainingTime;
        }

        public readonly GameEndReason Reason;
        public readonly int KillCount;
        public readonly float RemainingTime;
    }

    /// <summary>
    /// Compatibility facade for legacy callers. RunContext is authoritative whenever a
    /// LegacyRunBridge is present; this class only mirrors outcomes for old UI and tests.
    /// </summary>
    public static class GameSession
    {
        private const float TOTAL_DURATION = 900f;
        public const string GAME_SCENE_NAME = "GameScene";
        public const string MAIN_MENU_SCENE_NAME = "MainMenuScene";

        private static GameSessionResult _result = GameSessionResult.None;
        private static float _remainingTime = TOTAL_DURATION;
        private static GameEndReason _requestedEndReason = GameEndReason.None;
        private static bool _sceneLoadingEnabled = true;
        private static string _lastRequestedSceneNameForTests;

        public static event Action<GameSessionResult> OnGameEnded;
        public static event Action<GameSessionResult> OnVictory;
        public static event Action<GameSessionResult> OnDefeat;

        public static GameSessionResult Result => _result;
        public static GameEndReason EndReason => _result.Reason;
        public static bool IsGameEnded => EndReason != GameEndReason.None;
        public static float RemainingTime => _remainingTime;
        public static float ElapsedTime => TOTAL_DURATION - _remainingTime;

        public static bool IsPlaying
        {
            get
            {
                if (LegacyRunBridge.TryGetRunPhase(out RunPhase phase)) return phase == RunPhase.Playing;
                return !IsGameEnded && PresentationTimeScaleAdapter.IsSimulationPresentationRunning;
            }
        }

        public static string LastRequestedSceneNameForTests => _lastRequestedSceneNameForTests;

        public static void TickCountdown(float deltaTime)
        {
            if (LegacyRunBridge.IsAvailable || IsGameEnded || deltaTime <= 0f) return;

            _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
            if (_remainingTime <= 0f) EndGame(GameEndReason.Victory);
        }

        public static bool EndGame(GameEndReason reason)
        {
            if (reason == GameEndReason.None)
            {
                Debug.LogWarning("[Babel][GameSession] Ignore EndGame(None)");
                return false;
            }

            if (IsGameEnded || _requestedEndReason != GameEndReason.None) return false;

            if (LegacyRunBridge.TryResolveOutcome(reason))
            {
                _requestedEndReason = reason;
                return true;
            }

            return CompleteOutcome(reason, StatsTracker.KillCount, _remainingTime, true);
        }

        internal static void ApplyAuthoritativeTime(double remainingSeconds)
        {
            if (IsGameEnded) return;
            _remainingTime = Mathf.Max(0f, (float)remainingSeconds);
        }

        internal static bool ApplyAuthoritativeOutcome(GameEndReason reason, double remainingSeconds)
        {
            if (reason == GameEndReason.None || IsGameEnded) return false;
            return CompleteOutcome(reason, StatsTracker.KillCount, (float)Math.Max(0d, remainingSeconds), false);
        }

        public static void ResetSession()
        {
            _result = GameSessionResult.None;
            _requestedEndReason = GameEndReason.None;
            _remainingTime = TOTAL_DURATION;
            StatsTracker.Reset();
            if (!LegacyRunBridge.IsAvailable) PresentationTimeScaleAdapter.ResetLegacy();
        }

        public static void RestartGame()
        {
            if (LegacyRunBridge.TryRequestExit(RunExitRequest.Restart)) return;
            ResetSession();
            LoadScene(GAME_SCENE_NAME);
        }

        public static void StartGame()
        {
            ResetSession();
            LoadScene(GAME_SCENE_NAME);
        }

        public static void ReturnToMainMenu()
        {
            if (LegacyRunBridge.TryRequestExit(RunExitRequest.ReturnToMenu)) return;
            ResetSession();
            LoadScene(MAIN_MENU_SCENE_NAME);
        }

        public static void SetSceneLoadingEnabledForTests(bool enabled)
        {
            _sceneLoadingEnabled = enabled;
        }

        private static bool CompleteOutcome(GameEndReason reason, int killCount, float remainingTime, bool freezeLegacyPresentation)
        {
            if (IsGameEnded) return false;

            _requestedEndReason = reason;
            _remainingTime = Mathf.Max(0f, remainingTime);
            _result = new GameSessionResult(reason, killCount, _remainingTime);
            if (freezeLegacyPresentation) PresentationTimeScaleAdapter.FreezeLegacy();
            OnGameEnded?.Invoke(_result);

            if (reason == GameEndReason.Victory) OnVictory?.Invoke(_result);
            else OnDefeat?.Invoke(_result);
            return true;
        }

        private static void LoadScene(string sceneName)
        {
            _lastRequestedSceneNameForTests = sceneName;
            if (!_sceneLoadingEnabled) return;

            ProjectRoot root = ProjectRoot.Active;
            if (root != null && root.SceneFlow != null) root.SceneFlow.Load(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _result = GameSessionResult.None;
            _requestedEndReason = GameEndReason.None;
            _remainingTime = TOTAL_DURATION;
            _sceneLoadingEnabled = true;
            _lastRequestedSceneNameForTests = null;
            OnGameEnded = null;
            OnVictory = null;
            OnDefeat = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetSessionAfterSceneLoad()
        {
            ResetSession();
        }
    }
}
