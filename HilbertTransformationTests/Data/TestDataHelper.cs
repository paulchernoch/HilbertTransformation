using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clustering;
using HilbertTransformation;

namespace HilbertTransformationTests.Data
{
    public static class TestDataHelper
    {
        /// <summary>
        /// Assume the last value in the list identifies the category and is not one of the point's coordinates.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<UnsignedPoint> MakePoints(IList<int[]> data)
        {
            var dimensions = data[0].Length - 1; // The last number for each point is its category.
			var points = data.Select(asArray => new UnsignedPoint(asArray.Take(dimensions).ToArray())).ToList();
            return points;
        }

        /// <summary>
        /// Make a Classification of N-Dimensional data where the inputs are arrays of integers and the final element in each matrix
        /// is the number of its category.
        /// </summary>
        /// <param name="pointsPlusClass">Data to classify.</param>
        /// <returns>A Classification of the points.</returns>
        public static Classification<UnsignedPoint, string> MakeClassification(IList<int[]> pointsPlusClass)
        {
            var dimensions = pointsPlusClass[0].Length - 1; // The last number for each point is its category.
            var c = new Classification<UnsignedPoint, string>();
            foreach (var pointPlusClass in pointsPlusClass)
            {
				var point = new UnsignedPoint(pointPlusClass.Take(dimensions).ToArray());
                c.Add(point, pointPlusClass[dimensions].ToString(CultureInfo.InvariantCulture));
            }
            return c;
        }
    }
}
