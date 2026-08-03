using Babel.Unity.Presentation.UI;
using UnityEngine;

namespace Babel
{
    /// <summary>Scene composition entry for the explicitly authored main-menu screen.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string MainMenuScreenId = "main-menu";

        [SerializeField] private ScreenRouter _screenRouter;
        [SerializeField] private UIMainMenuPanel _mainMenuPanel;

        private bool _registered;

        public ScreenRouter Router => _screenRouter;
        public UIMainMenuPanel Panel => _mainMenuPanel;

        private void Awake()
        {
            if (_screenRouter == null || _mainMenuPanel == null)
            {
                throw new MissingReferenceException(
                    "MainMenuController requires serialized ScreenRouter and UIMainMenuPanel references.");
            }

            _screenRouter.Register(MainMenuScreenId, _mainMenuPanel);
            _registered = true;
        }

        private void Start()
        {
            _screenRouter.Show(MainMenuScreenId);
        }

        private void OnDestroy()
        {
            if (!_registered || _screenRouter == null || _screenRouter.IsDisposed) return;
            _screenRouter.Unregister(MainMenuScreenId);
            _registered = false;
        }
    }
}
