using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Clustering;
using HilbertTransformation;
using HilbertTransformation.Random;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	/// <summary>
	/// Test the Hilbert Transformation.
	/// 
	///    The transformation:
	/// 
	///       A. Hilbert Index to HilbertPoint to N-Dimensional coordinates
	/// 
	///          int bits = ???;       // Pick so that 2^bits exceeds the larges value in any coordinate.
	///          int dimensions = ???; // Number of dimensions for the point.
	///          var index1 = new BigInteger(...);
	///          var hPoint1 = new HilbertPoint(index1, dimensions, bits);
	/// 	     uint[] coordinates = hPoint.Coordinates;
	/// 
	///       B. Coordinates to Hilbert Index
	/// 
	///          var hPoint2 = new HilbertPoint(coordinates, bits);
	///          BigInteger index2 = hPoint2.Index;
	/// </summary>
	[TestFixture]
	public class HilbertPointTests
	{
		[Test]
		public void HilbertToCartesian_Dim2Bits6_AdjacentPoints()
		{
			AdjacentPointsCase(2, 6);
		}

		[Test]
		public void HilbertToCartesian_Dim3Bits7_AdjacentPoints()
		{
			AdjacentPointsCase(3, 7);
		}

		[Test]
		public void HilbertToCartesian_Dim4Bits4_AdjacentPoints()
		{
			AdjacentPointsCase(4, 4);
		}

		/// <summary>
		/// Verify that the square Cartesian distance calculation is correct.
		/// </summary>
		[Test]
		public void MeasureDistanceSquared()
		{
			const int dims = 100;
			var p1 = new uint[dims];
			var p2 = new uint[dims];
			var expectedSquareDistance = 0L;
			for (var i = 0; i < dims; i++)
			{
				p1[i] = (uint)(i % 37) * 10;
				p2[i] = (uint)(i % 18) * 17;
				long delta = (long)p1[i] - (long)p2[i];
				expectedSquareDistance += delta * delta;
			}
			var up1 = new UnsignedPoint(p1);
			var up2 = new UnsignedPoint(p2);
			var actualSquareDistance = up1.Measure(up2);
			Assert.AreEqual(expectedSquareDistance, actualSquareDistance, "Distances do not match");
		}

		/// <summary>
		/// The proof of this test is studying the Console output by eye.
		/// </summary>
		[Test]
		public void CartesianToHilbert_Dim2Bits2()
		{
			var bits = 2;
			var size = 1 << bits;
			var sb = new StringBuilder();
			for (var row = 0; row < size; row++)
			{
				for (var column = 0; column < size; column++)
				{
					var cartesianPoint = new int[] { row, column };
					var hilbertPoint = new HilbertPoint(cartesianPoint, bits);
					var hilbertIndex = hilbertPoint.HilbertIndex;
					sb.Append("Cart = [")
					  .Append(string.Join(",", cartesianPoint))
					  .Append("] Hilbert = ")
					  .Append(hilbertIndex.ToString())
					  .AppendLine();
				}
			}
			var diagnostic = sb.ToString();
			Console.WriteLine(diagnostic);
		}

		/// <summary>
		/// Verify the transformation in both directions, from 1-Dimensional index to N-dimensional point and back.
		/// 
		/// This evaluates 2^(dims*bits) points, so be careful or the test will run for a long time and consume a lot of memory.
		/// </summary>
		/// <param name="dims">Dimensions for each point.</param>
		/// <param name="bits">Bits per dimension.</param>
		public void AdjacentPointsCase(int dims, int bits)
		{
			var points = new HilbertPoint[1 << (bits*dims)];
			for (var i = 0; i < points.Length; i++)
			{
				var hilbertIndex = new BigInteger(i);
				points[i] = new HilbertPoint(hilbertIndex, dims, bits);
				if (i > 0)
				{
					var p1 = points[i - 1];
					var p2 = points[i];
					Assert.IsTrue(ArePointsAdjacent(p1, p2),
								  string.Format("Points {0} and {1}",
												FormatPoint(p1), FormatPoint(p2)));
				}
				AssertPointMapsToHilbertIndex(points[i].Coordinates, hilbertIndex, dims, bits);
			}
		}

		static void AssertPointMapsToHilbertIndex(uint[] coordinates, BigInteger hilbertIndex, int dims, int bits)
		{
			var hPoint = new HilbertPoint(coordinates, bits);
			Assert.AreEqual(hilbertIndex, hPoint.HilbertIndex , $"Coordinates {UintsToString(coordinates)} do not map back to the expected Hilbert Index {hilbertIndex.ToString()}, but instead {hPoint.HilbertIndex}");
		}

		/// <summary>
		/// Test if two points are adjacent, meaning that only a single coordiante differs between them and
		/// the difference in coordinate value is exactly one.
		/// </summary>
		/// <returns><c>true</c>, if points are adjacent, <c>false</c> otherwise.</returns>
		/// <param name="p1">First point.</param>
		/// <param name="p2">Second point.</param>
		static bool ArePointsAdjacent(HilbertPoint p1, HilbertPoint p2)
		{
			var maxCoordinateDistance = 0;
			var differentDimensionsCount = 0;
			for (var dim = 0; dim < p1.Dimensions; dim++)
			{
				var diff = Math.Abs(p1[dim] - p2[dim]);
				if (diff != 0)
				{
					differentDimensionsCount++;
					maxCoordinateDistance = Math.Max(diff, maxCoordinateDistance);
				}
			}
			return maxCoordinateDistance == 1 && differentDimensionsCount == 1;
		}

		/// <summary>
		/// Pretty print a HilbertPoint.
		/// </summary>
		/// <returns>Formatted point.</returns>
		/// <param name="p">Point to pretty print.</param>
		static string FormatPoint(HilbertPoint p)
		{
			return string.Format("Index: {0} Coords: [{1}]", p.HilbertIndex, string.Join(",", p.Coordinates));
		}



		/// <summary>
		/// Verify that two vectors have the same values, except that one coordinate differs form another by one.
		/// </summary>
		/// <returns><c>true</c>, if only one coordinate differs, <c>false</c> otherwise.</returns>
		/// <param name="u">First vector to compare.</param>
		/// <param name="v">Second vector to compare.</param>
		private static bool DifferByOne(uint[] u, uint[] v)
		{
			var dims = u.Length;
			var sum = 0U;
			for (var dim = 0; dim < dims; dim++)
			{
				sum += u[dim] > v[dim] ? u[dim] - v[dim] : v[dim] - u[dim];
			}
			return sum == 1U;
		}


		/// <summary>
		/// Pretty print a uint array.
		/// </summary>
		/// <returns>Formatted string.</returns>
		/// <param name="uVec">Vector of points.</param>
		private static string UintsToString(uint[] uVec)
		{
			var sb = new StringBuilder();
			foreach (var u in uVec)
			{
				if (sb.Length == 0)
					sb.Append("[");
				else
					sb.Append(",");
				sb.Append(u);
			}
			sb.Append("]");
			return sb.ToString();
		}

		[Test]
		public void SquareDistanceCompareAverage()
		{
			// Example output:
			//    After 200 trials, Optimizations were possible on Average 29.2514 %, with Min 20.14 % and Max 39.26 %
			var sumPercent = 0.0;
			var count = 200;
			var maxPercent = 0.0;
			var minPercent = 100.0;
			for (var i = 0; i < count; i++)
			{
				var percent = SquareDistanceCompareOptimizableCase(10000);
				maxPercent = Math.Max(maxPercent, percent);
				minPercent = Math.Min(minPercent, percent);
				sumPercent += percent;
			}
			var avgPercent = sumPercent / count;
			var message = $"After {count} trials, Optimizations were possible on Average {avgPercent} %, with Min {minPercent} % and Max {maxPercent} %";
			Console.WriteLine(message);
			Assert.GreaterOrEqual(avgPercent, 25.0, message);
		}


		/// <summary>
		/// UnsignedPoint.SquareDistanceCompare has an optimization. This tests how often this optimization
		/// can be exploited in a realistic test. The comparison will be against an estimated characteristic distance
		/// between points. This distance is assumed to be close enough to trigger two points to be merged into a single cluster.
		/// </summary>
		private double SquareDistanceCompareOptimizableCase(int totalComparisons)
		{
			// 1. Make test data.
			var bitsPerDimension = 10;
			var data = new GaussianClustering
			{
				ClusterCount = 100,
				Dimensions = 100,
				MaxCoordinate = (1 << bitsPerDimension) - 1,
				MinClusterSize = 50,
				MaxClusterSize = 150
			};
			var clusters = data.MakeClusters();


			// 2. Create HilbertIndex for points.
			var hIndex = new HilbertIndex(clusters, bitsPerDimension);

			// 3. Deduce the characteristic distance.
			var counter = new ClusterCounter
			{
				OutlierSize = 5,
				NoiseSkipBy = 10
			};
			var count = counter.Count(hIndex.SortedPoints);
			var mergeDistance = count.MaximumSquareDistance;
			var longDistance = 5 * mergeDistance;

			// 4. Select random pairs of points and see how many distance comparisons can exploit the optimization.
			var rng = new FastRandom();
			var points = clusters.Points().ToList();
			var ableToUseOptimizationsAtShortDistance = 0;
			var ableToUseOptimizationsAtLongDistance = 0;

			for (var i = 0; i < totalComparisons; i++)
			{
				var p1 = points[rng.Next(points.Count)];
				var p2 = points[rng.Next(points.Count)];
				if (IsDistanceOptimizationUsable(p1, p2, mergeDistance))
					ableToUseOptimizationsAtShortDistance++;
				if (IsDistanceOptimizationUsable(p1, p2, longDistance))
					ableToUseOptimizationsAtLongDistance++;
			}
			var percentOptimizable = 100.0 * ableToUseOptimizationsAtShortDistance / totalComparisons;
			var percentOptimizableLongDistance = 100.0 * ableToUseOptimizationsAtLongDistance / totalComparisons;
			var message = $"Comparisons were {percentOptimizable} % Optimizable at short distance, {percentOptimizableLongDistance} % at long distance";
			Console.WriteLine(message);
			return percentOptimizable;
		}

		/// <summary>
		/// UnsignedPoint.SquareDistanceCompare has an optimization. 
		/// This tests if that optimization can be used in a given case.
		/// </summary>
		/// <returns><c>true</c>, if distance optimization is usable, <c>false</c> otherwise.</returns>
		/// <param name="p1">First point to compare.</param>
		/// <param name="p2">Second point to compare.</param>
		/// <param name="squareDistance">Test if the distance between the points is less than, equal to or greater than this given distance.</param>
		private bool IsDistanceOptimizationUsable(UnsignedPoint p1, UnsignedPoint p2, long squareDistance)
		{
			var delta = p1.Magnitude - p2.Magnitude;
			var low = (long)Math.Floor(delta * delta);
			if (squareDistance < low) return true;

			var high = p1.SquareMagnitude + p2.SquareMagnitude;
			return (squareDistance > high);
		}
	}
}


