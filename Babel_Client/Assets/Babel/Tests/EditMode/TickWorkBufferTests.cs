using System;
using Babel.Foundation;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class TickWorkBufferTests
    {
        [Test]
        public void WorkBuffer_RequiresOpenTickAndClearsResolvedWork()
        {
            var buffer = new TickWorkBuffer<int>();
            Assert.Throws<InvalidOperationException>(() => buffer.Add(1));

            buffer.BeginTick();
            buffer.Add(3);
            buffer.Add(5);

            Assert.That(buffer.Items, Is.EqualTo(new[] { 3, 5 }));
            buffer.ClearResolved();
            Assert.That(buffer.Count, Is.Zero);

            buffer.Add(7);
            buffer.EndTick();
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.IsTickOpen, Is.False);
        }

        [Test]
        public void WorkBuffer_AbortDropsPartialTickAndCanRestart()
        {
            var buffer = new TickWorkBuffer<string>();
            buffer.BeginTick();
            buffer.Add("partial");

            buffer.AbortTick();

            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.IsTickOpen, Is.False);
            buffer.BeginTick();
            buffer.Add("next");
            Assert.That(buffer.Items[0], Is.EqualTo("next"));
            buffer.EndTick();
        }
    }
}
