using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel
{
    /// <summary>
    /// 游戏会话结束原因。
    /// </summary>
    public enum GameEndReason
    {
        None,
        Victory,
        Defeat
    }

    /// <summary>
    /// 游戏结束时冻结下来的结算快照。
    /// </summary>
    public readonly struct GameSessionResult
    {
        /// <summary>
        /// 未结束状态的空结算。
        /// </summary>
        public static readonly GameSessionResult None = new GameSessionResult(GameEndReason.None, 0, 900f);

        /// <summary>
        /// 创建结算快照。
        /// </summary>
        /// <param name="reason">结束原因。</param>
        /// <param name="killCount">结束瞬间的击杀数。</param>
        /// <param name="remainingTime">结束瞬间的剩余时间。</param>
        public GameSessionResult(GameEndReason reason, int killCount, float remainingTime)
        {
            Reason = reason;
            KillCount = killCount;
            RemainingTime = remainingTime;
        }

        /// <summary>
        /// 结束原因。
        /// </summary>
        public readonly GameEndReason Reason;

        /// <summary>
        /// 结束瞬间的击杀数。
        /// </summary>
        public readonly int KillCount;

        /// <summary>
        /// 结束瞬间的剩余时间。
        /// </summary>
        public readonly float RemainingTime;
    }

    /// <summary>
    /// 统一管理本局游戏的生命周期、倒计时和结束事件。
    /// </summary>
    public static class GameSession
    {
        private const float TOTAL_DURATION = 900f;
        public const string GAME_SCENE_NAME = "GameScene";
        public const string MAIN_MENU_SCENE_NAME = "MainMenuScene";

        private static GameSessionResult _result = GameSessionResult.None;
        private static bool _sceneLoadingEnabled = true;
        private static string _lastRequestedSceneNameForTests;

        /// <summary>
        /// 游戏结束时触发一次。
        /// </summary>
        public static event Action<GameSessionResult> OnGameEnded;

        /// <summary>
        /// 胜利时触发一次。
        /// </summary>
        public static event Action<GameSessionResult> OnVictory;

        /// <summary>
        /// 失败时触发一次。
        /// </summary>
        public static event Action<GameSessionResult> OnDefeat;

        /// <summary>
        /// 当前结算快照。
        /// </summary>
        public static GameSessionResult Result => _result;

        /// <summary>
        /// 当前结束原因。
        /// </summary>
        public static GameEndReason EndReason => _result.Reason;

        /// <summary>
        /// 游戏是否已经胜利或失败。
        /// </summary>
        public static bool IsGameEnded => EndReason != GameEndReason.None;

        /// <summary>
        /// 游戏逻辑是否应继续推进。
        /// </summary>
        public static bool IsPlaying => !IsGameEnded && Time.timeScale > 0f;

        /// <summary>
        /// 最近一次请求加载的场景名，仅供 EditMode 测试断言场景路由。
        /// </summary>
        public static string LastRequestedSceneNameForTests => _lastRequestedSceneNameForTests;

        /// <summary>
        /// 推进倒计时。时间归零会触发胜利。
        /// </summary>
        /// <param name="deltaTime">本帧游戏时间。</param>
        public static void TickCountdown(float deltaTime)
        {
            if (IsGameEnded || deltaTime <= 0f)
            {
                return;
            }

            Global.CurrentTime.Value = Mathf.Max(0f, Global.CurrentTime.Value - deltaTime);
            if (Global.CurrentTime.Value <= 0f)
            {
                EndGame(GameEndReason.Victory);
            }
        }

        /// <summary>
        /// 结束本局游戏，只允许第一次调用生效。
        /// </summary>
        /// <param name="reason">结束原因。</param>
        /// <returns>本次调用是否成功结束游戏。</returns>
        public static bool EndGame(GameEndReason reason)
        {
            if (reason == GameEndReason.None)
            {
                Debug.LogWarning("[BABEL][GameSession] Ignore EndGame(None)");
                return false;
            }

            if (IsGameEnded)
            {
                return false;
            }

            _result = new GameSessionResult(reason, StatsTracker.KillCount, Global.CurrentTime.Value);
            Time.timeScale = 0f;
            OnGameEnded?.Invoke(_result);

            if (reason == GameEndReason.Victory)
            {
                OnVictory?.Invoke(_result);
            }
            else
            {
                OnDefeat?.Invoke(_result);
            }

            return true;
        }

        /// <summary>
        /// 重置本局会话数据，用于重新开始。
        /// </summary>
        public static void ResetSession()
        {
            _result = GameSessionResult.None;
            Global.ResetData();
            StatsTracker.Reset();
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 重新载入游戏场景。
        /// </summary>
        public static void RestartGame()
        {
            ResetSession();
            LoadScene(GAME_SCENE_NAME);
        }

        /// <summary>
        /// 从主菜单开始新一局游戏。
        /// </summary>
        public static void StartGame()
        {
            ResetSession();
            LoadScene(GAME_SCENE_NAME);
        }

        /// <summary>
        /// 返回独立主菜单场景。
        /// </summary>
        public static void ReturnToMainMenu()
        {
            ResetSession();
            LoadScene(MAIN_MENU_SCENE_NAME);
        }

        /// <summary>
        /// 设置是否真正加载场景，仅供 EditMode 测试避免切换场景。
        /// </summary>
        /// <param name="enabled">是否启用实际场景加载。</param>
        public static void SetSceneLoadingEnabledForTests(bool enabled)
        {
            _sceneLoadingEnabled = enabled;
        }

        private static void LoadScene(string sceneName)
        {
            _lastRequestedSceneNameForTests = sceneName;
            if (_sceneLoadingEnabled)
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _result = GameSessionResult.None;
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
