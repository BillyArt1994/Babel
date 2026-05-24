using QFramework;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 独立主菜单场景入口，负责打开主菜单 QFramework 面板。
    /// </summary>
    public class MainMenuController : ViewController
    {
        private bool _isApplicationQuitting;

        private void Start()
        {
            UIKit.OpenPanel<UIMainMenuPanel>();
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (_isApplicationQuitting)
            {
                return;
            }

            UIKit.ClosePanel<UIMainMenuPanel>();
        }
    }
}
