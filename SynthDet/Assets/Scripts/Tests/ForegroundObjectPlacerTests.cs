using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class ForegroundObjectPlacerTests
    {
        [Test]
        public void ComputeOverlap_RectFullyOverlapped_Returns1()
        {
            var expectedOverlap = 1;
            var actualOverlap = ForegroundObjectPlacer.ComputeOverlap(new Rect(0, 0, 1, 1), new Rect(0, 0, 2, 2));
            Assert.AreEqual(expectedOverlap, actualOverlap);
        }
        [Test]
        public void ComputeOverlap_OtherFullyOverlapped_Returns1()
        {
            var expectedOverlap = 1;
            var actualOverlap = ForegroundObjectPlacer.ComputeOverlap(new Rect(0, 0, 2, 2), new Rect(0, 0, 1, 1));
            Assert.AreEqual(expectedOverlap, actualOverlap);
        }
    }
}
