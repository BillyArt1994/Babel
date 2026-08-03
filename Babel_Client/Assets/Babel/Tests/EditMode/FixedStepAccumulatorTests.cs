using Babel.Gameplay.RunFlow;
using NUnit.Framework;

namespace Babel.Tests
{
    public sealed class FixedStepAccumulatorTests
    {
        [Test]
        public void SixtyRegularFrames_ProduceSixtyTicksAtOneX()
        {
            var accumulator = new FixedStepAccumulator(1d / 60d, 12);
            int steps = 0;

            for (int i = 0; i < 60; i++)
                steps += accumulator.Consume(1d / 60d, RunSpeed.One, true).Steps;

            Assert.That(steps, Is.EqualTo(60));
            Assert.That(accumulator.DroppedTicksTotal, Is.Zero);
        }

        [Test]
        public void SpeedMultiplier_ChangesTickCountWithoutChangingFixedDelta()
        {
            var accumulator = new FixedStepAccumulator(1d / 60d, 12);

            FixedStepBatch twoX = accumulator.Consume(1d / 60d, RunSpeed.Two, true);
            FixedStepBatch fourX = accumulator.Consume(1d / 60d, RunSpeed.Four, true);

            Assert.That(twoX.Steps, Is.EqualTo(2));
            Assert.That(fourX.Steps, Is.EqualTo(4));
        }

        [Test]
        public void PausedFrame_DoesNotAddTimeAndPreservesRemainder()
        {
            var accumulator = new FixedStepAccumulator(1d / 60d, 12);

            Assert.That(accumulator.Consume(1d / 120d, RunSpeed.One, true).Steps, Is.Zero);
            double beforePause = accumulator.RemainderSeconds;
            Assert.That(accumulator.Consume(10d, RunSpeed.Four, false).Steps, Is.Zero);
            Assert.That(accumulator.RemainderSeconds, Is.EqualTo(beforePause).Within(1e-12d));
            Assert.That(accumulator.Consume(1d / 120d, RunSpeed.One, true).Steps, Is.EqualTo(1));
        }

        [Test]
        public void LongFrame_IsCappedAndExcessTicksAreDropped()
        {
            var accumulator = new FixedStepAccumulator(1d / 60d, 12);

            FixedStepBatch batch = accumulator.Consume(1d, RunSpeed.One, true);

            Assert.That(batch.Steps, Is.EqualTo(12));
            Assert.That(batch.DroppedTicks, Is.EqualTo(48));
            Assert.That(accumulator.DroppedTicksTotal, Is.EqualTo(48));
        }
    }
}
