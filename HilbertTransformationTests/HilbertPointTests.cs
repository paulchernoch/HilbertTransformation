using System;
using System.Numerics;
using System.Text;
using HilbertTransformation;
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
	}
}


