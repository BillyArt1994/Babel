using Babel.Unity.Infrastructure.Content;
using UnityEngine;

namespace Babel
{
    public static class BabelFont
    {
        private static Font _cachedFont;
        private static bool _resolved;

        public static Font Default
        {
            get
            {
                if (_resolved && _cachedFont != null) return _cachedFont;

                GameContentManifest manifest = GameContentRegistry.Current;
                _cachedFont = manifest == null ? null : manifest.DefaultFont;
                if (_cachedFont == null)
                {
                    Debug.LogWarning("[Babel][Font] Manifest font unavailable; using Unity built-in font.");
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                _resolved = true;
                return _cachedFont;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedFont = null;
            _resolved = false;
        }
    }
}
