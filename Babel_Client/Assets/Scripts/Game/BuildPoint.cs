using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class BuildPoint : ViewController
    {
        [SerializeField] private int buildAmount = 50;
        private int currentBuildProgress = 0;
        public bool isGateway = false;
        public bool IsBuildCompleted = false;
        public bool IsBilding = false;

        [HideInInspector]
        public Path OwnerPath;

        public void AddBuildProgress(int value)
        {
            if (IsBuildCompleted) return;

            IsBilding = true;
            currentBuildProgress += value;
            this.gameObject.SetActive(true);

            if (currentBuildProgress >= buildAmount)
            {
                IsBuildCompleted = true;
                IsBilding = false;
                GetComponent<SpriteRenderer>().color = Color.red;

                if (OwnerPath != null)
                {
                    OwnerPath.OnBuildPointCompleted();
                }
            }
        }
    }
}
