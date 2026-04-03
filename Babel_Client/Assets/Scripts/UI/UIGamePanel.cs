using UnityEngine;
using UnityEngine.UI;
using QFramework;
using System;

namespace Babel
{
    public class UIGamePanelData : UIPanelData
    {
    }
    public partial class UIGamePanel : UIPanel
    {
        protected override void OnInit(IUIData uiData = null)
        {
            mData = uiData as UIGamePanelData ?? new UIGamePanelData();
            // please add init code here
            Global.Exp.RegisterWithInitValue(exp =>
            {
                EXPScrollbar.value = exp % 20;
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            Global.Exp.RegisterWithInitValue(level =>
            {
                LevelText.text = "Lv:" + level.ToString();
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            Global.Exp.RegisterWithInitValue(exp =>
            {
                Global.Level.Value = (int)Mathf.Floor(Global.Exp.Value / 20.0f);
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            Global.Level.Register(Level =>
            {
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
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        protected override void OnOpen(IUIData uiData = null)
        {

        }

        protected override void OnShow()
        {

        }

        protected override void OnHide()
        {

        }

        protected override void OnClose()
        {

        }
    }
}
