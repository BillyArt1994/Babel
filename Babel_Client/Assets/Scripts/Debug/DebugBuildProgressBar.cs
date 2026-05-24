using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 建筑点上方的调试建筑度条。
    /// </summary>
    public sealed class DebugBuildProgressBar : DebugWorldBar
    {
        private BuildPoint _buildPoint;

        protected override Vector3 LocalOffset => Vector3.up * 0.45f;
        protected override Color FillColor => Color.yellow;
        protected override float FillPercent => _buildPoint != null ? _buildPoint.BuildProgressPercent : 0f;

        /// <summary>
        /// 绑定要显示建筑度的建筑点。
        /// </summary>
        /// <param name="buildPoint">建筑点实例。</param>
        public void Init(BuildPoint buildPoint)
        {
            _buildPoint = buildPoint;
        }

        protected override void Awake()
        {
            if (_buildPoint == null)
            {
                _buildPoint = GetComponentInParent<BuildPoint>();
            }

            base.Awake();
            SetVisible(ShouldShow());
        }

        protected override void LateUpdate()
        {
            SetVisible(ShouldShow());
            base.LateUpdate();
        }

        private bool ShouldShow()
        {
            return _buildPoint != null && _buildPoint.State != BuildPointState.Hidden;
        }
    }
}
