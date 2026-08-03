using System;

namespace Babel.Foundation
{
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        public static EntityHandle Invalid => default;

        public EntityHandle(int index, uint generation)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation), "Generation zero is reserved for invalid handles.");
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public uint Generation { get; }
        public bool IsValid => Generation != 0;

        public bool Equals(EntityHandle other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EntityHandle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (Index * 397) ^ (int)Generation; }
        }

        public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);
        public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);
        public override string ToString() => IsValid ? $"Entity({Index}:{Generation})" : "Entity(Invalid)";
    }
}
