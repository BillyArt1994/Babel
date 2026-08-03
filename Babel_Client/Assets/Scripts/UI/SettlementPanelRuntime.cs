using System;
using UnityEngine;
using UnityEngine.UI;

namespace Babel
{
    /// <summary>
    /// 为项目自有结算 Screen 按胜利/失败结果生成运行时 UI。
    /// </summary>
    public static class SettlementPanelRuntime
    {
        private const string ROOT_NAME = "SettlementRoot";
        private const string BUTTONS_ROOT_NAME = "SettlementButtons";
        private const string RESTART_BUTTON_NAME = "RestartButton";
        private const string MENU_BUTTON_NAME = "MenuButton";

        /// <summary>
        /// 配置结算面板显示内容与按钮行为。
        /// </summary>
        /// <param name="root">结算面板根节点。</param>
        /// <param name="result">结算快照。</param>
        /// <param name="restartAction">再战一局回调。</param>
        /// <param name="menuAction">返回主菜单回调。</param>
        public static void Configure(
            Transform root,
            GameSessionResult result,
            Action restartAction,
            Action menuAction)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            RectTransform settlementRoot = RebuildRoot(root);
            RectTransform contentRoot = result.Reason == GameEndReason.Defeat
                ? BuildDefeatFullScreenLayout(settlementRoot, result)
                : BuildVictoryCardLayout(settlementRoot, result);
            BuildButtons(settlementRoot, contentRoot, restartAction, menuAction);
        }

        private static RectTransform RebuildRoot(Transform root)
        {
            Transform existing = root.Find(ROOT_NAME);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var rootObject = new GameObject(ROOT_NAME, typeof(RectTransform));
            rootObject.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)rootObject.transform;
            Stretch(rect);
            return rect;
        }

        private static RectTransform BuildVictoryCardLayout(RectTransform root, GameSessionResult result)
        {
            CreateImage(root, "DimOverlay", Stretch, new Color(0f, 0f, 0f, 0.36f));
            RectTransform card = CreateImage(root, "VictoryCard", ConfigureVictoryCard, new Color(0.96f, 0.73f, 0.28f, 0.92f));
            CreateText(card, "ResultBadge", "神罚完成", new Vector2(0f, 168f), new Vector2(220f, 44f), 24, new Color(0.18f, 0.11f, 0.03f, 1f));
            CreateText(card, "Title", GetTitle(result.Reason), new Vector2(0f, 88f), new Vector2(560f, 84f), 34, Color.white);
            CreateText(card, "Subtitle", GetSubtitle(result.Reason), new Vector2(0f, 14f), new Vector2(520f, 64f), 22, new Color(1f, 0.96f, 0.85f, 1f));
            CreateText(card, "KillCountBadge", $"消灭了 {result.KillCount} 名人类", new Vector2(0f, -78f), new Vector2(320f, 52f), 24, new Color(0.2f, 0.12f, 0.02f, 1f));
            return card;
        }

        private static RectTransform BuildDefeatFullScreenLayout(RectTransform root, GameSessionResult result)
        {
            RectTransform overlay = CreateImage(root, "DefeatOverlay", Stretch, new Color(0.07f, 0.01f, 0.01f, 0.96f));
            CreateText(overlay, "ResultBadge", "塔已通天", new Vector2(0f, 176f), new Vector2(220f, 48f), 24, new Color(1f, 0.42f, 0.32f, 1f));
            CreateText(overlay, "Title", GetTitle(result.Reason), new Vector2(0f, 82f), new Vector2(620f, 100f), 36, Color.white);
            CreateText(overlay, "Subtitle", GetSubtitle(result.Reason), new Vector2(0f, -8f), new Vector2(560f, 72f), 22, new Color(0.95f, 0.74f, 0.68f, 1f));
            CreateText(overlay, "KillCountBadge", $"消灭了 {result.KillCount} 名人类", new Vector2(0f, -116f), new Vector2(320f, 52f), 24, new Color(1f, 0.82f, 0.62f, 1f));
            return overlay;
        }

        private static void BuildButtons(RectTransform root, RectTransform contentRoot, Action restartAction, Action menuAction)
        {
            var buttonsObject = new GameObject(BUTTONS_ROOT_NAME, typeof(RectTransform));
            buttonsObject.transform.SetParent(root, false);
            RectTransform buttonsRoot = (RectTransform)buttonsObject.transform;
            buttonsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            buttonsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            buttonsRoot.pivot = new Vector2(0.5f, 0.5f);
            buttonsRoot.anchoredPosition = contentRoot.name == "VictoryCard" ? new Vector2(0f, -230f) : new Vector2(0f, -250f);
            buttonsRoot.sizeDelta = new Vector2(420f, 64f);

            Button restartButton = CreateButton(buttonsRoot, RESTART_BUTTON_NAME, "再战一局", new Vector2(-112f, 0f));
            Button menuButton = CreateButton(buttonsRoot, MENU_BUTTON_NAME, "返回主菜单", new Vector2(112f, 0f));
            var state = new SettlementButtonState();
            restartButton.onClick.AddListener(() => RunOnce(root, state, restartAction));
            menuButton.onClick.AddListener(() => RunOnce(root, state, menuAction));
        }

        private static RectTransform CreateImage(RectTransform parent, string name, Action<RectTransform> configure, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)imageObject.transform;
            configure(rect);
            imageObject.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.text = value;
            text.font = BabelFont.Default;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = fontSize;
            text.color = color;
            return text;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 position)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(190f, 56f);
            buttonObject.GetComponent<Image>().color = new Color(1f, 0.93f, 0.74f, 0.95f);
            CreateText(rect, "Text", label, Vector2.zero, rect.sizeDelta, 22, Color.black);
            return buttonObject.GetComponent<Button>();
        }

        private static void ConfigureVictoryCard(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640f, 460f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void RunOnce(Transform root, SettlementButtonState state, Action action)
        {
            if (state.Handled)
            {
                return;
            }

            state.Handled = true;
            SetButtonsInteractable(root, false);
            action?.Invoke();
        }

        private static void SetButtonsInteractable(Transform root, bool interactable)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = interactable;
            }
        }

        private static string GetTitle(GameEndReason reason)
        {
            return reason == GameEndReason.Victory
                ? "天神的震怒平息，巴别塔化为尘埃"
                : "通天塔已建成——人类抵达了天庭";
        }

        private static string GetSubtitle(GameEndReason reason)
        {
            return reason == GameEndReason.Victory
                ? "人类的僭越之心，终被镇压于历史长河"
                : "审判日已降临，神的宝座第一次被动摇";
        }

        private sealed class SettlementButtonState
        {
            public bool Handled;
        }
    }
}
