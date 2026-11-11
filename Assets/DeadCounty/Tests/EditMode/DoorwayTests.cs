using DeadCounty.World;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // A 1 m opening centred on the origin along X, crossed along Z.
    public class DoorwayTests
    {
        static readonly Doorway Opening = new(Vector3.zero, Vector3.back, Vector3.right, 0.5f);

        [TestCase(0f, true)]
        [TestCase(0.45f, true)]
        [TestCase(-0.45f, true)]
        [TestCase(0.6f, false)]
        [TestCase(-0.6f, false)]
        public void SegmentCrossingTheWallCountsOnlyThroughTheOpening(float x, bool crosses)
            => Assert.That(Opening.Crosses(new Vector3(x, 0f, -0.3f), new Vector3(x, 0f, 0.3f)), Is.EqualTo(crosses));

        [Test]
        public void SegmentStayingOnOneSideOrAlongTheWallNeverCrosses()
        {
            Assert.That(Opening.Crosses(new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -0.2f)), Is.False);
            Assert.That(Opening.Crosses(new Vector3(-0.3f, 0f, 0f), new Vector3(0.3f, 0f, 0f)), Is.False);
        }

        [Test]
        public void SlantedSegmentCrossesWhereItMeetsTheWall()
        {
            Assert.That(Opening.Crosses(new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0f, 0.5f)), Is.True, "meets the wall at x = 0.5");
            Assert.That(Opening.Crosses(new Vector3(0.2f, 0f, -0.5f), new Vector3(1.2f, 0f, 0.5f)), Is.False, "meets the wall at x = 0.7");
        }

        [Test]
        public void SideIgnoresHeightAndFollowsTheNormal()
        {
            Assert.That(Opening.Side(new Vector3(3f, 5f, -0.1f)), Is.EqualTo(1f));
            Assert.That(Opening.Side(new Vector3(-3f, -5f, 0.1f)), Is.EqualTo(-1f));
        }
    }
}
