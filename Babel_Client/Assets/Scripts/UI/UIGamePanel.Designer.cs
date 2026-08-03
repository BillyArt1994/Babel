using UnityEngine;
using UnityEngine.UI;

namespace Babel
{
    public partial class UIGamePanel
    {
        [SerializeField] public Text LevelText;
        [SerializeField] public Scrollbar EXPScrollbar;
        [SerializeField] public Image MainSkill_Image;
        [SerializeField] public Image MainSkill_ImageFill;
        [SerializeField] public Button TimeScaleButton;
        [SerializeField] public Text TimeScaleText;
        [SerializeField] public Text TimerText;
        [SerializeField] public Image UpgradePanel;
        [SerializeField] public Button Card1Btn;
        [SerializeField] public Button Card2Btn;
        [SerializeField] public Button Card3Btn;
        [SerializeField] public RectTransform ChargeRing;
        [SerializeField] public Image ChargeRing_Background;
        [SerializeField] public Image ChargeRing_Fill;
    }
}
