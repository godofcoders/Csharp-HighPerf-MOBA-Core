using MOBA.Core.Infrastructure;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class BrawlerIdleGestureTests
    {
        [Test]
        public void Idle_PlaysAllVariantsWithRestAndNoImmediateRepeats()
        {
            var gesture = new BrawlerIdleGesture(42);
            var seen = new bool[3];
            int previous = -1;
            int starts = 0;
            bool active = false;
            bool rested = true;
            for (int i = 0; i < 12000; i++)
            {
                gesture.Tick(0.02f, false);
                Assert.That(gesture.Weight, Is.InRange(0f, 1f));
                if (gesture.Weight > 0f && !active)
                {
                    Assert.That(rested, Is.True);
                    Assert.That(gesture.Variant, Is.Not.EqualTo(previous));
                    seen[gesture.Variant] = true;
                    previous = gesture.Variant;
                    starts++;
                    rested = false;
                }
                active = gesture.Weight > 0f;
                if (!active) rested = true;
            }
            Assert.That(starts, Is.GreaterThan(10));
            Assert.That(seen, Is.All.True);
        }

        [Test]
        public void MovementOrCombat_FadesGestureAndRequiresFreshIdleDelay()
        {
            var gesture = new BrawlerIdleGesture(12);
            for (int i = 0; i < 500 && gesture.Weight < 0.8f; i++)
                gesture.Tick(0.02f, false);
            Assert.That(gesture.Weight, Is.GreaterThanOrEqualTo(0.8f));
            float previous = gesture.Weight;
            for (int i = 0; i < 50; i++)
            {
                gesture.Tick(0.02f, true);
                Assert.That(gesture.Weight, Is.LessThanOrEqualTo(previous));
                previous = gesture.Weight;
            }
            Assert.That(gesture.Weight, Is.Zero);
            for (int i = 0; i < 90; i++)
            {
                gesture.Tick(0.02f, false);
                Assert.That(gesture.Weight, Is.Zero);
            }
        }

        [Test]
        public void DifferentCharacters_DoNotShareGestureTiming()
        {
            var first = new BrawlerIdleGesture(10);
            var second = new BrawlerIdleGesture(20);
            bool differs = false;
            for (int i = 0; i < 500; i++)
            {
                first.Tick(0.02f, false);
                second.Tick(0.02f, false);
                differs |= first.Weight != second.Weight || first.Variant != second.Variant;
            }
            Assert.That(differs, Is.True);
        }

        [Test]
        public void Paused_DoesNotAdvanceGesture()
        {
            var gesture = new BrawlerIdleGesture(2);
            for (int i = 0; i < 500 && gesture.Weight < 0.5f; i++)
                gesture.Tick(0.02f, false);
            float before = gesture.Weight;
            Assert.That(before, Is.GreaterThan(0f));
            gesture.Tick(0f, false);
            Assert.That(gesture.Weight, Is.EqualTo(before));
        }
    }
}
