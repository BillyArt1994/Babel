using Babel.Foundation;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class EntityHandleTests
    {
        [Test]
        public void DefaultHandle_IsInvalid()
        {
            Assert.That(EntityHandle.Invalid.IsValid, Is.False);
        }

        [Test]
        public void SameIndexWithNewGeneration_IsDifferentEntity()
        {
            var oldHandle = new EntityHandle(4, 1);
            var recycledHandle = new EntityHandle(4, 2);

            Assert.That(oldHandle, Is.Not.EqualTo(recycledHandle));
            Assert.That(oldHandle.IsValid, Is.True);
            Assert.That(recycledHandle.IsValid, Is.True);
        }

        [Test]
        public void ZeroGeneration_IsRejected()
        {
            Assert.That(() => new EntityHandle(0, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
