using HilbertTransformation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HilbertTransformationTests
{
    [TestFixture]
    public class HyperContrastedPointTests
    {
        /// <summary>
        /// Ensure that the full array of coordinates has correct values at the positions specified by the sparse coordinates.
        /// </summary>
        [Test]
        public void SparseCoordinatesAreCorrect()
        {
            var coordinates = new[] { 2, 3, 6, 7, 8 };
            var values = new uint[] { 1, 2, 3, 4, 5 };
            var missingValues = new uint[] { 0, 6 };
            var point = new HyperContrastedPoint(coordinates, values, 10, missingValues);

            for(var i = 0; i < coordinates.Length; i++)
            {
                Assert.AreEqual(values[i], point.Coordinates[coordinates[i]]);
            }
        }

        [Test]
        public void MissingCoordinatesAreCorrect()
        {
            var coordinates = new[] { 2, 3, 6, 7, 8 };
            var values = new uint[] { 1, 2, 3, 4, 5 };
            var missingValues = new uint[] { 0, 6 };
            var point = new HyperContrastedPoint(coordinates, values, 10, missingValues);

            for (var i = 0; i < point.Dimensions; i++)
            {
                if (coordinates.Contains(i))
                    continue; // Not a missing value
                Assert.IsTrue(missingValues.Contains(point.Coordinates[i]));
            }
        }
    }
}
