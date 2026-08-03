using UnityEngine;
using UnityEngine.UI;
using Babel.Unity.Presentation.UI;
using System;
using System.Collections.Generic;
using Babel.Gameplay.RunFlow;
using Babel.Unity.Infrastructure.Time;

namespace Babel
{
    public partial class UIGamePanel : Babel.Unity.Presentation.UI.Screen
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
        private long _lastRunReadModelVersion = -1;
        private const int MAX_PASSIVE_ICON_COUNT = 8;
        private static readonly Color UPGRADE_CARD_BACKGROUND_COLOR = new Color(0.10f, 0.08f, 0.14f, 0.94f);
        private static readonly Color UPGRADE_CARD_HIGHLIGHT_COLOR = new Color(0.18f, 0.14f, 0.24f, 0.98f);
        private static readonly Color UPGRADE_CARD_PRESSED_COLOR = new Color(0.26f, 0.20f, 0.32f, 1f);
        private static readonly Color UPGRADE_CARD_TITLE_COLOR = new Color(1f, 0.92f, 0.66f, 1f);
        private static readonly Color UPGRADE_CARD_BODY_COLOR = new Color(0.94f, 0.90f, 0.84f, 1f);
        private static readonly Color UPGRADE_CARD_TYPE_COLOR = new Color(1f, 0.72f, 0.28f, 1f);

        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _canvas = GetComponentInParent<Canvas>();
            _panelRectTransform = transform as RectTransform;
            _upgradeButtons[0] = Card1Btn;
            _upgradeButtons[1] = Card2Btn;
            _upgradeButtons[2] = Card3Btn;
            _initialized = true;

            ApplyRuntimePortraitLayout();
            HideChargeRing();
            SetUpgradePanelVisible(false);
            SetUpgradeButtonsActive(false);
            RefreshSkillHudFromSystem();
            RefreshPresentation();
            ResetTimeScale();
        }

        protected override void OnScreenShown()
        {
            EnsureInitialized();
            _lastRunReadModelVersion = -1;
            SubscribeVisibilityEvents();
            ApplyRuntimePortraitLayout();
            HideChargeRing();
            SetUpgradePanelVisible(false);
            SetUpgradeButtonsActive(false);
            RefreshSkillHudFromSystem();
            RefreshPresentation();
            ResetTimeScale();
        }

        protected override void OnScreenHidden()
        {
            _currentOptions = Array.Empty<SkillConfig>();
            HideChargeRing();
            SetUpgradePanelVisible(false);
            SetUpgradeButtonsActive(false);
            ResetTimeScale();
        }

        private void Update()
        {
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            SyncRunControlsFromReadModel();
            UpdateMainSkillCooldownFill();

            XpSystem xpSystem = XpSystem.Instance;
            if (LevelText != null)
                LevelText.text = "LV:" + (xpSystem != null ? xpSystem.CurrentLevel : 1);
            if (EXPScrollbar != null)
                EXPScrollbar.size = xpSystem != null ? xpSystem.XpProgress : 0f;
            if (!LegacyRunBridge.IsAvailable)
                UpdateTimerText(GameSession.RemainingTime);
        }

        private void UpdateTimerText(float remainingTime)
        {
            if (TimerText == null) return;

            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(remainingTime));
            TimerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private void SubscribeVisibilityEvents()
        {
            InputEvents.OnPointerDown += OnPointerDown;
            VisibilitySubscriptions.Add(() => InputEvents.OnPointerDown -= OnPointerDown);
            InputEvents.OnPointerHold += OnPointerHold;
            VisibilitySubscriptions.Add(() => InputEvents.OnPointerHold -= OnPointerHold);
            InputEvents.OnPointerUp += OnPointerUp;
            VisibilitySubscriptions.Add(() => InputEvents.OnPointerUp -= OnPointerUp);
            InputEvents.OnPointerCancel += OnPointerCancel;
            VisibilitySubscriptions.Add(() => InputEvents.OnPointerCancel -= OnPointerCancel);
            UpgradeEvents.OnOptionsGenerated += OnUpgradeOptionsGenerated;
            VisibilitySubscriptions.Add(() => UpgradeEvents.OnOptionsGenerated -= OnUpgradeOptionsGenerated);
            SkillEvents.OnEquippedSkillsChanged += RefreshSkillHud;
            VisibilitySubscriptions.Add(() => SkillEvents.OnEquippedSkillsChanged -= RefreshSkillHud);

            AddVisibilityListener(Card1Btn, OnCard1Clicked);
            AddVisibilityListener(Card2Btn, OnCard2Clicked);
            AddVisibilityListener(Card3Btn, OnCard3Clicked);
            AddVisibilityListener(TimeScaleButton, OnTimeScaleClicked);
            AddVisibilityListener(_pauseButton, OnPauseClicked);
        }

        private void AddVisibilityListener(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button == null) return;

            button.onClick.AddListener(listener);
            VisibilitySubscriptions.Add(() =>
            {
                if (button != null) button.onClick.RemoveListener(listener);
            });
        }

        private void SetUpgradePanelVisible(bool visible)
        {
            if (UpgradePanel != null) UpgradePanel.gameObject.SetActive(visible);
        }

        private void OnUpgradeOptionsGenerated(IReadOnlyList<SkillConfig> options)
        {
            _currentOptions = options ?? Array.Empty<SkillConfig>();
            if (_currentOptions.Count == 0)
            {
                SetUpgradePanelVisible(false);
                SetUpgradeButtonsActive(false);
                return;
            }

            for (int i = 0; i < _upgradeButtons.Length; i++)
            {
                SetUpgradeButton(i);
            }

            SetUpgradePanelVisible(true);
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
                if (_upgradeButtons[i] != null) _upgradeButtons[i].gameObject.SetActive(active);
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

            Text nameText = FindOrCreateCardText(button.transform, "SkillNameText", new Vector2(0f, 36f), new Vector2(-28f, 44f), 24);
            nameText.text = config.SkillName;
            nameText.color = UPGRADE_CARD_TITLE_COLOR;

            Text descriptionText = FindOrCreateCardText(button.transform, "SkillDecsText", new Vector2(0f, -58f), new Vector2(-28f, 118f), 18);
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
            bool stretchHorizontally = sizeDelta.x < 0f;
            rect.anchorMin = stretchHorizontally ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = stretchHorizontally ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;

            text.font = BabelFont.Default;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = UPGRADE_CARD_BODY_COLOR;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = fontSize;
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
            if (GameSession.IsGameEnded) return;

            EnsureRuntimeHudControls();
            _pausedByButton = !_pausedByButton;
            if (!LegacyRunBridge.TryTogglePause())
            {
                if (_pausedByButton) PresentationTimeScaleAdapter.PauseLegacy();
                else PresentationTimeScaleAdapter.ResumeLegacy();
            }

            UpdatePauseButtonText();
        }

        private void ApplyTimeScale()
        {
            float scale = TIME_SCALES[_timeScaleIndex];
            RunSpeed speed = (RunSpeed)Mathf.RoundToInt(scale);
            if (!LegacyRunBridge.TrySetSpeed(speed))
                PresentationTimeScaleAdapter.ApplyLegacySpeed(scale);

            if (TimeScaleText != null) TimeScaleText.text = $"{scale:0}x";
        }

        private void ResetTimeScale()
        {
            if (GameSession.IsGameEnded) return;

            _timeScaleIndex = 0;
            _pausedByButton = false;
            _timeScaleBeforePause = TIME_SCALES[_timeScaleIndex];
            if (!LegacyRunBridge.IsAvailable) PresentationTimeScaleAdapter.ResetLegacy();
            if (TimeScaleText != null) TimeScaleText.text = "1x";
            UpdatePauseButtonText();
        }

        private void SyncRunControlsFromReadModel()
        {
            if (!LegacyRunBridge.TryGetReadModel(out RunReadModel model) ||
                model.Version == _lastRunReadModelVersion)
                return;

            _lastRunReadModelVersion = model.Version;
            int speedValue = (int)model.Speed;
            for (int i = 0; i < TIME_SCALES.Length; i++)
            {
                if (!Mathf.Approximately(TIME_SCALES[i], speedValue)) continue;
                _timeScaleIndex = i;
                break;
            }

            _pausedByButton = model.Phase == RunPhase.Paused;
            if (TimeScaleText != null) TimeScaleText.text = $"{speedValue:0}x";
            UpdatePauseButtonText();

            if (TimerText != null)
            {
                int totalSeconds = Mathf.Max(0, Mathf.FloorToInt((float)model.RemainingSeconds));
                TimerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
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
            FindOrCreateCardText(button.transform, "SkillNameText", new Vector2(0f, 36f), new Vector2(-28f, 44f), 24);
            FindOrCreateCardText(button.transform, "SkillDecsText", new Vector2(0f, -58f), new Vector2(-28f, 118f), 18);
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
            if (ChargeRing != null) ChargeRing.gameObject.SetActive(false);
            if (ChargeRing_Fill != null) ChargeRing_Fill.fillAmount = 0f;
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


    }
}
