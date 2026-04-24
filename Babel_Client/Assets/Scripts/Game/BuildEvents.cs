using System;

namespace Babel
{
    public static class BuildEvents
    {
        public static event Action<BuildPoint> OnBuildStarted;
        public static event Action<BuildPoint> OnBuildCompleted;
        public static event Action<Path> OnLayerCompleted;

        public static void RaiseBuildStarted(BuildPoint bp) => OnBuildStarted?.Invoke(bp);
        public static void RaiseBuildCompleted(BuildPoint bp) => OnBuildCompleted?.Invoke(bp);
        public static void RaiseLayerCompleted(Path path) => OnLayerCompleted?.Invoke(path);
    }
}
