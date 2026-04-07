using UnityEngine;
using UnityEngine.UI;
using QFramework;
using System;
using Unity.VisualScripting;

namespace Babel
{
    public class UIGamePanelData : UIPanelData
    {
    }
    public partial class UIGamePanel : UIPanel
    {

        private Canvas _canvas;
        private RectTransform _panelRectTransform;

        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as UIGamePanelData ?? new UIGamePanelData();

            _canvas = GetComponentInParent<Canvas>();
            _panelRectTransform = transform as RectTransform;
            ChargeRing.gameObject.SetActive(false);
            ChargeRing_Fill.fillAmount = 0;

            // please add init code here
            Global.Exp.RegisterWithInitValue(exp =>
            {
                var num = exp / 5.0f;
                EXPScrollbar.size = num - MathF.Truncate(exp / 5.0f);
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

           Global.Exp.RegisterWithInitValue(exp =>
           {
               if (exp >= 5) 
               {
                   Global.Level.Value++;
                   Global.Exp.Value -= 5;
               }
           }).UnRegisterWhenGameObjectDestroyed(gameObject);

            Global.Level.Register(Level =>
            {
                LevelText.text = "LV:" + (Level).ToString();
                Time.timeScale = 0;
                UpgradePanel.Show();
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
        }

        protected override void OnShow()
        {

        }

        protected override void OnHide()
        {

        }

        protected override void OnClose()
        {
            InputEvents.OnPointerDown -= OnPointerDown;
            InputEvents.OnPointerHold -= OnPointerHold;
            InputEvents.OnPointerUp -= OnPointerUp;
            InputEvents.OnPointerCancel -= OnPointerCancel;
        }
        private void OnPointerDown(PointerInputContext context)
        {
            ChargeRing.gameObject.SetActive(true);
            UpdateChargeRingPosition(context.ScreenPosition);
            ChargeRing_Fill.fillAmount = 0f;
        }

        private void OnPointerHold(PointerInputContext context)
        {
            UpdateChargeRingPosition(context.ScreenPosition);
            ChargeRing_Fill.fillAmount = context.ChargeRatio;
        }

        private void OnPointerUp(PointerInputContext context)
        {
            ChargeRing.gameObject.SetActive(false);
            ChargeRing_Fill.fillAmount = 0f;
        }

        private void OnPointerCancel(PointerInputContext context)
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


    }
}
