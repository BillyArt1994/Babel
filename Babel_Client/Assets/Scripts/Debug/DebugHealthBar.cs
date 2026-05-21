using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 敌人头顶调试血条。
    /// </summary>
    public sealed class DebugHealthBar : DebugWorldBar
    {
        private Enemy _enemy;

        protected override Color FillColor => Color.green;
        protected override float FillPercent => _enemy != null ? _enemy.HealthPercent : 0f;

        /// <summary>
        /// 绑定要显示生命值的敌人。
        /// </summary>
        /// <param name="enemy">敌人实例。</param>
        public void Init(Enemy enemy)
        {
            _enemy = enemy;
        }

        protected override void Awake()
        {
            if (_enemy == null)
            {
                _enemy = GetComponentInParent<Enemy>();
            }

            base.Awake();
        }
    }
}
