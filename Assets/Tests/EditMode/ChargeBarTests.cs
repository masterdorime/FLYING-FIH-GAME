using NUnit.Framework;
using UnityEngine;

namespace FlyingFishMomentum.Tests.EditMode
{
    public class ChargeBarTests
    {
        [Test]
        public void DrawDirectionCompensatesBillboardMirror()
        {
            // The bar billboards +Z at the camera, which mirrors local +X
            // to screen-left (positions already compensate). Shapes must
            // mirror too: a Left step draws pointing local +X so it reads
            // as left on screen. Up/down are unaffected.
            Assert.AreEqual(new Vector2(1f, 0f), ChargeBar.DrawDirection(ChargeArrow.Left));
            Assert.AreEqual(new Vector2(-1f, 0f), ChargeBar.DrawDirection(ChargeArrow.Right));
            Assert.AreEqual(new Vector2(0f, 1f), ChargeBar.DrawDirection(ChargeArrow.Up));
            Assert.AreEqual(new Vector2(0f, -1f), ChargeBar.DrawDirection(ChargeArrow.Down));
        }
    }
}
