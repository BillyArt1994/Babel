namespace Babel.Foundation
{
    public interface IRandomSource
    {
        uint NextUInt();
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        bool NextBool();
    }
}
