using Babel.Foundation;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class SeededRandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var left = new SeededRandomSource(123456);
            var right = new SeededRandomSource(123456);

            for (int i = 0; i < 128; i++)
                Assert.That(left.NextUInt(), Is.EqualTo(right.NextUInt()));
        }

        [Test]
        public void NextFloat_StaysInsideHalfOpenUnitInterval()
        {
            var random = new SeededRandomSource(7);

            for (int i = 0; i < 1024; i++)
            {
                float value = random.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f));
                Assert.That(value, Is.LessThan(1f));
            }
        }

        [Test]
        public void InvalidIntegerRange_IsRejected()
        {
            var random = new SeededRandomSource(1);
            Assert.That(() => random.NextInt(2, 2), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
