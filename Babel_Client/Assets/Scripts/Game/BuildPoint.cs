using UnityEngine;
using QFramework;

namespace Babel
{
    /// <summary>
    /// 建造点的显示与建造状态。
    /// </summary>
    public enum BuildPointState
    {
        Hidden = 0,
        Building = 1,
        Completed = 2
    }

    public partial class BuildPoint : ViewController
    {
        [SerializeField] private int buildAmount = 50;
        [HideInInspector] public Path OwnerPath;
        public bool isGateway = false;

        /// <summary>
        /// 当前建造点状态。
        /// </summary>
        public BuildPointState State { get; private set; } = BuildPointState.Hidden;
        public bool IsBuildCompleted => State == BuildPointState.Completed;
        public bool IsOccupied { get; private set; }

        private int _currentProgress;
        private readonly System.Collections.Generic.List<IBuildInterruptible> _activeBuilders
            = new System.Collections.Generic.List<IBuildInterruptible>(4);
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
            CacheVisualRenderer();
            EnsureDebugBuildProgressBar();
            ApplyVisualState(State);
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
        }

        /// <summary>
        /// 注册一个正在建造此点的建造者，建造完成时会收到 OnTargetBuildCompleted 回调。
        /// </summary>
        public void AttachBuilder(IBuildInterruptible builder)
        {
            if (builder != null && !_activeBuilders.Contains(builder))
                _activeBuilders.Add(builder);
        }

        /// <summary>
        /// 取消注册建造者（建造者主动放弃或死亡时调用）。
        /// </summary>
        public void DetachBuilder(IBuildInterruptible builder)
        {
            _activeBuilders.Remove(builder);
        }

        /// <summary>
        /// 开始建造该建造点，并显示建造中视觉。
        /// </summary>
        public void BeginBuild()
        {
            if (State == BuildPointState.Hidden)
            {
                SetState(BuildPointState.Building);
            }
        }

        public void AddBuildProgress(int value)
        {
            if (IsBuildCompleted) return;

            BeginBuild();
            _currentProgress += value;

            if (_currentProgress >= buildAmount)
            {
                _currentProgress = buildAmount;
                IsOccupied = false;
                SetState(BuildPointState.Completed);

                // 通知所有在建者：此点已被建完，请中断
                var snapshot = new System.Collections.Generic.List<IBuildInterruptible>(_activeBuilders);
                _activeBuilders.Clear();
                foreach (var b in snapshot)
                    b.OnTargetBuildCompleted(this);

                if (OwnerPath != null)
                    OwnerPath.OnBuildPointCompleted();

                BuildEvents.RaiseBuildCompleted(this);
            }
        }

        public void Reset()
        {
            IsOccupied = false;
            _activeBuilders.Clear();
            _currentProgress = 0;
            SetState(BuildPointState.Hidden);
            ApplyVisualState(State);
        }

        private void SetState(BuildPointState newState)
        {
            if (State == newState)
            {
                return;
            }

            State = newState;
            ApplyVisualState(State);
            BuildEvents.RaiseBuildStateChanged(this, State);
        }

        private void ApplyVisualState(BuildPointState state)
        {
            CacheVisualRenderer();
            bool isVisible = state != BuildPointState.Hidden;

            if (gameObject.activeSelf != isVisible)
            {
                gameObject.SetActive(isVisible);
            }

            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.enabled = isVisible;
            _spriteRenderer.color = state == BuildPointState.Completed ? Color.red : Color.white;
        }

        private void CacheVisualRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
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
