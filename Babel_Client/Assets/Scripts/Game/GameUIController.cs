using UnityEngine;
using QFramework;

namespace Babel
{
	public partial class GameUIController : ViewController
	{
		void Start()
		{
			UIKit.OpenPanel<UIGamePanel>();
			// Code Here
		}

        private void OnDestroy()
        {
            UIKit.ClosePanel<UIGamePanel>();
        }
    }
}
