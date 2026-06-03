using UnityEngine;
using UnityEngine.UI;
using QFramework;
using System;
using System.Collections.Generic;

namespace Babel
{
    public class UIGamePanelData : UIPanelData
    {
    }
    public partial class UIGamePanel : UIPanel
    {
        private static readonly float[] TIME_SCALES = { 1f, 2f, 4f };

        private Canvas _canvas;
        private RectTransform _panelRectTransform;
        private RectTransform _passiveSkillList;
        private Text _passiveOverflowText;
        private Button _pauseButton;
        private Text _pauseButtonText;
        private readonly Button[] _upgradeButtons = new Button[3];
        private IReadOnlyList<SkillConfig> _currentOptions = Array.Empty<SkillConfig>();
        private int _timeScaleIndex;
        private bool _pausedByButton;
        private float _timeScaleBeforePause = 1f;
        private const int MAX_PASSIVE_ICON_COUNT = 8;
        private static readonly Color UPGRADE_CARD_BACKGROUND_COLOR = new Color(0.10f, 0.08f, 0.14f, 0.94f);
        private static readonly Color UPGRADE_CARD_HIGHLIGHT_COLOR = new Color(0.18f, 0.14f, 0.24f, 0.98f);
        private static readonly Color UPGRADE_CARD_PRESSED_COLOR = new Color(0.26f, 0.20f, 0.32f, 1f);
        private static readonly Color UPGRADE_CARD_TITLE_COLOR = new Color(1f, 0.92f, 0.66f, 1f);
        private static readonly Color UPGRADE_CARD_BODY_COLOR = new Color(0.94f, 0.90f, 0.84f, 1f);
        private static readonly Color UPGRADE_CARD_TYPE_COLOR = new Color(1f, 0.72f, 0.28f, 1f);

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as UIGamePanelData ?? new UIGamePanelData();

            _canvas = GetComponentInParent<Canvas>();
            _panelRectTransform = transform as RectTransform;
            _upgradeButtons[0] = Card1Btn;
            _upgradeButtons[1] = Card2Btn;
            _upgradeButtons[2] = Card3Btn;
            ApplyRuntimePortraitLayout();
            ChargeRing.gameObject.SetActive(false);
            ChargeRing_Fill.fillAmount = 0;
            UpdateMainSkillCooldownFill();
            RefreshSkillHudFromSystem();
            ResetTimeScale();

            // please add init code here
            // EXP 进度条由 XpSystem.XpProgress 驱动（见 ActionKit.OnUpdate），Global.Exp 订阅已移除

             Global.Level.Register(Level =>
             {
                 LevelText.text = "LV:" + (Level).ToString();
             }).UnRegisterWhenGameObjectDestroyed(gameObject);

            Global.CurrentTime.RegisterWithInitValue(time =>
            {
                var currentTimeInt = Mathf.FloorToInt(Global.CurrentTime.Value);
                var seconds = currentTimeInt % 60;
                var minutes = currentTimeInt / 60;
                TimerText.text = $"{minutes:00}:{seconds:00}";
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            UpgradePanel.Hide();

            ActionKit.OnUpdate.Register(() =>
            {
                GameSession.TickCountdown(Time.deltaTime);
                UpdateMainSkillCooldownFill();
                // XP 进度条：由 XpSystem 驱动
                if (XpSystem.Instance != null && EXPScrollbar != null)
                    EXPScrollbar.size = XpSystem.Instance.XpProgress;
            }).UnRegisterWhenGameObjectDestroyed(gameObject);



        }

        protected override void OnOpen(IUIData uiData = null)
        {
            InputEvents.OnPointerDown += OnPointerDown;
            InputEvents.OnPointerHold += OnPointerHold;
            InputEvents.OnPointerUp += OnPointerUp;
            InputEvents.OnPointerCancel += OnPointerCancel;
            UpgradeEvents.OnOptionsGenerated += OnUpgradeOptionsGenerated;
            SkillEvents.OnEquippedSkillsChanged += RefreshSkillHud;
            GameSession.OnGameEnded += OnGameEnded;
            Card1Btn.onClick.AddListener(OnCard1Clicked);
            Card2Btn.onClick.AddListener(OnCard2Clicked);
            Card3Btn.onClick.AddListener(OnCard3Clicked);
            if (TimeScaleButton != null)
            {
                TimeScaleButton.onClick.AddListener(OnTimeScaleClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(OnPauseClicked);
            }
        }

        protected override void OnShow()
        {

        }

        protected override void OnHide()
        {

        }

        protected override void OnClose()
        {
            UnsubscribeEvents();
            ResetTimeScale();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            ResetTimeScale();
        }

        private void UnsubscribeEvents()
        {
            InputEvents.OnPointerDown -= OnPointerDown;
            InputEvents.OnPointerHold -= OnPointerHold;
            InputEvents.OnPointerUp -= OnPointerUp;
            InputEvents.OnPointerCancel -= OnPointerCancel;
            UpgradeEvents.OnOptionsGenerated -= OnUpgradeOptionsGenerated;
            SkillEvents.OnEquippedSkillsChanged -= RefreshSkillHud;
            GameSession.OnGameEnded -= OnGameEnded;
            if (Card1Btn != null)
            {
                Card1Btn.onClick.RemoveListener(OnCard1Clicked);
            }

            if (Card2Btn != null)
            {
                Card2Btn.onClick.RemoveListener(OnCard2Clicked);
            }

            if (Card3Btn != null)
            {
                Card3Btn.onClick.RemoveListener(OnCard3Clicked);
            }

            if (TimeScaleButton != null)
            {
                TimeScaleButton.onClick.RemoveListener(OnTimeScaleClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(OnPauseClicked);
            }
        }

        private void OnUpgradeOptionsGenerated(IReadOnlyList<SkillConfig> options)
        {
            _currentOptions = options ?? Array.Empty<SkillConfig>();
            if (_currentOptions.Count == 0)
            {
                UpgradePanel.Hide();
                SetUpgradeButtonsActive(false);
                return;
            }

            for (int i = 0; i < _upgradeButtons.Length; i++)
            {
                SetUpgradeButton(i);
            }

            UpgradePanel.Show();
        }

        private void SetUpgradeButton(int index)
        {
            Button button = _upgradeButtons[index];
            bool hasOption = index < _currentOptions.Count;
            button.gameObject.SetActive(hasOption);
            if (!hasOption)
            {
                return;
            }

            SkillConfig config = _currentOptions[index];
            ConfigureUpgradeCard(button, config);
        }

        private void SetUpgradeButtonsActive(bool active)
        {
            for (int i = 0; i < _upgradeButtons.Length; i++)
            {
                _upgradeButtons[i].gameObject.SetActive(active);
            }
        }

        private void ConfigureUpgradeCard(Button button, SkillConfig config)
        {
            if (button == null || config == null)
            {
                return;
            }

            ApplyUpgradeCardStyle(button);

            Image icon = EnsureCardIcon(button.transform);
            icon.sprite = SkillIconLoader.LoadIcon(config);

            Text typeLabel = FindOrCreateCardText(button.transform, "TypeLabel", new Vector2(0f, 78f), new Vector2(88f, 26f), 20);
            typeLabel.text = IsPassiveSkill(config) ? "被动" : "主动";
            typeLabel.color = UPGRADE_CARD_TYPE_COLOR;

            Text nameText = FindOrCreateCardText(button.transform, "SkillNameText", new Vector2(0f, 36f), new Vector2(162f, 44f), 24);
            nameText.text = config.SkillName;
            nameText.color = UPGRADE_CARD_TITLE_COLOR;

            Text descriptionText = FindOrCreateCardText(button.transform, "SkillDecsText", new Vector2(0f, -58f), new Vector2(162f, 118f), 18);
            descriptionText.text = config.Description;
            descriptionText.color = UPGRADE_CARD_BODY_COLOR;
        }

        private Image EnsureCardIcon(Transform card)
        {
            Transform existing = card.Find("SkillIcon");
            Image icon = existing != null ? existing.GetComponent<Image>() : null;
            if (icon != null)
            {
                ApplyCardRect(icon.rectTransform, new Vector2(0f, 126f), new Vector2(56f, 56f));
                return icon;
            }

            var iconObject = new GameObject("SkillIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(card, false);
            RectTransform rect = (RectTransform)iconObject.transform;
            ApplyCardRect(rect, new Vector2(0f, 126f), new Vector2(56f, 56f));
            return iconObject.GetComponent<Image>();
        }

        private Text FindOrCreateCardText(Transform card, string name, Vector2 position, Vector2 sizeDelta, int fontSize)
        {
            Transform existing = card.Find(name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                text = EnsureCardText(card, name);
            }

            // 描述文字使用固定字号 + 自动换行（多行展示），其余使用 bestFit 自适应。
            bool isDescription = name == "SkillDecsText";
            ApplyCardTextLayout(text, position, sizeDelta, fontSize, isDescription);
            return text;
        }

        private Text EnsureCardText(Transform card, string name)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(card, false);
            Text text = textObject.GetComponent<Text>();
            text.font = BabelFont.Default;
            text.color = UPGRADE_CARD_BODY_COLOR;
            return text;
        }

        private void ApplyCardTextLayout(Text text, Vector2 position, Vector2 sizeDelta, int fontSize, bool wrapMultiline)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;

            text.font = BabelFont.Default;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = UPGRADE_CARD_BODY_COLOR;
            text.resizeTextForBestFit = false;
            text.fontSize = fontSize;

            if (wrapMultiline)
            {
                // 描述：自动换行，超出区域则截断。
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
            else
            {
                // 名称/标签：单行不换行，固定字号保证清晰。
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private void ApplyCardRect(RectTransform rect, Vector2 position, Vector2 sizeDelta)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
        }

        private void OnCard1Clicked()
        {
            UpgradeEvents.RaiseOptionSelected(0);
        }

        private void OnCard2Clicked()
        {
            UpgradeEvents.RaiseOptionSelected(1);
        }

        private void OnCard3Clicked()
        {
            UpgradeEvents.RaiseOptionSelected(2);
        }

        private void OnTimeScaleClicked()
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            _timeScaleIndex = GetNextTimeScaleIndex(_timeScaleIndex, TIME_SCALES.Length);
            ApplyTimeScale();
        }

        private void OnPauseClicked()
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            EnsureRuntimeHudControls();
            if (!_pausedByButton)
            {
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : TIME_SCALES[_timeScaleIndex];
                Time.timeScale = 0f;
                _pausedByButton = true;
                UpdatePauseButtonText();
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _pausedByButton = false;
            UpdatePauseButtonText();
        }

        private void ApplyTimeScale()
        {
            float scale = TIME_SCALES[_timeScaleIndex];
            if (Time.timeScale > 0f)
            {
                Time.timeScale = scale;
            }

            if (TimeScaleText != null)
            {
                TimeScaleText.text = $"{scale:0}x";
            }
        }

        private void ResetTimeScale()
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            _timeScaleIndex = 0;
            _pausedByButton = false;
            _timeScaleBeforePause = TIME_SCALES[_timeScaleIndex];
            Time.timeScale = TIME_SCALES[_timeScaleIndex];
            if (TimeScaleText != null)
            {
                TimeScaleText.text = "1x";
            }

            UpdatePauseButtonText();
        }

        private static int GetNextTimeScaleIndex(int currentIndex, int scaleCount)
        {
            return (currentIndex + 1) % scaleCount;
        }

        private void EnsureRuntimeHudControls()
        {
            Transform existing = transform.Find("PauseButton");
            if (existing != null)
            {
                _pauseButton = existing.GetComponent<Button>();
                _pauseButtonText = existing.GetComponentInChildren<Text>(true);
            }
            else
            {
                var pauseObject = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                pauseObject.transform.SetParent(transform, false);
                Image background = pauseObject.GetComponent<Image>();
                background.color = new Color(1f, 1f, 1f, 0.65f);
                _pauseButton = pauseObject.GetComponent<Button>();
                CreatePauseText(pauseObject.transform);
            }

            ApplyAnchoredRect((RectTransform)_pauseButton.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(24f, -72f), new Vector2(32f, 80f));
            UpdatePauseButtonText();
        }

        private void CreatePauseText(Transform parent)
        {
            var labelObject = new GameObject("PauseText", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            _pauseButtonText = labelObject.GetComponent<Text>();
            _pauseButtonText.alignment = TextAnchor.MiddleCenter;
            _pauseButtonText.font = BabelFont.Default;
            _pauseButtonText.fontSize = 24;
            _pauseButtonText.color = Color.black;
        }

        private void ApplyRuntimePortraitLayout()
        {
            EnsureRuntimeHudControls();
            ApplyHudRect(transform.Find("TimeScale") as RectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(84f, -72f), new Vector2(80f, 80f));
            ApplyHudRect(TimerText != null ? TimerText.rectTransform : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -56f), new Vector2(180f, 56f));
            ApplyHudRect(MainSkill_Image != null ? MainSkill_Image.rectTransform : null, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-72f, -72f), new Vector2(80f, 80f));
            EnsurePassiveSkillList();
            LayoutUpgradeCards();
        }

        private void ApplyHudRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            ApplyAnchoredRect(rect, anchor, pivot, position, size);
        }

        private void ApplyAnchoredRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void LayoutUpgradeCards()
        {
            if (UpgradePanel == null)
            {
                return;
            }

            LayoutGroup[] layoutGroups = UpgradePanel.GetComponents<LayoutGroup>();
            for (int i = 0; i < layoutGroups.Length; i++)
            {
                layoutGroups[i].enabled = false;
            }

            LayoutUpgradeCard(Card1Btn, -215f);
            LayoutUpgradeCard(Card2Btn, 0f);
            LayoutUpgradeCard(Card3Btn, 215f);
        }

        private void LayoutUpgradeCard(Button button, float x)
        {
            if (button == null)
            {
                return;
            }

            ApplyCardRect(button.transform as RectTransform, new Vector2(x, 0f), new Vector2(190f, 280f));
            ApplyUpgradeCardStyle(button);
            EnsureCardIcon(button.transform);
            FindOrCreateCardText(button.transform, "TypeLabel", new Vector2(0f, 78f), new Vector2(88f, 26f), 20);
            FindOrCreateCardText(button.transform, "SkillNameText", new Vector2(0f, 36f), new Vector2(162f, 44f), 24);
            FindOrCreateCardText(button.transform, "SkillDecsText", new Vector2(0f, -58f), new Vector2(162f, 118f), 18);
        }

        private void ApplyUpgradeCardStyle(Button button)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = UPGRADE_CARD_BACKGROUND_COLOR;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = UPGRADE_CARD_BACKGROUND_COLOR;
            colors.highlightedColor = UPGRADE_CARD_HIGHLIGHT_COLOR;
            colors.pressedColor = UPGRADE_CARD_PRESSED_COLOR;
            colors.selectedColor = UPGRADE_CARD_HIGHLIGHT_COLOR;
            colors.disabledColor = new Color(0.18f, 0.16f, 0.20f, 0.55f);
            button.colors = colors;
        }

        private void UpdatePauseButtonText()
        {
            if (_pauseButtonText != null)
            {
                _pauseButtonText.text = _pausedByButton ? "▶" : "Ⅱ";
            }
        }

        private void OnPointerDown(PointerInputContext context)
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            if (IsMainSkillCoolingDown())
            {
                HideChargeRing();
                return;
            }

            ChargeRing.gameObject.SetActive(true);
            UpdateChargeRingPosition(context.ScreenPosition);
            ChargeRing_Fill.fillAmount = 0f;
        }

        private void OnPointerHold(PointerInputContext context)
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            if (IsMainSkillCoolingDown())
            {
                HideChargeRing();
                return;
            }

            UpdateChargeRingPosition(context.ScreenPosition);
            ChargeRing_Fill.fillAmount = context.ChargeRatio;
        }

        private void OnPointerUp(PointerInputContext context)
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            HideChargeRing();
        }

        private void OnPointerCancel(PointerInputContext context)
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            HideChargeRing();
        }

        private void HideChargeRing()
        {
            ChargeRing.gameObject.SetActive(false);
            ChargeRing_Fill.fillAmount = 0f;
        }

        private void UpdateChargeRingPosition(Vector2 screenPosition)
        {
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRectTransform,
                screenPosition,
                uiCamera,
                out var localPoint))
            {
                ChargeRing.anchoredPosition = localPoint;
            }
        }

        private void UpdateMainSkillCooldownFill()
        {
            if (MainSkill_ImageFill == null)
            {
                return;
            }

            MainSkill_ImageFill.fillAmount = SkillSystem.Instance != null
                ? SkillSystem.Instance.GetActiveClickCooldownProgress()
                : 0f;
        }

        private bool IsMainSkillCoolingDown()
        {
            return SkillSystem.Instance != null &&
                SkillSystem.Instance.GetActiveClickCooldownProgress() > 0f;
        }

        private void RefreshSkillHudFromSystem()
        {
            RefreshSkillHud(SkillSystem.Instance != null
                ? SkillSystem.Instance.GetEquippedSkillsAsConfigs()
                : Array.Empty<SkillConfig>());
        }

        private void RefreshSkillHud(IReadOnlyList<SkillConfig> skills)
        {
            EnsurePassiveSkillList();
            SkillConfig activeSkill = null;
            var passiveSkills = new List<SkillConfig>();

            for (int i = 0; i < skills.Count; i++)
            {
                SkillConfig config = skills[i];
                if (IsPassiveSkill(config))
                {
                    passiveSkills.Add(config);
                }
                else if (activeSkill == null)
                {
                    activeSkill = config;
                }
            }

            if (MainSkill_Image != null)
            {
                MainSkill_Image.sprite = SkillIconLoader.LoadIcon(activeSkill);
            }

            RefreshPassiveIcons(passiveSkills);
        }

        private void EnsurePassiveSkillList()
        {
            if (_passiveSkillList != null)
            {
                LayoutPassiveSkillList();
                return;
            }

            Transform existing = transform.Find("PassiveSkillList");
            if (existing != null)
            {
                _passiveSkillList = (RectTransform)existing;
            }
            else
            {
                var listObject = new GameObject("PassiveSkillList", typeof(RectTransform), typeof(VerticalLayoutGroup));
                listObject.transform.SetParent(transform, false);
                _passiveSkillList = (RectTransform)listObject.transform;
            }

            LayoutPassiveSkillList();
            VerticalLayoutGroup layout = _passiveSkillList.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = _passiveSkillList.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            if (layout != null)
            {
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.spacing = 6f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

        }

        private void LayoutPassiveSkillList()
        {
            ApplyAnchoredRect(_passiveSkillList, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -118f), new Vector2(40f, 360f));
        }

        private void EnsurePassiveOverflowText()
        {
            Transform existing = _passiveSkillList.Find("OverflowText");
            if (existing != null)
            {
                _passiveOverflowText = existing.GetComponent<Text>();
                return;
            }

            var overflowObject = new GameObject("OverflowText", typeof(RectTransform), typeof(Text));
            overflowObject.transform.SetParent(_passiveSkillList, false);
            RectTransform overflowRect = (RectTransform)overflowObject.transform;
            overflowRect.sizeDelta = new Vector2(36f, 22f);
            _passiveOverflowText = overflowObject.GetComponent<Text>();
            _passiveOverflowText.alignment = TextAnchor.MiddleCenter;
            _passiveOverflowText.font = BabelFont.Default;
            _passiveOverflowText.fontSize = 14;
            _passiveOverflowText.color = Color.white;
            _passiveOverflowText.gameObject.SetActive(false);
        }

        private void RefreshPassiveIcons(IReadOnlyList<SkillConfig> passiveSkills)
        {
            ClearPassiveIconChildren();
            int visibleCount = Mathf.Min(passiveSkills.Count, MAX_PASSIVE_ICON_COUNT);
            for (int i = 0; i < visibleCount; i++)
            {
                var iconObject = new GameObject($"PassiveSkillIcon_{i}", typeof(RectTransform), typeof(PassiveSkillIconView));
                iconObject.transform.SetParent(_passiveSkillList, false);
                iconObject.GetComponent<PassiveSkillIconView>().Configure(passiveSkills[i], 1);
            }

            int overflowCount = passiveSkills.Count - visibleCount;
            if (overflowCount > 0)
            {
                EnsurePassiveOverflowText();
                _passiveOverflowText.transform.SetAsLastSibling();
                _passiveOverflowText.gameObject.SetActive(true);
                _passiveOverflowText.text = $"+{overflowCount}";
            }
            else if (_passiveOverflowText != null)
            {
                DestroyPassiveChild(_passiveOverflowText.gameObject);
                _passiveOverflowText = null;
            }
        }

        private void ClearPassiveIconChildren()
        {
            for (int i = _passiveSkillList.childCount - 1; i >= 0; i--)
            {
                Transform child = _passiveSkillList.GetChild(i);
                if (_passiveOverflowText != null && child == _passiveOverflowText.transform)
                {
                    continue;
                }

                DestroyPassiveChild(child.gameObject);
            }
        }

        private void DestroyPassiveChild(GameObject child)
        {
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        private static bool IsPassiveSkill(SkillConfig config)
        {
            return config != null && config.TriggerType != "OnClick";
        }

        private void OnGameEnded(GameSessionResult result)
        {
            if (result.Reason == GameEndReason.Victory)
            {
                UIKit.OpenPanel<UIGamePassPanel>();
            }
            else if (result.Reason == GameEndReason.Defeat)
            {
                UIKit.OpenPanel<UIGameOverPanel>();
            }

            gameObject.SetActive(false);
        }

    }
}
