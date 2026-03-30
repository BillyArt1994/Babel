using UnityEngine;
using UnityEngine.UI;

// Attach to each card root; set cardIndex in Inspector (0/1/2).
// Hooks its Button.onClick to UpgradeSelectionUI.OnCardClicked(cardIndex).
[RequireComponent(typeof(Button))]
public class UpgradeCardClickHandler : MonoBehaviour
{
    [SerializeField] private int _cardIndex;
    [SerializeField] private UpgradeSelectionUI _upgradeUI;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => _upgradeUI.OnCardClicked(_cardIndex));
    }
}
