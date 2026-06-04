namespace Babel
{
    /// <summary>
    /// 敌人移动策略契约。对称 IEnemyAbility。
    /// </summary>
    public interface IEnemyMovement
    {
        /// <summary>由 Enemy.Init 调用，注入宿主和数据。</summary>
        void Init(Enemy owner, EnemyData data);

        /// <summary>每帧由 Enemy.Update 驱动，deltaTime = Time.deltaTime。</summary>
        void Tick(float deltaTime);

        /// <summary>true 时 Animator IsMoving = true。</summary>
        bool IsMoving { get; }

        /// <summary>Enemy 死亡/销毁时调用，用于释放预约/监听。</summary>
        void OnRemoved();
    }
}
