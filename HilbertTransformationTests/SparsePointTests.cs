using HilbertTransformation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HilbertTransformationTests
{
    [TestFixture]
    public class SparsePointTests
    {

        [Test]
        public void SparseToSparseMeasure()
        {
            var sparseData1 = new Dictionary<int, uint>
            {
                [5] = 1,
                [7] = 2,
                [10] = 3,
                [15] = 4
            };
            var sparseData2 = new Dictionary<int, uint>
            {
                [4] = 4,
                [7] = 3,
                [10] = 2,
                [18] = 1
            };
            var missingValue = 0U;
            var p1 = new SparsePoint(sparseData1, 20, missingValue);
            var p2 = new SparsePoint(sparseData2, 20, missingValue);
            var actualSquareDistance = p1.Measure(p2);
            var expectedSquareDistance = 36L; // 30 + 30 - 2*(6+6)
            Assert.AreEqual(expectedSquareDistance, actualSquareDistance, "Sparse-to-sparse Distances do not match.");
        }

        [Test]
        public void SparseToUnsignedMeasureWhereMissingValueIsZero()
        {
            var sparseData1 = new Dictionary<int, uint>
            {
                [5] = 1,
                [7] = 2,
                [10] = 3,
                [15] = 4
            };
            var missingValue = 0U;
            var p1 = new SparsePoint(sparseData1, 20, missingValue);
            var p2 = new UnsignedPoint(new[] { 0,0,0,0,4,0,0,3,0,0,2,0,0,0,0,0,0,0,1,0 });
            var actualSquareDistance = p1.Measure(p2);
            var expectedSquareDistance = 36L;
            Assert.AreEqual(expectedSquareDistance, actualSquareDistance, $"Sparse-to-unsigned Distances with MissingValue={missingValue} do not match.");
        }

        [Test]
        public void SparseToUnsignedMeasureWhereMissingValueIsPositive()
        {
            var sparseData1 = new Dictionary<int, uint>
            {
                [5] = 1,
                [7] = 2,
                [10] = 3,
                [15] = 4
            };
            var missingValue = 1U;
            var p1 = new SparsePoint(sparseData1, 20, missingValue);
            var p2 = new UnsignedPoint(new[] { 0, 0, 0, 0, 4, 0, 0, 3, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 1, 0 });
            var actualSquareDistance = p1.Measure(p2);
            var expectedSquareDistance = 42L; // 46 + 30 - 2(4+6+6+1)
            Assert.AreEqual(expectedSquareDistance, actualSquareDistance, $"Sparse-to-unsigned Distances with MissingValue={missingValue} do not match.");

        }
    }
}
