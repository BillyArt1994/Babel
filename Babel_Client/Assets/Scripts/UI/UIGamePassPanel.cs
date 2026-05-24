using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Babel
{
	public class UIGamePassPanelData : UIPanelData
	{
	}
	public partial class UIGamePassPanel : UIPanel
	{
		protected override void OnInit(IUIData uiData = null)
		{
			mData = uiData as UIGamePassPanelData ?? new UIGamePassPanelData();
			// please add init code here
		}
		
		protected override void OnOpen(IUIData uiData = null)
		{
			SettlementPanelRuntime.Configure(
				transform,
				GameSession.Result,
				RestartFromSettlement,
				ReturnToMenuFromSettlement);
		}

		private void RestartFromSettlement()
		{
			CloseBeforeSceneTransition();
			GameSession.RestartGame();
		}

		private void ReturnToMenuFromSettlement()
		{
			CloseBeforeSceneTransition();
			GameSession.ReturnToMainMenu();
		}

		private void CloseBeforeSceneTransition()
		{
			gameObject.SetActive(false);
			if (Application.isPlaying)
			{
				CloseSelf();
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
		}
	}
}
