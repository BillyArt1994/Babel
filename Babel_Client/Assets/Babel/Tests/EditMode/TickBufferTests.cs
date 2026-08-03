using Babel.Foundation;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class TickBufferTests
    {
        [Test]
        public void CommandQueuedDuringTick_IsConsumedOnFollowingTick()
        {
            var buffer = new TickCommandBuffer<int>();
            buffer.Enqueue(1);

            buffer.BeginTick();
            Assert.That(buffer.Current, Is.EqualTo(new[] { 1 }));
            buffer.Enqueue(2);
            buffer.EndTick();

            buffer.BeginTick();
            Assert.That(buffer.Current, Is.EqualTo(new[] { 2 }));
            buffer.EndTick();
        }

        [Test]
        public void FrameEvents_ArePublishedAtomically()
        {
            var buffer = new FrameEventBuffer<int>();

            buffer.BeginFrame();
            buffer.Add(3);
            buffer.Add(5);
            Assert.That(buffer.Published, Is.Empty);
            buffer.PublishFrame();

            Assert.That(buffer.Published, Is.EqualTo(new[] { 3, 5 }));

            buffer.BeginFrame();
            buffer.PublishFrame();
            Assert.That(buffer.Published, Is.Empty);
        }
    }
}
