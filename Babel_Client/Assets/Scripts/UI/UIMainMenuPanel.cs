using System;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Babel
{
    /// <summary>
    /// 独立主菜单运行时 UI 面板。UI 结构由 prefab 定义，此脚本只负责按钮绑定。
    /// </summary>
    public class UIMainMenuPanel : UIPanel
    {
        private Action _startAction;
        private Action _quitAction = QuitGame;
        private bool _handled;

        /// <summary>
        /// 为 EditMode 测试替换按钮动作。
        /// </summary>
        public void SetActionsForTests(Action startAction, Action quitAction)
        {
            _startAction = startAction;
            _quitAction = quitAction;
        }

        protected override void OnInit(IUIData uiData = null)
        {
            _startAction = StartGameFromMenu;
        }

        protected override void OnOpen(IUIData uiData = null)
        {
            _handled = false;
            BindButtons();
        }

        protected override void OnShow() { }
        protected override void OnHide() { }

        protected override void OnClose()
        {
            RemoveButtonListeners();
        }

        private void BindButtons()
        {
            var startBtn = transform.Find("StartButton")?.GetComponent<Button>();
            var exitBtn  = transform.Find("ExitButton")?.GetComponent<Button>();
            if (startBtn == null || exitBtn == null)
            {
                Debug.LogWarning("[BABEL][MainMenu] StartButton or ExitButton not found in prefab");
                return;
            }
            startBtn.onClick.RemoveAllListeners();
            exitBtn.onClick.RemoveAllListeners();
            startBtn.interactable = true;
            exitBtn.interactable = true;
            startBtn.onClick.AddListener(() => RunOnce(_startAction ?? StartGameFromMenu));
            exitBtn.onClick.AddListener(() => RunOnce(_quitAction));
        }

        private void StartGameFromMenu()
        {
            CloseStaleGamePanels();
            if (Application.isPlaying) CloseSelf();
            GameSession.StartGame();
        }

        private static void CloseStaleGamePanels()
        {
            if (Application.isPlaying)
            {
                UIKit.ClosePanel<UIGameOverPanel>();
                UIKit.ClosePanel<UIGamePassPanel>();
                UIKit.ClosePanel<UIGamePanel>();
            }
            DestroyScenePanelsOfType<UIGameOverPanel>();
            DestroyScenePanelsOfType<UIGamePassPanel>();
        }

        private static void DestroyScenePanelsOfType<T>() where T : UIPanel
        {
            T[] panels = FindObjectsOfType<T>(true);
            for (int i = 0; i < panels.Length; i++)
                DestroyPanelObject(panels[i].gameObject);
        }

        private static void DestroyPanelObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
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
            foreach (var btn in GetComponentsInChildren<Button>(true))
                btn.interactable = interactable;
        }

        private void RemoveButtonListeners()
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
                btn.onClick.RemoveAllListeners();
        }

        private static void QuitGame()
        {
            Debug.Log("[BABEL][MainMenu] Quit requested");
            Application.Quit();
        }
    }
}
