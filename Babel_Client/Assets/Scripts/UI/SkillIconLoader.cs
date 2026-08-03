using Babel.Unity.Infrastructure.Content;
using UnityEngine;

namespace Babel
{
    public static class SkillIconLoader
    {
        private static Sprite _fallbackSprite;

        public static Sprite LoadIcon(SkillConfig config)
        {
            if (config != null &&
                GameContentRegistry.TryGetSkillIcon(config.SkillId, out Sprite icon) &&
                icon != null)
                return icon;

            GameContentManifest manifest = GameContentRegistry.Current;
            if (manifest != null && manifest.FallbackSkillIcon != null)
                return manifest.FallbackSkillIcon;

            return GetGeneratedFallbackSprite();
        }

        private static Sprite GetGeneratedFallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;

            _fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));
            _fallbackSprite.name = "FallbackSkillIcon";
            return _fallbackSprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _fallbackSprite = null;
        }
    }
}
