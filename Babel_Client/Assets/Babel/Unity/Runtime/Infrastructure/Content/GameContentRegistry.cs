using UnityEngine;

namespace Babel.Unity.Infrastructure.Content
{
    /// <summary>
    /// Process-local content reference installed by ProjectRoot. Runtime consumers receive
    /// stable Unity object references from the manifest and never resolve hard-coded paths.
    /// </summary>
    public static class GameContentRegistry
    {
        public static GameContentManifest Current { get; private set; }
        public static bool IsReady => Current != null;

        public static void Register(GameContentManifest manifest)
        {
            if (manifest == null) throw new System.ArgumentNullException(nameof(manifest));
            if (Current != null && Current != manifest)
                Debug.LogWarning("[Babel][Content] Replacing the active GameContentManifest.");
            Current = manifest;
        }

        public static void Unregister(GameContentManifest manifest)
        {
            if (Current == manifest) Current = null;
        }

        public static bool TryGetHumanView(string humanId, out GameObject prefab)
        {
            if (Current != null) return Current.TryGetHumanView(humanId, out prefab);
            prefab = null;
            return false;
        }

        public static bool TryGetSkillIcon(string skillId, out Sprite icon)
        {
            if (Current != null) return Current.TryGetSkillIcon(skillId, out icon);
            icon = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Current = null;
        }
    }
}
