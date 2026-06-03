using UnityEngine;
using UnityEngine.UI;

namespace Babel
{
    /// <summary>
    /// 单个被动技能 HUD 图标视图，负责显示图标与层数角标。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PassiveSkillIconView : MonoBehaviour
    {
        private const float ICON_SIZE = 36f;
        private const float BADGE_SIZE = 18f;

        private Image _iconImage;
        private Text _stackBadge;

        private void Awake()
        {
            EnsureHierarchy();
        }

        /// <summary>
        /// 使用技能配置刷新图标显示。
        /// </summary>
        /// <param name="config">被动技能配置。</param>
        /// <param name="stackCount">技能层数。</param>
        public void Configure(SkillConfig config, int stackCount)
        {
            EnsureHierarchy();
            _iconImage.sprite = SkillIconLoader.LoadIcon(config);
            _stackBadge.gameObject.SetActive(stackCount > 1);
            _stackBadge.text = stackCount.ToString();
        }

        private void EnsureHierarchy()
        {
            RectTransform rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);
            _iconImage ??= GetComponent<Image>();
            if (_iconImage == null)
            {
                _iconImage = gameObject.AddComponent<Image>();
            }

            _stackBadge ??= GetComponentInChildren<Text>(true);
            if (_stackBadge != null)
            {
                return;
            }

            GameObject badgeObject = new GameObject("StackBadge", typeof(RectTransform), typeof(Text));
            badgeObject.transform.SetParent(transform, false);
            RectTransform badgeRect = (RectTransform)badgeObject.transform;
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(1f, 0f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.sizeDelta = new Vector2(BADGE_SIZE, BADGE_SIZE);

            _stackBadge = badgeObject.GetComponent<Text>();
            _stackBadge.alignment = TextAnchor.MiddleCenter;
            _stackBadge.font = BabelFont.Default;
            _stackBadge.fontSize = 12;
            _stackBadge.color = Color.white;
        }
    }
}
