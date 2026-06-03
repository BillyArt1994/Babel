using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 统一提供运行时 UI 文本使用的字体。
    /// 默认加载项目内置的中文字体（Resources/Fonts/NotoSansSC），
    /// 缺失时回退到 Unity 内置字体，保证文本始终可渲染。
    /// </summary>
    public static class BabelFont
    {
        private const string CHINESE_FONT_RESOURCE_PATH = "Fonts/NotoSansSC";

        private static Font _cachedFont;
        private static bool _resolved;

        /// <summary>
        /// 获取默认 UI 字体（支持中文）。失败时回退到内置字体。
        /// </summary>
        public static Font Default
        {
            get
            {
                if (_resolved && _cachedFont != null)
                {
                    return _cachedFont;
                }

                _cachedFont = Resources.Load<Font>(CHINESE_FONT_RESOURCE_PATH);
                if (_cachedFont == null)
                {
                    Debug.LogWarning($"[BABEL][BabelFont] Missing Chinese font at Resources/{CHINESE_FONT_RESOURCE_PATH}, falling back to built-in font.");
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                _resolved = true;
                return _cachedFont;
            }
        }
    }
}
