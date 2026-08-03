using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public sealed class WaveCatalog
    {
        private readonly Dictionary<string, WaveDefinition> _byId;
        private readonly ReadOnlyCollection<WaveDefinition> _all;

        public WaveCatalog(IEnumerable<WaveDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<string, WaveDefinition>(StringComparer.Ordinal);
            var all = new List<WaveDefinition>();
            foreach (WaveDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Catalog entries cannot be null.", nameof(definitions));
                if (!_byId.TryAdd(definition.Id, definition))
                    throw new ArgumentException($"Duplicate wave ID '{definition.Id}'.", nameof(definitions));
                all.Add(definition);
            }

            _all = all.AsReadOnly();
        }

        public int Count => _all.Count;
        public IReadOnlyList<WaveDefinition> All => _all;

        public bool TryGet(string id, out WaveDefinition definition)
        {
            if (string.IsNullOrEmpty(id))
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }

        public WaveDefinition GetRequired(string id)
        {
            if (!TryGet(id, out WaveDefinition definition))
                throw new KeyNotFoundException($"Unknown wave ID '{id}'.");
            return definition;
        }

        public void Validate(HumanCatalog humans)
        {
            if (humans == null) throw new ArgumentNullException(nameof(humans));
            for (int waveIndex = 0; waveIndex < _all.Count; waveIndex++)
            {
                WaveDefinition wave = _all[waveIndex];
                for (int poolIndex = 0; poolIndex < wave.Pool.Count; poolIndex++)
                {
                    string humanId = wave.Pool[poolIndex].HumanId;
                    if (!humans.Contains(humanId))
                        throw new InvalidOperationException($"Wave '{wave.Id}' references unknown human '{humanId}'.");
                }
            }
        }
    }
}
