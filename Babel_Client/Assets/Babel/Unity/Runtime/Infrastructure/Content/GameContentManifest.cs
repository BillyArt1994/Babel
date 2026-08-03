using System;
using UnityEngine;

namespace Babel.Unity.Infrastructure.Content
{
    [CreateAssetMenu(fileName = "GameContentManifest", menuName = "Babel/Content/Game Content Manifest")]
    public sealed class GameContentManifest : ScriptableObject
    {
        [Header("Authored data")]
        [SerializeField] private TextAsset _experienceCsv;
        [SerializeField] private TextAsset _enemiesCsv;
        [SerializeField] private TextAsset _wavesCsv;
        [SerializeField] private TextAsset _skillsCsv;

        [Header("Compiled runtime catalogs")]
        [SerializeField] private CompiledGameContent _compiledContent;

        [Header("Shared presentation")]
        [SerializeField] private Font _defaultFont;
        [SerializeField] private GameObject _fallbackHumanView;
        [SerializeField] private Sprite _fallbackSkillIcon;

        [Header("View mappings")]
        [SerializeField] private HumanViewEntry[] _humanViews = Array.Empty<HumanViewEntry>();
        [SerializeField] private SkillIconEntry[] _skillIcons = Array.Empty<SkillIconEntry>();
        [SerializeField] private PoolConfigEntry[] _poolConfigs = Array.Empty<PoolConfigEntry>();

        public TextAsset ExperienceCsv => _experienceCsv;
        public TextAsset EnemiesCsv => _enemiesCsv;
        public TextAsset WavesCsv => _wavesCsv;
        public TextAsset SkillsCsv => _skillsCsv;
        public CompiledGameContent CompiledContent => _compiledContent;
        public Font DefaultFont => _defaultFont;
        public GameObject FallbackHumanView => _fallbackHumanView;
        public Sprite FallbackSkillIcon => _fallbackSkillIcon;

        public bool TryGetHumanView(string humanId, out GameObject prefab)
        {
            for (int i = 0; i < _humanViews.Length; i++)
            {
                if (!string.Equals(_humanViews[i].Id, humanId, StringComparison.Ordinal)) continue;
                prefab = _humanViews[i].Prefab;
                return prefab != null;
            }

            prefab = _fallbackHumanView;
            return prefab != null;
        }

        public bool TryGetSkillIcon(string skillId, out Sprite icon)
        {
            for (int i = 0; i < _skillIcons.Length; i++)
            {
                if (!string.Equals(_skillIcons[i].Id, skillId, StringComparison.Ordinal)) continue;
                icon = _skillIcons[i].Icon;
                return icon != null;
            }

            icon = _fallbackSkillIcon;
            return icon != null;
        }

        public bool TryGetPoolConfig(string viewId, out PoolConfig config)
        {
            for (int i = 0; i < _poolConfigs.Length; i++)
            {
                if (!string.Equals(_poolConfigs[i].ViewId, viewId, StringComparison.Ordinal)) continue;
                config = new PoolConfig(_poolConfigs[i].Prewarm, _poolConfigs[i].ExpectedCapacity, _poolConfigs[i].AllowExpansion);
                return true;
            }

            config = default;
            return false;
        }

        private void OnValidate()
        {
            if (_humanViews == null) _humanViews = Array.Empty<HumanViewEntry>();
            if (_skillIcons == null) _skillIcons = Array.Empty<SkillIconEntry>();
            if (_poolConfigs == null) _poolConfigs = Array.Empty<PoolConfigEntry>();
        }
    }

    [Serializable]
    public struct HumanViewEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private GameObject _prefab;
        public string Id => _id;
        public GameObject Prefab => _prefab;
    }

    [Serializable]
    public struct SkillIconEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private Sprite _icon;
        public string Id => _id;
        public Sprite Icon => _icon;
    }

    [Serializable]
    public struct PoolConfigEntry
    {
        [SerializeField] private string _viewId;
        [SerializeField, Min(0)] private int _prewarm;
        [SerializeField, Min(1)] private int _expectedCapacity;
        [SerializeField] private bool _allowExpansion;
        public string ViewId => _viewId;
        public int Prewarm => _prewarm;
        public int ExpectedCapacity => _expectedCapacity;
        public bool AllowExpansion => _allowExpansion;
    }

    public readonly struct PoolConfig
    {
        public PoolConfig(int prewarm, int expectedCapacity, bool allowExpansion)
        {
            Prewarm = prewarm;
            ExpectedCapacity = expectedCapacity;
            AllowExpansion = allowExpansion;
        }

        public int Prewarm { get; }
        public int ExpectedCapacity { get; }
        public bool AllowExpansion { get; }
    }
}
