using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class BuildPoint : ViewController
    {
        [SerializeField] private int buildAmount = 50;
        [HideInInspector] public Path OwnerPath;
        public bool isGateway = false;

        public bool IsBuildCompleted { get; private set; }
        public bool IsOccupied { get; private set; }

        private int _currentProgress;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
        }

        public void AddBuildProgress(int value)
        {
            if (IsBuildCompleted) return;

            _currentProgress += value;

            if (_currentProgress >= buildAmount)
            {
                IsBuildCompleted = true;
                IsOccupied = false;
                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.red;

                if (OwnerPath != null)
                    OwnerPath.OnBuildPointCompleted();

                BuildEvents.RaiseBuildCompleted(this);
            }
        }

        public void Reset()
        {
            IsBuildCompleted = false;
            IsOccupied = false;
            _currentProgress = 0;
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
        }
    }
}
