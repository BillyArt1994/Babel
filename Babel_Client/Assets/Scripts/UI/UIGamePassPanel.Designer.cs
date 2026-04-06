using System;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace Babel
{
	// Generate Id:ef7bad61-c625-46b6-b0d7-e871656a47eb
	public partial class UIGamePassPanel
	{
		public const string Name = "UIGamePassPanel";
		
		
		private UIGamePassPanelData mPrivateData = null;
		
		protected override void ClearUIComponents()
		{
			
			mData = null;
		}
		
		public UIGamePassPanelData Data
		{
			get
			{
				return mData;
			}
		}
		
		UIGamePassPanelData mData
		{
			get
			{
				return mPrivateData ?? (mPrivateData = new UIGamePassPanelData());
			}
			set
			{
				mUIData = value;
				mPrivateData = value;
			}
		}
	}
}
