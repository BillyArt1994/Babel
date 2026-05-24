using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 世界空间调试条基类，用 SpriteRenderer 显示归一化进度。
    /// </summary>
    public abstract class DebugWorldBar : MonoBehaviour
    {
        private const float BAR_WIDTH = 1.2f;
        private const float BAR_HEIGHT = 0.12f;
        private const int SORTING_ORDER = 1000;

        private static Sprite _sharedSprite;

        private Transform _fillTransform;
        private SpriteRenderer _backgroundRenderer;
        private SpriteRenderer _fillRenderer;

        protected virtual Vector3 LocalOffset => Vector3.up * 1.0f;
        protected abstract Color FillColor { get; }
        protected abstract float FillPercent { get; }

        protected virtual void Awake()
        {
            CreateRenderers();
        }

        protected virtual void LateUpdate()
        {
            UpdateFill(FillPercent);
        }

        protected void CreateRenderers()
        {
            transform.localPosition = LocalOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            _backgroundRenderer = CreateSegment("Background", Color.black, BAR_WIDTH, BAR_HEIGHT, 0f);
            _fillRenderer = CreateSegment("Fill", FillColor, BAR_WIDTH, BAR_HEIGHT, -0.01f);
            _fillTransform = _fillRenderer.transform;
            UpdateFill(FillPercent);
        }

        /// <summary>
        /// 设置调试条所有渲染段的可见性。
        /// </summary>
        /// <param name="isVisible">是否显示调试条。</param>
        protected void SetVisible(bool isVisible)
        {
            if (_backgroundRenderer != null)
            {
                _backgroundRenderer.enabled = isVisible;
            }

            if (_fillRenderer != null)
            {
                _fillRenderer.enabled = isVisible;
            }
        }

        private static Sprite GetSharedSprite()
        {
            if (_sharedSprite != null)
            {
                return _sharedSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _sharedSprite.hideFlags = HideFlags.HideAndDontSave;
            return _sharedSprite;
        }

        private SpriteRenderer CreateSegment(string segmentName, Color color, float width, float height, float zOffset)
        {
            var segment = new GameObject(segmentName);
            segment.transform.SetParent(transform, false);
            segment.transform.localPosition = new Vector3(0f, 0f, zOffset);
            segment.transform.localScale = new Vector3(width, height, 1f);

            SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSharedSprite();
            renderer.color = color;
            renderer.sortingOrder = SORTING_ORDER;
            return renderer;
        }

        private void UpdateFill(float percent)
        {
            if (_fillTransform == null)
            {
                return;
            }

            float clampedPercent = Mathf.Clamp01(percent);
            float fillWidth = BAR_WIDTH * clampedPercent;
            _fillTransform.localScale = new Vector3(fillWidth, BAR_HEIGHT, 1f);
            _fillTransform.localPosition = new Vector3(
                -BAR_WIDTH * 0.5f + fillWidth * 0.5f,
                0f,
                -0.01f);
        }
    }
}
