using Babel.Unity.Presentation.UI;

namespace Babel
{
    public partial class UIGameOverPanel : Babel.Unity.Presentation.UI.Screen
    {
        protected override void OnScreenShown()
        {
            SettlementPanelRuntime.Configure(
                transform,
                GameSession.Result,
                GameSession.RestartGame,
                GameSession.ReturnToMainMenu);
        }
    }
}
