using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Upgrade selection UI. Subscribes in Awake/OnDestroy so the listener
/// survives Hide() toggling _root inactive.
/// Cards are dynamically instantiated from a prefab instead of hard-coded arrays.
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Image _overlay;
    [SerializeField] private Transform _cardContainer;
    [SerializeField] private UpgradeCardUI _cardPrefab;

    private readonly List<UpgradeCardUI> _spawnedCards = new List<UpgradeCardUI>();
    private SkillData[] _currentOptions;
    private bool _accepted;

    private void Awake()
    {
        UpgradeEvents.OnOptionsGenerated += OnOptionsGenerated;
    }

    private void OnDestroy()
    {
        UpgradeEvents.OnOptionsGenerated -= OnOptionsGenerated;
    }

    private void Start()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void ClearCards()
    {
        foreach (UpgradeCardUI card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        _spawnedCards.Clear();
    }

    private void OnOptionsGenerated(SkillData[] options)
    {
        _currentOptions = options;
        _accepted = false;

        ClearCards();

        if (options != null)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null) continue;

                UpgradeCardUI card = Instantiate(_cardPrefab, _cardContainer);
                var le = card.gameObject.AddComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.flexibleHeight = 1f;
                card.Setup(options[i], i, this);
                _spawnedCards.Add(card);
            }
        }

        if (_root != null) _root.SetActive(true);
    }

    public void OnCardClicked(int index)
    {
        if (_accepted) return;
        _accepted = true;

        ClearCards();

        if (_root != null) _root.SetActive(false);

        if (UpgradeSystem.Instance != null)
            UpgradeSystem.Instance.SelectOption(index);
    }
}
