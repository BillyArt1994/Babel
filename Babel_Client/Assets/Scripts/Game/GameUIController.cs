using Babel.Unity.Presentation.UI;
using UnityEngine;

namespace Babel
{
    [DisallowMultipleComponent]
    public partial class GameUIController : MonoBehaviour
    {
        public const string HudScreenId = "hud";
        public const string WinScreenId = "win";
        public const string LoseScreenId = "lose";

        [SerializeField] private ScreenRouter screenRouter;
        [SerializeField] private UIGamePanel hudScreen;
        [SerializeField] private UIGamePassPanel winScreen;
        [SerializeField] private UIGameOverPanel loseScreen;

        private bool _screensRegistered;

        private void Awake()
        {
            RegisterScreens();
        }

        private void OnEnable()
        {
            GameSession.OnGameEnded += OnGameEnded;
        }

        private void Start()
        {
            if (!_screensRegistered) RegisterScreens();
            if (_screensRegistered) ShowForResult(GameSession.Result);
        }

        private void OnDisable()
        {
            GameSession.OnGameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            GameSession.OnGameEnded -= OnGameEnded;
            UnregisterScreens();
        }

        private void RegisterScreens()
        {
            if (_screensRegistered) return;
            if (screenRouter == null || hudScreen == null || winScreen == null || loseScreen == null)
            {
                Debug.LogError("[Babel][GameUIController] ScreenRouter and all game screens must be assigned in the scene.", this);
                return;
            }

            screenRouter.Register(HudScreenId, hudScreen);
            screenRouter.Register(WinScreenId, winScreen);
            screenRouter.Register(LoseScreenId, loseScreen);
            _screensRegistered = true;
        }

        private void UnregisterScreens()
        {
            if (!_screensRegistered || screenRouter == null || screenRouter.IsDisposed) return;

            screenRouter.Unregister(HudScreenId);
            screenRouter.Unregister(WinScreenId);
            screenRouter.Unregister(LoseScreenId);
            _screensRegistered = false;
        }

        private void OnGameEnded(GameSessionResult result)
        {
            ShowForResult(result);
        }

        private void ShowForResult(GameSessionResult result)
        {
            if (!_screensRegistered) return;

            switch (result.Reason)
            {
                case GameEndReason.Victory:
                    screenRouter.Show(WinScreenId);
                    break;
                case GameEndReason.Defeat:
                    screenRouter.Show(LoseScreenId);
                    break;
                default:
                    screenRouter.Show(HudScreenId);
                    break;
            }
        }
    }
}
