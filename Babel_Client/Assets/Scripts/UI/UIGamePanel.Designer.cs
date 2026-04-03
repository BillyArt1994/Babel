using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Babel
{
	// Generate Id:78536547-017b-4149-8bb4-841af4ef95c8
	public partial class UIGamePanel
	{
		public const string Name = "UIGamePanel";
		
		[SerializeField]
		public UnityEngine.UI.Text LevelText;
		[SerializeField]
		public UnityEngine.UI.Scrollbar EXPScrollbar;
		[SerializeField]
		public UnityEngine.UI.Image MainSkill_Image;
		[SerializeField]
		public UnityEngine.UI.Image MainSkill_ImageFill;
		[SerializeField]
		public UnityEngine.UI.Button TimeScaleButton;
		[SerializeField]
		public UnityEngine.UI.Text TimeScaleText;
		[SerializeField]
		public UnityEngine.UI.Text TimerText;
		[SerializeField]
		public UnityEngine.UI.Image UpgradePanel;
		[SerializeField]
		public UnityEngine.UI.Button Card1Btn;
		[SerializeField]
		public UnityEngine.UI.Button Card2Btn;
		[SerializeField]
		public UnityEngine.UI.Button Card3Btn;
		
		private UIGamePanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			LevelText = null;
			EXPScrollbar = null;
			MainSkill_Image = null;
			MainSkill_ImageFill = null;
			TimeScaleButton = null;
			TimeScaleText = null;
			TimerText = null;
			UpgradePanel = null;
			Card1Btn = null;
			Card2Btn = null;
			Card3Btn = null;
			
			mData = null;
		}
		
		public UIGamePanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		UIGamePanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new UIGamePanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
