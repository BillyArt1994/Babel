using System;
using Babel.Unity.Presentation.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Babel
{
    /// <summary>
    /// Main-menu screen. Its hierarchy and button references are authored in the prefab;
    /// the scene-owned ScreenRouter controls its visible lifetime.
    /// </summary>
    public sealed class UIMainMenuPanel : Babel.Unity.Presentation.UI.Screen
    {
        private static readonly string[] SettlementPanelTypeNames =
        {
            "Babel.UIGameOverPanel",
            "Babel.UIGamePassPanel"
        };

        [SerializeField] private Button _startButton;
        [SerializeField] private Button _exitButton;

        private Action _startAction;
        private Action _quitAction = QuitGame;
        private bool _handled;

        public Button StartButton => _startButton;
        public Button ExitButton => _exitButton;

        /// <summary>Replaces menu actions in tests without changing scene navigation globally.</summary>
        public void SetActionsForTests(Action startAction, Action quitAction)
        {
            _startAction = startAction;
            _quitAction = quitAction;
        }

        protected override void OnScreenShown()
        {
            ValidateBindings();
            _handled = false;
            SetButtonsInteractable(true);

            UnityAction startListener = HandleStartClicked;
            UnityAction exitListener = HandleExitClicked;
            _startButton.onClick.AddListener(startListener);
            _exitButton.onClick.AddListener(exitListener);
            VisibilitySubscriptions.Add(() => _startButton.onClick.RemoveListener(startListener));
            VisibilitySubscriptions.Add(() => _exitButton.onClick.RemoveListener(exitListener));
        }

        private void HandleStartClicked()
        {
            RunOnce(_startAction ?? StartGameFromMenu);
        }

        private void HandleExitClicked()
        {
            RunOnce(_quitAction ?? QuitGame);
        }

        private void StartGameFromMenu()
        {
            CloseStaleSettlementPanels();
            GameSession.StartGame();
        }

        private static void CloseStaleSettlementPanels()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.gameObject.scene.IsValid()) continue;

                string fullName = behaviour.GetType().FullName;
                for (int typeIndex = 0; typeIndex < SettlementPanelTypeNames.Length; typeIndex++)
                {
                    if (!string.Equals(fullName, SettlementPanelTypeNames[typeIndex], StringComparison.Ordinal))
                        continue;

                    DestroySceneObject(behaviour.gameObject);
                    break;
                }
            }
        }

        private static void DestroySceneObject(GameObject value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void RunOnce(Action action)
        {
            if (_handled) return;
            _handled = true;
            SetButtonsInteractable(false);
            action?.Invoke();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = interactable;
        }

        private void ValidateBindings()
        {
            if (_startButton == null || _exitButton == null)
                throw new InvalidOperationException(
                    "UIMainMenuPanel requires serialized StartButton and ExitButton references.");
        }

        private static void QuitGame()
        {
            Debug.Log("[BABEL][MainMenu] Quit requested");
            Application.Quit();
        }
    }
}
