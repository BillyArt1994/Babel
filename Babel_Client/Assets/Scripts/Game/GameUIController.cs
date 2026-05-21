using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class GameUIController : ViewController
    {
        private bool _isApplicationQuitting;

        void Start()
        {
            UIKit.OpenPanel<UIGamePanel>();
            // Code Here
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

            UIKit.ClosePanel<UIGamePanel>();
        }
    }
}
