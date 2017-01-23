using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace HilbertTransformation
{

	/// <summary>
	/// A multi-dimensional point where all coordinates are positive values.
	/// </summary>
	public class UnsignedPoint : IEquatable<UnsignedPoint>, IComparable<UnsignedPoint>,
								ICloneable, IHaveIntegerCoordinates,
								IMeasurable<UnsignedPoint, long>

	{

		#region Unique Id

		/// <summary>
		/// Auto-incrementing counter to use for generating unique ids.
		/// 
		/// This is incremented in a thread-safe manner.
		/// </summary>
		private static int _counter;

		/// <summary>
		/// Unique id for point.
		/// </summary>
		public int UniqueId { get; } = NextId();

		private static int NextId() { return Interlocked.Increment(ref _counter); }

		#endregion

		#region Properties (Coordinates, Dimensions, etc)



		/// <summary>
		/// Coordinates of the point in N-space.
		/// </summary>
		public uint[] Coordinates { get; private set; }

		/// <summary>
		/// Number of dimensions for the point.
		/// </summary>
		public int Dimensions { get; private set; }

		/// <summary>
		/// Maximum value in the Coordinates array.
		/// </summary>
		protected long MaxCoordinate { get; set; }

		/// <summary>
		/// Square of the distance from the point to the origin.
		/// </summary>
		protected long SquareMagnitude { get; set; }

		private void InitInvariants()
		{
			MaxCoordinate = Coordinates.Max();
			SquareMagnitude = Coordinates.Sum(x => x * (long)x);
		}

		#endregion

		#region Indexers

		/// <summary>
		/// Access the coordinate values as signed integers.
		/// </summary>
		/// <param name="i">Index, which must be between zero and DimensionDefinitions - 1, inclusive.</param>
		/// <returns>Corrdinate value as an integer.</returns>
		public int this[int i] { get { return (int)Coordinates[i]; } }

		#endregion

		#region Constructors and helpers

		/// <summary>
		/// Construct a UnsignedPoint given its N-dimensional coordinates.
		/// </summary>
		/// <param name="coordinates">Coordinate values as unsigned integers.</param>
		public UnsignedPoint(uint[] coordinates)
		{
			Coordinates = coordinates;
			Dimensions = Coordinates.Length;
			InitInvariants();
		}

		/// <summary>
		/// Convert signed integers into unsigned ones.
		/// </summary>
		/// <param name="p">List of signed integers.</param>
		/// <returns>Array of unsigned integers.</returns>
		public static uint[] MakeUnsigned(IList<int> p)
		{
			var dimensions = p.Count;
			var coordinates = new uint[dimensions];
			for (var i = 0; i < dimensions; i++)
				coordinates[i] = (uint)p[i];
			return coordinates;
		}

		/// <summary>
		/// Convert unsigned integers into signed ones.
		/// </summary>
		/// <param name="p">List of unsigned integers.</param>
		/// <returns>Array of signed integers.</returns>
		public static int[] MakeSigned(IList<uint> p)
		{
			var dimensions = p.Count;
			var coordinates = new int[dimensions];
			for (var i = 0; i < dimensions; i++)
				coordinates[i] = (int)p[i];
			return coordinates;
		}

		/// <summary>
		/// Convert signed integers into unsigned ones while permuting the dimensions.
		/// 
		/// Such permutation is useful for when mapping the same points to many different Hilbert Curves with 
		/// alterred orientation.
		/// </summary>
		/// <param name="p">List of signed integers.</param>
		/// <param name="permutation">Maps from target coordinates to source coordinates:
		///    target[i] = source[permutation[i]]
		/// </param>
		/// <returns>Array of unsigned integers.</returns>
		private static uint[] PermuteAndMakeUnsigned(IList<int> p, int[] permutation)
		{
			var dimensions = p.Count;
			var coordinates = new uint[dimensions];
			for (var i = 0; i < dimensions; i++)
				coordinates[i] = (uint)p[permutation[i]];
			return coordinates;
		}

		#region Implement Clone interface

		/// <summary>
		/// Clone constructor.
		/// </summary>
		private UnsignedPoint(UnsignedPoint original)
		{
			Coordinates = (uint[])original.Coordinates.Clone();
			Dimensions = original.Dimensions;
			MaxCoordinate = original.MaxCoordinate;
			SquareMagnitude = original.SquareMagnitude;
		}

		public object Clone() { return new UnsignedPoint(this); }

		#endregion

		#endregion

		#region Equality and Hash Code

		public override int GetHashCode() { return UniqueId; }

		/// <summary>
		/// An UnsignedPoint only equals a second point if they share the same UniqueId.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:HilbertTransformation.UnsignedPoint"/>.</param>
		/// <returns>True if the objects are both UnsignedPoints and share the same id and false otherwise.</returns>
		public override bool Equals(object obj)
		{
			var p = obj as UnsignedPoint;
			return p != null && Equals(p);
		}

		public bool Equals(UnsignedPoint other)
		{
			return UniqueId == other.UniqueId;
		}

		#endregion

		#region Comparing, Measuring Distance and Sorting

		/// <summary>
		/// Sort by UniqueId.
		/// </summary>
		/// <param name="other">Second point in comparison.</param>
		/// <returns>-1 if this has a lower index, 0 if they match, and +1 if this has a higher index.</returns>
		public int CompareTo(UnsignedPoint other)
		{
			return UniqueId.CompareTo(other.UniqueId);
		}

		/// <summary>
		/// Square of the cartesian distance between two points.
		/// </summary>
		/// <param name="other">Second point for distance computation.</param>
		/// <returns>The square of the distance between the two points.</returns>
		public long SquareDistance(UnsignedPoint other)
		{
			return SquareDistanceDotProduct(
				Coordinates, other.Coordinates, SquareMagnitude, other.SquareMagnitude, MaxCoordinate, other.MaxCoordinate
			);
		}

		/// <summary>
		/// Squares the distance dot product.
		/// </summary>
		/// <returns>The square distance.</returns>
		/// <param name="x">First point.</param>
		/// <param name="y">Second point.</param>
		/// <param name="xMag2">Distance from x to the origin, squared.</param>
		/// <param name="yMag2">Distance from y to the origin, squared.</param>
		/// <param name="xMax">Maximum value of any coordinate in x.</param>
		/// <param name="yMax">Maximum value of any coordinate in y.</param>
		private static long SquareDistanceDotProduct(uint[] x, uint[] y, long xMag2, long yMag2, long xMax, long yMax)
		{
			const int unroll = 4;
			if (xMax * yMax * unroll < (long)uint.MaxValue)
				return SquareDistanceDotProductNoOverflow(x, y, xMag2, yMag2);

			// Unroll the loop partially to improve speed. (2.7x improvement!)
			var dotProduct = 0UL;
			var leftovers = x.Length % unroll;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;

			for (var i = 0; i < roundDimensions; i += unroll)
			{
				var x1 = x[i];
				ulong y1 = y[i];
				var x2 = x[i + 1];
				ulong y2 = y[i + 1];
				var x3 = x[i + 2];
				ulong y3 = y[i + 2];
				var x4 = x[i + 3];
				ulong y4 = y[i + 3];
				dotProduct += x1 * y1 + x2 * y2 + x3 * y3 + x4 * y4;
			}
			for (var i = roundDimensions; i < dimensions; i++)
				dotProduct += x[i] * (ulong)y[i];
			return xMag2 + yMag2 - 2L * (long)dotProduct;
		}

		/// <summary>
		/// Compute the square of the Cartesian distance using the dotproduct method,
		/// assuming that calculations wont overflow uint.
		/// 
		/// This permits us to skip some widening conversions to ulong, making the computation faster.
		/// 
		/// Algorithm:
		/// 
		///    2         2       2
		///   D    =  |x|  +  |y|  -  2(x·y)
		/// 
		/// Using the dot product of x and y and precomputed values for the square magnitudes of x and y
		/// permits us to use two operations (multiply and add) instead of three (subtract, multiply and add)
		/// in the main loop, saving one third of the time.
		/// </summary>
		/// <returns>The square distance.</returns>
		/// <param name="x">First point.</param>
		/// <param name="y">Second point.</param>
		/// <param name="xMag2">Distance from x to the origin, squared.</param>
		/// <param name="yMag2">Distance from y to the origin, squared.</param>
		private static long SquareDistanceDotProductNoOverflow(uint[] x, uint[] y, long xMag2, long yMag2)
		{
			// Unroll the loop partially to improve speed. (2.7x improvement!)
			const int unroll = 4;
			var dotProduct = 0UL;
			var leftovers = x.Length % unroll;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;
			for (var i = 0; i < roundDimensions; i += unroll)
				dotProduct += (x[i] * y[i] + x[i + 1] * y[i + 1] + x[i + 2] * y[i + 2] + x[i + 3] * y[i + 3]);
			for (var i = roundDimensions; i < dimensions; i++)
				dotProduct += x[i] * y[i];
			return xMag2 + yMag2 - 2L * (long)dotProduct;
		}

		/// <summary>
		/// Measure the square of the Cartesian distance between two points.
		/// </summary>
		/// <param name="reference">Second point for comparison.</param>
		/// <returns>Square of the Cartesian distance between the two points.</returns>
		public long Measure(UnsignedPoint reference)
		{
			return SquareDistance(reference);
		}

		/// <summary>
		/// Cartesian distance between two points.
		/// </summary>
		/// <param name="other">Second point for distance computation.</param>
		/// <returns>The distance between the two points.</returns>
		public double Distance(UnsignedPoint other)
		{
			var squareDistance = SquareDistance(other);
			return Math.Sqrt(squareDistance);
		}

		#endregion

		#region IHaveIntegerCoordinates interface

		/// <summary>
		/// Get the largest value from among all the coordinates.
		/// </summary>
		/// <returns>The largest coordinate value.</returns>
		public int Range()
		{
			return (int)Coordinates.Max();
		}


		public int GetDimensions()
		{
			return Dimensions;
		}

		public IEnumerable<int> GetCoordinates()
		{
			return Coordinates.Select(u => (int)u);
		}

		#endregion

		#region ToString

		/// <summary>
		/// When calling the parameterless ToString method, use only show this many coordinates.
		/// </summary>
		public static readonly int MaxCoordinatesToShow = 10;

		/// <summary>
		/// Compose a possibly abbreviated string representation of the point including at most dimensions zero through MaxCoordinatesToShow - 1.
		/// </summary>
		/// <returns>Possibly abbreviated string representation of the point.</returns>
		public override string ToString()
		{
			return AsString(MaxCoordinatesToShow);
		}

		/// <summary>
		/// Compose a string representation of the point but only show dimensions zero through maxCoordinatesToShow - 1.
		/// </summary>
		/// <param name="maxCoordinatesToShow">Number of coordinates to include in string.
		/// If there are more than this number, put "..." at the end.
		/// If zero, then include all dimensions.</param>
		/// <returns>Possibly abbreviated string representation of the point.</returns>
		public string AsString(int maxCoordinatesToShow = 0)
		{
			var sb = new StringBuilder();
			sb.Append('[');
			var limit = Math.Min(maxCoordinatesToShow == 0 ? Dimensions : maxCoordinatesToShow, Dimensions);
			for (var dim = 0; dim < limit; dim++)
			{
				if (dim > 0) sb.Append(',');
				sb.Append(Coordinates[dim]);
			}
			if (limit < Dimensions)
				sb.Append(",...");
			sb.Append(']');
			return sb.ToString();
		}

		#endregion


	}
}
