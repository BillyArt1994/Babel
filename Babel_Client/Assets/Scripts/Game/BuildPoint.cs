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

        /// <summary>
        /// 当前建筑进度，供调试 UI 读取。
        /// </summary>
        public int CurrentProgress => _currentProgress;

        /// <summary>
        /// 完成该建筑点所需总进度。
        /// </summary>
        public int RequiredProgress => buildAmount;

        /// <summary>
        /// 当前建筑进度比例 [0, 1]。
        /// </summary>
        public float BuildProgressPercent => buildAmount > 0
            ? Mathf.Clamp01((float)_currentProgress / buildAmount)
            : 0f;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureDebugBuildProgressBar();
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

        private void EnsureDebugBuildProgressBar()
        {
            if (GetComponentInChildren<DebugBuildProgressBar>(true) != null)
            {
                return;
            }

            var barObject = new GameObject("DebugBuildProgressBar");
            barObject.transform.SetParent(transform, false);
            barObject.AddComponent<DebugBuildProgressBar>().Init(this);
        }
    }
}
