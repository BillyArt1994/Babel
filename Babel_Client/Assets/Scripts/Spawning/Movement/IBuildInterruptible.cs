namespace Babel
{
    /// <summary>
    /// 建造打断回调接口。注册到 BuildPoint._activeBuilders 的建造者需实现此接口。
    /// </summary>
    public interface IBuildInterruptible
    {
        /// <summary>
        /// 当前正在建造的 BuildPoint 已被其他人建完时触发。
        /// 实现者应立即退出 Building 状态，选下一个目标。
        /// </summary>
        void OnTargetBuildCompleted(BuildPoint point);
    }
}
