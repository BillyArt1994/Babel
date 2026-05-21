using UnityEngine;
using UnityEngine.UI;
using QFramework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

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
        private readonly Button[] _upgradeButtons = new Button[3];
        private IReadOnlyList<SkillConfig> _currentOptions = Array.Empty<SkillConfig>();
        private int _timeScaleIndex;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as UIGamePanelData ?? new UIGamePanelData();

            _canvas = GetComponentInParent<Canvas>();
            _panelRectTransform = transform as RectTransform;
            _upgradeButtons[0] = Card1Btn;
            _upgradeButtons[1] = Card2Btn;
            _upgradeButtons[2] = Card3Btn;
            ChargeRing.gameObject.SetActive(false);
            ChargeRing_Fill.fillAmount = 0;
            UpdateMainSkillCooldownFill();
            ResetTimeScale();

            // please add init code here
            Global.Exp.RegisterWithInitValue(exp =>
            {
                var num = exp / 5.0f;
                EXPScrollbar.size = num - MathF.Truncate(exp / 5.0f);
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

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
                Global.CurrentTime.Value -= Time.deltaTime;
                UpdateMainSkillCooldownFill();
                if (Global.CurrentTime.Value <= 0)
                {
                    UIKit.OpenPanel<UIGamePassPanel>();
                }
            }).UnRegisterWhenGameObjectDestroyed(gameObject);



        }

        protected override void OnOpen(IUIData uiData = null)
        {
            InputEvents.OnPointerDown += OnPointerDown;
            InputEvents.OnPointerHold += OnPointerHold;
            InputEvents.OnPointerUp += OnPointerUp;
            InputEvents.OnPointerCancel += OnPointerCancel;
            UpgradeEvents.OnOptionsGenerated += OnUpgradeOptionsGenerated;
            Card1Btn.onClick.AddListener(OnCard1Clicked);
            Card2Btn.onClick.AddListener(OnCard2Clicked);
            Card3Btn.onClick.AddListener(OnCard3Clicked);
            if (TimeScaleButton != null)
            {
                TimeScaleButton.onClick.AddListener(OnTimeScaleClicked);
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
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = $"{config.SkillName}\n{config.Description}";
            }
        }

        private void SetUpgradeButtonsActive(bool active)
        {
            for (int i = 0; i < _upgradeButtons.Length; i++)
            {
                _upgradeButtons[i].gameObject.SetActive(active);
            }
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
            _timeScaleIndex = GetNextTimeScaleIndex(_timeScaleIndex, TIME_SCALES.Length);
            ApplyTimeScale();
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
            _timeScaleIndex = 0;
            Time.timeScale = TIME_SCALES[_timeScaleIndex];
            if (TimeScaleText != null)
            {
                TimeScaleText.text = "1x";
            }
        }

        private static int GetNextTimeScaleIndex(int currentIndex, int scaleCount)
        {
            return (currentIndex + 1) % scaleCount;
        }

        private void OnPointerDown(PointerInputContext context)
        {
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
            HideChargeRing();
        }

        private void OnPointerCancel(PointerInputContext context)
        {
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


    }
}
