using NUnit.Framework;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ChargeBarMathTests
    {
        [Test]
        public void FillReachesFullAtLimit()
        {
            // required 0.8, limit 1.3: fill hits 1.0 exactly at the limit.
            Assert.AreEqual(0f, ChargeBar.FillFraction(0f, 0.8f, 1.3f), 0.001f);
            Assert.AreEqual(0.8f / 1.3f, ChargeBar.FillFraction(1f, 0.8f, 1.3f), 0.001f);
            Assert.AreEqual(1f, ChargeBar.FillFraction(1.625f, 0.8f, 1.3f), 0.001f);
            Assert.AreEqual(1f, ChargeBar.FillFraction(5f, 0.8f, 1.3f), 0.001f);
        }

        [Test]
        public void MarkerSitsAtRequired()
        {
            Assert.AreEqual(0.8f / 1.3f, ChargeBar.MarkerFraction(0.8f, 1.3f), 0.001f);
        }
    }
}
