using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HilbertTransformation
{
	/// <summary>
	/// Speeds up the test of whether the square distance between two points is greater than,less than or equal to 
	/// a given distance by precomputing the distances from a point to several triangulation points. 
	/// 
	/// The number of triangulation points is configurable, but the first is always the origin, and the second is
	///  { S, S, S, ... S } where S is the maximum value permitted for any coordinate, (2^B)-1 where B is the
	/// number of bits required to represent the maximum value.
	/// 
	/// A Triangulator is only suitable for points whose dimensionality and number of Bits are compatible.
	/// </summary>
	public class Triangulator
	{
		long[] ReferenceSquareDistances { get; set; }
		double[] ReferenceDistances { get; set; }

		int Key { get; set; }


		/// <summary>
		/// Square of the distance from the point to the origin.
		/// </summary>
		public long SquareMagnitude { get { return ReferenceSquareDistances[0]; } }

		/// <summary>
		/// Distance from the point to the origin.
		/// </summary>
		public double Magnitude { get { return ReferenceDistances[0]; } }

		#region Reference Points

		/// <summary>
		/// Form the key used for looking up the reference points for a given case.
		/// </summary>
		/// <returns>A key into the disctionary storing the reference points for a given case.</returns>
		/// <param name="dimensions">Number of Dimensions for the case.</param>
		/// <param name="bits">Number of Bits for the case, from 1 to 31.</param>
		/// <param name="numTriangulationPoints">Number of triangulation points, from 1 to 31.</param>
		private static int LookupKey(int dimensions, int bits, int numTriangulationPoints)
		{
			return bits + (numTriangulationPoints << 5) + (dimensions << 10);
		}

		private static ConcurrentDictionary<int, UnsignedPoint[]> ReferencePoints { get; set; } 
			= new ConcurrentDictionary<int, UnsignedPoint[]>();

		private static UnsignedPoint[] CreateReferencePoints(int dimensions, int bits, int numTriangulationPoints)
		{
			var tPoints = new UnsignedPoint[numTriangulationPoints];
			var max = (uint)((1 << bits) - 1);
			var modulus = numTriangulationPoints - 1;
			foreach (var i in Enumerable.Range(0, numTriangulationPoints))
			{
				var coordinates = new uint[dimensions];
				if (i == 1)
				{
					for (var iDim = 0; iDim < dimensions; iDim++)
						coordinates[i] = max;
				}
				else if (i > 1) {
					for (var iDim = 0; iDim < dimensions; iDim++)
					{
						if (iDim % modulus == 0)
							coordinates[i] = max;
					}
				}
				tPoints[i] = new UnsignedPoint(coordinates);
			}
			return tPoints;
		}

		private UnsignedPoint[] GetReferencePoints(int dimensions, int bits, int numTriangulationPoints)
		{
			return ReferencePoints.GetOrAdd(
				LookupKey(dimensions, bits, numTriangulationPoints),
				(key) => CreateReferencePoints(dimensions, bits, numTriangulationPoints)
			);
		}

		#endregion

		public Triangulator(UnsignedPoint point, int bits, int numTriangulationPoints = 10)
		{
			Key = LookupKey(point.Dimensions, bits, numTriangulationPoints);
			var refPoints = GetReferencePoints(point.Dimensions, bits, numTriangulationPoints);
			ReferenceSquareDistances = new long[numTriangulationPoints];
			ReferenceDistances = new double[numTriangulationPoints];
			for (var iTri = 0; iTri < numTriangulationPoints; iTri++)
			{
				ReferenceSquareDistances[iTri] = point.Measure(refPoints[iTri]);
				ReferenceDistances[iTri] = Math.Sqrt(ReferenceSquareDistances[iTri]);                                           
			}
		}

		/// <summary>
		/// Tests if two points are farther apart than a given square distance.
		/// </summary>
		/// <returns><c>true</c>, if the points are farther apart than the given square distance,
		/// but <c>false</c> if we are not sure.
		/// If we are not sure, we may need to perform the full distance computation.</returns>
		/// <param name="other">Information about Other point.</param>
		/// <param name="squareDistance">Square distance for test.</param>
		public bool ArePointsFartherForSure(Triangulator other, long squareDistance)
		{
			if (Key != other.Key)
				return false;
			var highestMinimumDistance = Enumerable
				.Range(0, ReferenceDistances.Length)
				.Select(i => Math.Abs(ReferenceDistances[i] - other.ReferenceDistances[i]))
			    .Max();
			var highMinSquareDistance = (long)(highestMinimumDistance * highestMinimumDistance);
			return (highMinSquareDistance > squareDistance);
		}

		/// <summary>
		/// Tests if two points are nearer than a given square distance.
		/// </summary>
		/// <returns><c>true</c>, if the points are nearer than the given square distance,
		/// but <c>false</c> if we are not sure.
		/// If we are not sure, we may need to perform the full distance computation.</returns>
		/// <param name="other">Information about Other point.</param>
		/// <param name="squareDistance">Square distance for test.</param>
		public bool ArePointsNearerForSure(Triangulator other, long squareDistance)
		{
			if (Key != other.Key)
				return false;
			// This only exploits the distance to the Origin. 
			// TODO: Figure out how to exploit the other reference points beside the origin.
			var high = SquareMagnitude + other.SquareMagnitude;
			return (squareDistance > high);
		}
	}
}
