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
