using System.Linq;
using NUnit.Framework;
using FlyingFishMomentum.Run;

namespace FlyingFishMomentum.Tests.EditMode
{
    // W0: the headless capture harness shoots a fixed bookmark table.
    // This pins the table: every realm covered, a ring close-up present,
    // frame names unique (they become PNG filenames).
    public class VisualVerifyTests
    {
        [Test]
        public void Bookmarks_CoverAllRealmsAndRingCloseup()
        {
            var specs = VisualVerifyPlan.Bookmarks.Select(b => b.Spec).ToArray();
            foreach (var realm in new[] { "Lagoon", "Gauntlet", "Storm", "Sky" })
                Assert.Contains(realm, specs, "no bookmark for " + realm);
            Assert.IsTrue(VisualVerifyPlan.Bookmarks.Any(
                b => b.Subject == VisualVerifyPlan.Subject.FirstRing), "no ring close-up");
            Assert.AreEqual(VisualVerifyPlan.Bookmarks.Length,
                VisualVerifyPlan.Bookmarks.Select(b => b.Name).Distinct().Count(),
                "duplicate frame names");
        }
    }
}
