using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Babel.Gameplay.Content
{
    public sealed class SkillCatalog
    {
        private readonly Dictionary<SkillKey, SkillDefinition> _byKey;
        private readonly Dictionary<string, ReadOnlyCollection<SkillDefinition>> _byId;
        private readonly ReadOnlyCollection<SkillDefinition> _all;

        public SkillCatalog(IEnumerable<SkillDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _byKey = new Dictionary<SkillKey, SkillDefinition>();
            var mutableById = new Dictionary<string, List<SkillDefinition>>(StringComparer.Ordinal);
            var all = new List<SkillDefinition>();
            foreach (SkillDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Catalog entries cannot be null.", nameof(definitions));

                var key = new SkillKey(definition.Id, definition.Level);
                if (!_byKey.TryAdd(key, definition))
                    throw new ArgumentException($"Duplicate skill key '({definition.Id}, {definition.Level})'.", nameof(definitions));

                if (!mutableById.TryGetValue(definition.Id, out List<SkillDefinition> levels))
                {
                    levels = new List<SkillDefinition>();
                    mutableById.Add(definition.Id, levels);
                }

                levels.Add(definition);
                all.Add(definition);
            }

            _byId = new Dictionary<string, ReadOnlyCollection<SkillDefinition>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<SkillDefinition>> pair in mutableById)
            {
                pair.Value.Sort((left, right) => left.Level.CompareTo(right.Level));
                _byId.Add(pair.Key, pair.Value.AsReadOnly());
            }

            _all = all.AsReadOnly();
        }

        public int Count => _all.Count;
        public IReadOnlyList<SkillDefinition> All => _all;

        public bool Contains(string id) => !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);

        public bool TryGet(string id, int level, out SkillDefinition definition)
        {
            if (string.IsNullOrEmpty(id) || level <= 0)
            {
                definition = null;
                return false;
            }

            return _byKey.TryGetValue(new SkillKey(id, level), out definition);
        }

        public SkillDefinition GetRequired(string id, int level)
        {
            if (!TryGet(id, level, out SkillDefinition definition))
                throw new KeyNotFoundException($"Unknown skill key '({id}, {level})'.");
            return definition;
        }

        public IReadOnlyList<SkillDefinition> GetLevels(string id)
        {
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out ReadOnlyCollection<SkillDefinition> levels))
                return Array.Empty<SkillDefinition>();
            return levels;
        }

        public void Validate()
        {
            foreach (KeyValuePair<string, ReadOnlyCollection<SkillDefinition>> pair in _byId)
            {
                IReadOnlyList<SkillDefinition> levels = pair.Value;
                int expectedMaxLevel = levels[0].MaxLevel;
                if (levels[0].Level != 1)
                    throw new InvalidOperationException($"Skill '{pair.Key}' is missing level 1.");
                if (levels.Count != expectedMaxLevel)
                    throw new InvalidOperationException($"Skill '{pair.Key}' declares {expectedMaxLevel} levels but defines {levels.Count}.");

                for (int i = 0; i < levels.Count; i++)
                {
                    SkillDefinition definition = levels[i];
                    int expectedLevel = i + 1;
                    if (definition.Level != expectedLevel)
                        throw new InvalidOperationException($"Skill '{pair.Key}' is missing level {expectedLevel}.");
                    if (definition.MaxLevel != expectedMaxLevel)
                        throw new InvalidOperationException($"Skill '{pair.Key}' has inconsistent MaxLevel values.");

                    if (definition.UpgradesFrom.Length == 0) continue;
                    if (string.Equals(definition.UpgradesFrom, definition.Id, StringComparison.Ordinal))
                        throw new InvalidOperationException($"Skill '{definition.Id}' cannot upgrade from itself.");
                    if (!_byId.ContainsKey(definition.UpgradesFrom))
                        throw new InvalidOperationException($"Skill '{definition.Id}' references unknown source skill '{definition.UpgradesFrom}'.");
                }
            }

            ValidateEvolutionCycles();
        }

        private void ValidateEvolutionCycles()
        {
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SkillDefinition definition in _all)
            {
                if (definition.UpgradesFrom.Length == 0) continue;
                if (sources.TryGetValue(definition.Id, out string existing) &&
                    !string.Equals(existing, definition.UpgradesFrom, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Skill '{definition.Id}' has inconsistent evolution sources.");
                sources[definition.Id] = definition.UpgradesFrom;
            }

            foreach (string id in sources.Keys)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                string current = id;
                while (sources.TryGetValue(current, out string source))
                {
                    if (!visited.Add(current))
                        throw new InvalidOperationException($"Skill evolution contains a cycle involving '{current}'.");
                    current = source;
                }
            }
        }

        private readonly struct SkillKey : IEquatable<SkillKey>
        {
            public SkillKey(string id, int level)
            {
                Id = id;
                Level = level;
            }

            private string Id { get; }
            private int Level { get; }

            public bool Equals(SkillKey other) => Level == other.Level && string.Equals(Id, other.Id, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is SkillKey other && Equals(other);
            public override int GetHashCode() => ((Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0) * 397) ^ Level;
        }
    }
}
