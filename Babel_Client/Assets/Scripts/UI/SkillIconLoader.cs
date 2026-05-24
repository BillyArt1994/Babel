using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 负责从 Resources 加载技能图标，并在资源缺失时提供占位图。
    /// </summary>
    public static class SkillIconLoader
    {
        private static Sprite _fallbackSprite;

        /// <summary>
        /// 加载技能图标；若配置为空或资源不存在，则返回 fallback 图标。
        /// </summary>
        /// <param name="config">技能配置。</param>
        /// <returns>可用于 UI 显示的技能图标。</returns>
        public static Sprite LoadIcon(SkillConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.IconPath))
            {
                Sprite sprite = Resources.Load<Sprite>(config.IconPath);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return GetFallbackSprite();
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null)
            {
                return _fallbackSprite;
            }

            _fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));
            _fallbackSprite.name = "FallbackSkillIcon";
            return _fallbackSprite;
        }
    }
}
