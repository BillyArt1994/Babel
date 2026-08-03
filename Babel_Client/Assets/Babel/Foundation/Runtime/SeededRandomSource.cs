using System;

namespace Babel.Foundation
{
    public sealed class SeededRandomSource : IRandomSource
    {
        private const uint FallbackSeed = 0x6D2B79F5u;
        private uint _state;

        public SeededRandomSource(int seed) : this(unchecked((uint)seed)) { }

        public SeededRandomSource(uint seed)
        {
            _state = seed == 0 ? FallbackSeed : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");

            uint range = unchecked((uint)(maxExclusive - minInclusive));
            uint threshold = unchecked(0u - range) % range;
            uint sample;
            do { sample = NextUInt(); } while (sample < threshold);
            return minInclusive + (int)(sample % range);
        }

        public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);
        public bool NextBool() => (NextUInt() & 1u) != 0;
    }
}
