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
        private const string BG_SPRITE_PATH        = "Art/UI/MainMenu/bg_mainmenu";
        private const string LOGO_SPRITE_PATH      = "Art/UI/MainMenu/logo_babel";
        private const string BTN_START_SPRITE_PATH = "Art/UI/MainMenu/btn_start";
        private const string BTN_EXIT_SPRITE_PATH  = "Art/UI/MainMenu/btn_exit";

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
            if (panelRect != null) Stretch(panelRect);

            Sprite bgSprite = Resources.Load<Sprite>(BG_SPRITE_PATH);
            CreateSpriteImage("MenuBackground", Stretch, bgSprite, Color.white, Image.Type.Simple);

            Sprite logoSprite = Resources.Load<Sprite>(LOGO_SPRITE_PATH);
            CreateSpriteImage("LogoImage", ConfigureLogo, logoSprite, Color.white, Image.Type.Simple);

            CreateText("MenuSubtitle", "阻止人类触及天庭",
                new Vector2(0f, 80f), new Vector2(360f, 52f), 24,
                new Color(1f, 0.86f, 0.58f, 1f));

            Sprite startSprite = Resources.Load<Sprite>(BTN_START_SPRITE_PATH);
            CreateSpriteButton("StartButton", "开始游戏", new Vector2(0f, -96f),
                new Vector2(300f, 72f), startSprite, new Color(0.12f, 0.08f, 0.02f, 1f));

            Sprite exitSprite = Resources.Load<Sprite>(BTN_EXIT_SPRITE_PATH);
            CreateSpriteButton("ExitButton", "退出游戏", new Vector2(0f, -186f),
                new Vector2(240f, 58f), exitSprite, new Color(0.85f, 0.82f, 0.80f, 1f));

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

        private static void CreateButtonLabel(RectTransform buttonRect, string label, Color textColor)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonRect, false);
            RectTransform rect = (RectTransform)textObject.transform;
            Stretch(rect);
            Text text = textObject.GetComponent<Text>();
            text.text      = label;
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize  = 26;
            text.fontStyle = FontStyle.Bold;
            text.color     = textColor;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void ConfigureLogo(RectTransform rect)
        {
            rect.anchorMin        = new Vector2(0.5f, 1f);
            rect.anchorMax        = new Vector2(0.5f, 1f);
            rect.pivot            = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta        = new Vector2(420f, 160f);
        }

        private Image CreateSpriteImage(string name, Action<RectTransform> configure,
            Sprite sprite, Color tint, Image.Type imageType)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            configure((RectTransform)go.transform);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color  = sprite != null ? tint : tint * new Color(0.3f, 0.3f, 0.3f, 1f);
            img.type   = imageType;
            return img;
        }

        private Button CreateSpriteButton(string name, string label, Vector2 position,
            Vector2 size, Sprite sprite, Color textColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta        = size;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type   = Image.Type.Sliced;
            img.color  = sprite != null ? Color.white : new Color(0.95f, 0.78f, 0.38f, 0.96f);
            CreateButtonLabel(rect, label, textColor);
            return go.GetComponent<Button>();
        }

        private static void QuitGame()
        {
            Debug.Log("[BABEL][MainMenu] Quit requested");
            Application.Quit();
        }
    }
}
