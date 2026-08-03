using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public sealed class HumanCatalog
    {
        private readonly Dictionary<string, HumanDefinition> _byId;
        private readonly ReadOnlyCollection<HumanDefinition> _all;

        public HumanCatalog(IEnumerable<HumanDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<string, HumanDefinition>(StringComparer.Ordinal);
            var all = new List<HumanDefinition>();
            foreach (HumanDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Catalog entries cannot be null.", nameof(definitions));
                if (!_byId.TryAdd(definition.Id, definition))
                    throw new ArgumentException($"Duplicate human ID '{definition.Id}'.", nameof(definitions));
                all.Add(definition);
            }

            _all = all.AsReadOnly();
        }

        public int Count => _all.Count;
        public IReadOnlyList<HumanDefinition> All => _all;

        public bool Contains(string id) => TryGet(id, out _);

        public bool TryGet(string id, out HumanDefinition definition)
        {
            if (string.IsNullOrEmpty(id))
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }

        public HumanDefinition GetRequired(string id)
        {
            if (!TryGet(id, out HumanDefinition definition))
                throw new KeyNotFoundException($"Unknown human ID '{id}'.");
            return definition;
        }
    }
}
