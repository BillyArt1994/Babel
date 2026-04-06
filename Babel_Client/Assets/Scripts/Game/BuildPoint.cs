using UnityEngine;
using QFramework;
using UnityEngine.UIElements;

namespace Babel
{
    public partial class BuildPoint : ViewController
    {
        private int buildAmount = 50;
        private int currentBuildProgress = 0;
        public bool isGateway = false;
        public bool IsBuildCompleted = false;
        public bool IsBilding = false;


        public void AddBuildProgress(int value)
        {
            IsBilding = true;
            currentBuildProgress += value;
            this.gameObject.SetActive(true);
            if (currentBuildProgress >= buildAmount)
            {
                IsBuildCompleted = true;
                GetComponent<SpriteRenderer>().color = Color.red;
            }
        }
    }
}
