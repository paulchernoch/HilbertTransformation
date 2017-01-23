using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace HilbertTransformation
{

	/// <summary>
	/// A vector for which one can get integer coordinate values and compute the largest value across all of its dimensions.
	/// This mapping may require scaling, translating, or loss of precision.
	/// </summary>
	public interface IHaveIntegerCoordinates
	{
		/// <summary>
		/// Largest value among all coordinates.
		/// </summary>
		/// <returns></returns>
		int Range();

		/// <summary>
		/// Number of dimensions.
		/// </summary>
		/// <returns></returns>
		int GetDimensions();

		IEnumerable<int> GetCoordinates();
	}

    /// <summary>
    /// Maps an immutable, N-dimensional point to a 1-Dimensional Hilbert index.
    /// 
    /// These points can be sorted by the HilbertIndex or their Cartesian distance to a given reference point.
    /// By sorting points by their HilbertIndex, the Nearest Neighbor problem can be solved more efficiently,
    /// since items whose 1-Dimensional HilbertIndex values are close are also close in N-Dimensional space.
	/// 
	/// To permit HilbertPoints to be added to collections and multiple points at the same coordinate to be distinguished
	/// from one another, a unique Id is maintained for each point, which is used as a tie-breaker in sorting.
    /// </summary>
	public class HilbertPoint : IEquatable<HilbertPoint>, IComparable<HilbertPoint>, 
								ICloneable, IHaveIntegerCoordinates,
								IMeasurable<HilbertPoint, long>

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

        #region Properties (HilbertIndex, Coordinates, DimensionDefinitions, BitsPerDimension)

        /// <summary>
        /// This point's distance from the origin along the Hilbert curve.
        /// 
        /// This is its coordinate in 1-Space.
        /// </summary>
        public BigInteger HilbertIndex { get; private set; }

        /// <summary>
        /// Coordinates of the point in N-space.
        /// </summary>
        public uint[] Coordinates { get; private set; }

        /// <summary>
        /// Number of dimensions for the point.
        /// </summary>
        public int Dimensions { get; private set; }

        /// <summary>
        /// Number of bits used to encode each individual coordinate when converting it into a Hilbert index.
        /// </summary>
        public int BitsPerDimension { get; private set; }

		/// <summary>
		/// Maximum value in the Coordinates array.
		/// </summary>
		private long MaxCoordinate { get; set; }

		/// <summary>
		/// Square of the distance from the point to the origin.
		/// </summary>
		private long SquareMagnitude { get; set; }

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
        public int this[int i] { get { return (int) Coordinates[i]; } }

        #endregion

        #region Constructors and helpers

        /// <summary>
        /// Transform a list of points expressed as IList(int) into HilbertPoints.
        /// 
        /// If the BitsPerDimension is not given, it will be inferred by studying the values of all the points.
        /// </summary>
        /// <param name="points">Points to transform.</param>
        /// <param name="bitsPerDimension">If zero, this value will be inferred.
        /// Otherwise, this is the log base two of the largest value taken from all points and all dimensions plus one.</param>
        /// <returns>A new list of HilberPoints, where each corresponds to one of the original points.</returns>
        public static List<HilbertPoint> Transform(IList<IList<int>> points, int bitsPerDimension = 0)
        {
            if (bitsPerDimension <= 0)
            {
                bitsPerDimension = FindBitsPerDimension(points);
            }
            return points.Select(point => new HilbertPoint(point, bitsPerDimension)).ToList();
        }

        /// <summary>
        /// Transform a list of points expressed as IList(int) into HilbertPoints.
        /// 
        /// If the BitsPerDimension is not given, it will be inferred by studying the values of all the points.
        /// </summary>
        /// <param name="points">Points to transform.</param>
        /// <param name="bitsPerDimension">If zero, this value will be inferred.
        /// Otherwise, this is the log base two of the largest value taken from all points and all dimensions plus one.</param>
        /// <param name="permutation">Scramble the coordinates using this mapping:
        ///   target[i] = source[permutation[i]]
        /// permutation must have as many elements as there are dimensions to the coordinates.
        /// </param>
        /// <returns>A new list of HilberPoints, where each corresponds to one of the original points.</returns>
        public static List<HilbertPoint> Transform(IList<IList<int>> points, int bitsPerDimension, int[] permutation)
        {
            return points.Select(point => new HilbertPoint(point, bitsPerDimension, permutation)).ToList();
        }

        public static List<HilbertPoint> Transform(IList<IList<int>> points, int[] permutation)
        {
            var bitsPerDimension = FindBitsPerDimension(points);
            return points.Select(point => new HilbertPoint(point, bitsPerDimension, permutation)).ToList();
        }

        /// <summary>
        /// Construct a HibertPoint given its N-dimensional coordinates.
        /// </summary>
        /// <param name="coordinates">Coordinate values as unsigned integers.</param>
        /// <param name="bitsPerDimension">Number of bits with which to encode each coordinate value.</param>
        public HilbertPoint(uint[] coordinates, int bitsPerDimension)
        {
            Coordinates = coordinates;
            Dimensions = Coordinates.Length;
            BitsPerDimension = bitsPerDimension;
            HilbertIndex = coordinates.HilbertIndex(BitsPerDimension);
			InitInvariants();
        }

        /// <summary>
        /// Construct a HilbertPoint when you have signed coordinate values and their maximum ranges.
        /// 
        /// Note: All coordinate values must be non-negative.
        /// </summary>
        /// <param name="coordinates">Coordinates for the point in N-space.</param>
        /// <param name="coordinateRanges">Maximum value for each corresponding dimension.</param>
        public HilbertPoint(IList<int> coordinates, IEnumerable<int> coordinateRanges)
            : this(MakeUnsigned(coordinates), FindBitsPerDimension(coordinateRanges))
        {
        }

        /// <summary>
        /// Construct a HilbertPoint when you have signed coordinate values and the maximum number of bits required to encode each coordinate value.
        /// </summary>
        /// <param name="coordinates">Coordinates for the point in N-space.</param>
        /// <param name="bitsPerDimension">Maximum number of bits required to encode each coordinate value.</param>
        public HilbertPoint(IList<int> coordinates, int bitsPerDimension)
            : this(MakeUnsigned(coordinates), bitsPerDimension)
        {
        }

        /// <summary>
        /// Construct a HilbertPoint using permuted coordinates.
        /// </summary>
        /// <param name="coordinates">Coordinates for the point in N-space.</param>
        /// <param name="bitsPerDimension">Maximum number of bits required to encode each coordinate value.</param>
        /// <param name="permutation">Scramble the coordinates using this mapping:
        ///   target[i] = source[permutation[i]]
        /// permutation must have as many elements as there are dimensions to the coordinates.
        /// </param>
        public HilbertPoint(IList<int> coordinates, int bitsPerDimension, int[] permutation)
            : this(PermuteAndMakeUnsigned(coordinates, permutation), bitsPerDimension)
        {
        }

        /// <summary>
        /// Deduce the log base two of the smallest power of two that exceeds the largest value in the range of any dimension.
        /// 
        /// Examples:
        ///   0 ... 1
        ///   1 ... 1
        ///   2 ... 2
        ///   3 ... 2
        ///   4 ... 3
        /// 
        /// Note that at each exact power of two the number of bits increases by one.
        /// </summary>
        /// <param name="maxValuePerDimension">Range for each corresponding dimension.</param>
        /// <returns>Maximum number of bits needed to encode any value between zero and the maximum allowed value of the dimension with the largest range.</returns>
        public static int FindBitsPerDimension(IEnumerable<int> maxValuePerDimension)
        {
            var max = maxValuePerDimension.Max();
            // Add one, because if the range is 0 to N, we need to represent N+1 different values.
            return (max+1).SmallestPowerOfTwo();
        }

        /// <summary>
        /// Find the number of bits needed to encode the largest value in any coordinate of any point in the collection.
        /// </summary>
        /// <param name="points">Points to measure.</param>
        /// <returns>Maximum number of bits.</returns>
        public static int FindBitsPerDimension(IList<IList<int>> points)
        {
            var max = 1;
            foreach (var point in points)
            {
                foreach (var coordinate in point)
                {
                    // We could use Math.Max, but all we care about is the highest order bit.
                    // This approach has a small chance of overestimating the number of bits by 1.
                    max = max | coordinate;
                }
            }
            return (max + 1).SmallestPowerOfTwo();
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

        /// <summary>
        /// Create a HilbertPoint given its Hilbert Index.
        /// </summary>
        /// <param name="hilbertIndex">Distance from the origin along the Hilbert Curve to the desired point.</param>
        /// <param name="dimensions">Number of dimensions for the point.</param>
        /// <param name="bitsPerDimension">Number of bits used to encode each dimension.
        /// This is also the number of fractal iterations of the Hilbert curve.</param>
        public HilbertPoint(BigInteger hilbertIndex, int dimensions, int bitsPerDimension)
        {
            HilbertIndex = hilbertIndex;
            Coordinates = HilbertIndex.HilbertAxes(bitsPerDimension, dimensions);
            Dimensions = Coordinates.Length;
            BitsPerDimension = bitsPerDimension;
			InitInvariants();
        }

		#region Implement Clone interface

		/// <summary>
		/// Clone constructor.
		/// </summary>
		private HilbertPoint(HilbertPoint original)
		{
			Coordinates = (uint[])original.Coordinates.Clone();
			Dimensions = original.Dimensions;
			BitsPerDimension = original.BitsPerDimension;
			HilbertIndex = original.HilbertIndex;
			MaxCoordinate = original.MaxCoordinate;
			SquareMagnitude = original.SquareMagnitude;
		}

		public object Clone() { return new HilbertPoint(this); }

		#endregion

        #endregion

        #region Equality and Hash Code

        public override int GetHashCode() { return UniqueId; }

		/// <summary>
		/// A HilbertPoint only equals a second point if they share the same UniqueId.
		/// </summary>
		/// <param name="obj">The <see cref="object"/> to compare with the current <see cref="T:HilbertTransformation.HilbertPoint"/>.</param>
		/// <returns>True if the objects are both HilbertPoints and share the same id and false otherwise.</returns>
        public override bool Equals(object obj)
        {
            var p = obj as HilbertPoint;
            return p != null && Equals(p);
        }

        public bool Equals(HilbertPoint other)
        {
			return UniqueId == other.UniqueId;
        }

        #endregion

        #region Comparing, Measuring Distance and Sorting

        /// <summary>
        /// Compare the HilbertIndex values of the two points, but use the UniqueId as a tie-breaker.
        /// 
        /// This permits sorting by HilbertIndex.
        /// </summary>
        /// <param name="other">Second point in comparison.</param>
        /// <returns>-1 if this has a lower index, 0 if they match, and +1 if this has a higher index.</returns>
        public int CompareTo(HilbertPoint other)
        {
            var cmp = HilbertIndex.CompareTo(other.HilbertIndex);
			if (cmp == 0)
				cmp = UniqueId.CompareTo(other.UniqueId);
			return cmp;
        }

        /// <summary>
        /// Square of the cartesian distance between two points.
        /// </summary>
        /// <param name="other">Second point for distance computation.</param>
        /// <returns>The square of the distance between the two points.</returns>
        public long SquareDistance(HilbertPoint other)
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
		public long Measure(HilbertPoint reference)
		{
			return SquareDistance(reference);
		}

        /// <summary>
        /// Cartesian distance between two points.
        /// </summary>
        /// <param name="other">Second point for distance computation.</param>
        /// <returns>The distance between the two points.</returns>
        public double Distance(HilbertPoint other)
        {
            var squareDistance = SquareDistance(other);
            return Math.Sqrt(squareDistance);
        }

		/// <summary>
		/// Compute the distance along the Hilbert curve between the two points,
		/// which is the absolute difference in their Hilbert indices.
		/// </summary>
		/// <returns>The distance between the two points.</returns>
		/// <param name="other">The second point for the comparison.</param>
		public BigInteger HilbertDistance(HilbertPoint other)
		{
			return BigInteger.Abs(HilbertIndex - other.HilbertIndex);
		}

        /// <summary>
        /// In-place sort of the points by their HilbertIndex.
        /// </summary>
        /// <param name="points">List of points to sort.</param>
        public static void SortByHilbertIndex(List<HilbertPoint> points)
        {
            points.Sort();
        }

        #endregion

		#region IHaveIntegerCoordinates interface

		/// <summary>
		/// Get the largest value from among all the coordinates.
		/// </summary>
		/// <returns>The largest coordinate value.</returns>
		public int Range()
		{
			return (int) Coordinates.Max();
		}


		public int GetDimensions()
		{
			return Dimensions;
		}

		public IEnumerable<int> GetCoordinates()
		{
			return Coordinates.Select(u => (int) u);
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
