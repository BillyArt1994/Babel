using System;

namespace Babel
{
    public static class BuildEvents
    {
        public static event Action<BuildPoint> OnBuildStarted;
        public static event Action<BuildPoint> OnBuildCompleted;
        /// <summary>
        /// 建造点状态切换时触发。
        /// </summary>
        public static event Action<BuildPoint, BuildPointState> OnBuildStateChanged;
        public static event Action<Path> OnLayerCompleted;

        public static void RaiseBuildStarted(BuildPoint bp) => OnBuildStarted?.Invoke(bp);
        public static void RaiseBuildCompleted(BuildPoint bp) => OnBuildCompleted?.Invoke(bp);
        public static void RaiseBuildStateChanged(BuildPoint bp, BuildPointState state) =>
            OnBuildStateChanged?.Invoke(bp, state);
        public static void RaiseLayerCompleted(Path path) => OnLayerCompleted?.Invoke(path);
    }
}
