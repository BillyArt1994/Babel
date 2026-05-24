using System;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Babel
{
    /// <summary>
    /// 独立主菜单运行时 UI 面板。
    /// </summary>
    public class UIMainMenuPanel : UIPanel
    {
        private Action _startAction;
        private Action _quitAction = QuitGame;
        private bool _handled;

        /// <summary>
        /// 为 EditMode 测试替换按钮动作。
        /// </summary>
        /// <param name="startAction">开始游戏动作。</param>
        /// <param name="quitAction">退出游戏动作。</param>
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
            BuildRuntimeLayout();
            BindButtons();
        }

        protected override void OnShow()
        {
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
            RemoveButtonListeners();
        }

        private void BuildRuntimeLayout()
        {
            ClearChildren();
            RectTransform panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                Stretch(panelRect);
            }

            CreateImage("MenuBackground", Stretch, new Color(0.05f, 0.04f, 0.08f, 1f));
            CreateImage("TowerBackground", ConfigureTower, new Color(0.35f, 0.24f, 0.14f, 0.82f));
            CreateImage("LightningAccent", ConfigureLightning, new Color(1f, 0.86f, 0.32f, 0.86f));
            CreateText("MenuTitle", "BABEL", new Vector2(0f, 160f), new Vector2(420f, 120f), 64, Color.white);
            CreateText("MenuSubtitle", "阻止人类触及天庭", new Vector2(0f, 88f), new Vector2(360f, 52f), 24, new Color(1f, 0.86f, 0.58f, 1f));
            CreateButton("StartButton", "开始游戏", new Vector2(0f, -96f));
            CreateButton("ExitButton", "退出游戏", new Vector2(0f, -174f));
            _handled = false;
        }

        private void BindButtons()
        {
            Button startButton = transform.Find("StartButton").GetComponent<Button>();
            Button exitButton = transform.Find("ExitButton").GetComponent<Button>();
            startButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();
            startButton.interactable = true;
            exitButton.interactable = true;
            startButton.onClick.AddListener(() => RunOnce(_startAction ?? StartGameFromMenu));
            exitButton.onClick.AddListener(() => RunOnce(_quitAction));
        }

        private void StartGameFromMenu()
        {
            CloseStaleGamePanels();
            if (Application.isPlaying)
            {
                CloseSelf();
            }

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
            {
                DestroyPanelObject(panels[i].gameObject);
            }
        }

        private static void DestroyPanelObject(GameObject panelObject)
        {
            if (Application.isPlaying)
            {
                Destroy(panelObject);
            }
            else
            {
                DestroyImmediate(panelObject);
            }
        }

        private void RunOnce(Action action)
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            SetButtonsInteractable(false);
            action?.Invoke();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = interactable;
            }
        }

        private void RemoveButtonListeners()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].onClick.RemoveAllListeners();
            }
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private Image CreateImage(string name, Action<RectTransform> configure, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)imageObject.transform;
            configure(rect);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(transform, false);
            Text text = textObject.GetComponent<Text>();
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            text.color = color;
            return text;
        }

        private Button CreateButton(string name, string label, Vector2 position)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(260f, 58f);
            buttonObject.GetComponent<Image>().color = new Color(0.95f, 0.78f, 0.38f, 0.96f);
            CreateButtonLabel(rect, label);
            return buttonObject.GetComponent<Button>();
        }

        private static void CreateButtonLabel(RectTransform buttonRect, string label)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonRect, false);
            RectTransform rect = (RectTransform)textObject.transform;
            Stretch(rect);
            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 24;
            text.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        }

        private static void ConfigureTower(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(210f, 420f);
        }

        private static void ConfigureLightning(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(94f, 196f);
            rect.sizeDelta = new Vector2(28f, 240f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void QuitGame()
        {
            Debug.Log("[BABEL][MainMenu] Quit requested");
            Application.Quit();
        }
    }
}
