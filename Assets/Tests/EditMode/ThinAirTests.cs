using NUnit.Framework;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ThinAirTests
    {
        [Test]
        public void ThinAirFadesLiftWithAltitude()
        {
            // Full lift low, nothing above the band, linear between.
            Assert.AreEqual(1f, PlayerMovementController.ThinAirFactor(0f), 0.001f);
            Assert.AreEqual(1f, PlayerMovementController.ThinAirFactor(60f), 0.001f);
            Assert.AreEqual(0.5f, PlayerMovementController.ThinAirFactor(85f), 0.001f);
            Assert.AreEqual(0f, PlayerMovementController.ThinAirFactor(110f), 0.001f);
            Assert.AreEqual(0f, PlayerMovementController.ThinAirFactor(500f), 0.001f);
        }
    }
}
