using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HilbertTransformation.Random;

namespace HilbertTransformation
{

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
	public class HilbertPoint : UnsignedPoint,
		IEquatable<HilbertPoint>, IComparable<HilbertPoint>, 
		IMeasurable<HilbertPoint, long>
	{
        #region Properties (HilbertIndex, Dimensions, BitsPerDimension, etc)

        /// <summary>
        /// This point's distance from the origin along the Hilbert curve.
        /// 
        /// This is its coordinate in 1-Space.
        /// </summary>
        public BigInteger HilbertIndex { get; private set; }

        /// <summary>
        /// Number of bits used to encode each individual coordinate when converting it into a Hilbert index.
        /// </summary>
        public int BitsPerDimension { get; private set; }

		Triangulator _triangulation;
		Triangulator Triangulation { 
			get {
				if (_triangulation == null)
					_triangulation = new Triangulator(this, BitsPerDimension);
				return _triangulation;
			} 
			set
			{
				_triangulation = value;
			}
		}

		public int NumTriangulationPoints
		{
			get { return Triangulation.NumTriangulationPoints; }
			set {
				if (_triangulation == null || _triangulation.NumTriangulationPoints != value)
					Triangulation = new Triangulator(this, BitsPerDimension, value);
			}
		}

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
        public static List<HilbertPoint> Transform(IList<IList<int>> points, int bitsPerDimension, Permutation<int> permutation)
        {
            return points.Select(point => new HilbertPoint(point, bitsPerDimension, permutation)).ToList();
        }

        public static List<HilbertPoint> Transform(IList<IList<int>> points, Permutation<int> permutation)
        {
            var bitsPerDimension = FindBitsPerDimension(points);
            return points.Select(point => new HilbertPoint(point, bitsPerDimension, permutation)).ToList();
        }

        /// <summary>
        /// Construct a HibertPoint given its N-dimensional coordinates.
        /// </summary>
        /// <param name="coordinates">Coordinate values as unsigned integers.</param>
        /// <param name="bitsPerDimension">Number of bits with which to encode each coordinate value.</param>
		public HilbertPoint(uint[] coordinates, int bitsPerDimension) : base(coordinates)
        {
            BitsPerDimension = bitsPerDimension;
            HilbertIndex = coordinates.HilbertIndex(BitsPerDimension);
        }

		public HilbertPoint(IList<uint> coordinates, int bitsPerDimension) : this(coordinates.ToArray(), bitsPerDimension)
		{
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
        public HilbertPoint(IList<int> coordinates, int bitsPerDimension, Permutation<int> permutation)
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

		public static int FindBitsPerDimension(int max)
		{
			// Add one, because if the range is 0 to N, we need to represent N+1 different values.
			return (max + 1).SmallestPowerOfTwo();
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
        /// Create a HilbertPoint given its Hilbert Index.
        /// </summary>
        /// <param name="hilbertIndex">Distance from the origin along the Hilbert Curve to the desired point.</param>
        /// <param name="dimensions">Number of dimensions for the point.</param>
        /// <param name="bitsPerDimension">Number of bits used to encode each dimension.
        /// This is also the number of fractal iterations of the Hilbert curve.</param>
        public HilbertPoint(BigInteger hilbertIndex, int dimensions, int bitsPerDimension) 
			: base(hilbertIndex.HilbertAxes(bitsPerDimension, dimensions))
        {
            HilbertIndex = hilbertIndex;
            BitsPerDimension = bitsPerDimension;
        }


		public HilbertPoint(uint[] coordinates, int bitsPerDimension, long maxCoordinate, long squareMagnitude) : base(coordinates, maxCoordinate, squareMagnitude)
		{
			BitsPerDimension = bitsPerDimension;
			HilbertIndex = coordinates.HilbertIndex(BitsPerDimension);
		}

		public HilbertPoint(uint[] coordinates, int bitsPerDimension, long maxCoordinate, long squareMagnitude, int key) : base(coordinates, maxCoordinate, squareMagnitude, key)
		{
			BitsPerDimension = bitsPerDimension;
			HilbertIndex = coordinates.HilbertIndex(BitsPerDimension);
		}

		/// <summary>
		/// If the point is already a HilbertPoint, return it unchanged, otherwise create a new one
		/// that contains the same coordinates yet has a Hilbert index.
		/// </summary>
		/// <returns>A HilbertPoint.</returns>
		/// <param name="uPoint">U point.</param>
		/// <param name="bitsPerDimension">Bits per dimension.</param>
		/// <param name="useSameKey">If true and a new point is created, it will share the same key as the original.</param>
		public static HilbertPoint CastOrConvert(UnsignedPoint uPoint, int bitsPerDimension, bool useSameKey = false)
		{
			HilbertPoint hPoint = uPoint as HilbertPoint;
			if (hPoint == null || hPoint.BitsPerDimension != bitsPerDimension)
			{
				if (useSameKey)
					hPoint = new HilbertPoint(uPoint.Coordinates, bitsPerDimension, uPoint.MaxCoordinate, uPoint.SquareMagnitude, uPoint.UniqueId);
				else
					hPoint = new HilbertPoint(uPoint.Coordinates, bitsPerDimension, uPoint.MaxCoordinate, uPoint.SquareMagnitude);
			}
			return hPoint; 
		}

		/// <summary>
		/// Create a new point that has the same coordinates as the original, but reordered by a permutation.
		/// </summary>
		/// <param name="point">Point to permute.</param>
		/// <param name="permutation">Permutation.</param>
		private HilbertPoint(HilbertPoint point, Permutation<uint> permutation)
			: base(Permute(point.Coordinates, permutation), point.MaxCoordinate, point.SquareMagnitude, point.UniqueId)
		{
			BitsPerDimension = point.BitsPerDimension;
			HilbertIndex = Coordinates.HilbertIndex(BitsPerDimension);
		}

		/// <summary>
		/// Permute the coordinates (reorder them).
		/// 
		/// Such permutation is useful when mapping the same points to many different Hilbert Curves with 
		/// altered orientation.
		/// </summary>
		/// <param name="coordinates">Coordinates before the permutation.</param>
		/// <param name="permutation">Maps from target coordinates to source coordinates:
		///    target[i] = source[permutation[i]]
		/// The array must contain all numbers from zero to Dimensions - 1 exactly once, in any order.
		/// </param>
		/// <returns>Array of unsigned integers containing all values from coordinates, but reordered.</returns>
		public static uint[] Permute(uint[] coordinates, Permutation<uint> permutation)
		{
			return permutation.ApplyToArray(coordinates);
		}

		/// <summary>
		/// Create a new point by permuting the coordinates of the original.
		/// 
		/// The new point will share the same UniqueId as the original.
		/// </summary>
		/// <param name="permutation">Permutation.</param>
		public HilbertPoint Permute(Permutation<uint> permutation)
		{
			return new HilbertPoint(this, permutation);
		}

		#region Implement Clone interface

		/// <summary>
		/// Clone constructor. The cloned object will have a different UniqueId.
		/// </summary>
		private HilbertPoint(HilbertPoint original): base(original)
		{
			BitsPerDimension = original.BitsPerDimension;
			HilbertIndex = original.HilbertIndex;
			Triangulation = original.Triangulation;
		}

		public override object Clone() { return new HilbertPoint(this); }

		#endregion

        #endregion

        #region Equality and Hash Code

		/// <summary>
		/// This is to silence compiler warnings about not overriding GetHashCode.
		/// </summary>
		public override int GetHashCode() { return base.GetHashCode(); }

		/// <summary>
		/// A HilbertPoint only equals a second point if they share the same UniqueId and are both HilbertPoints.
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
			return base.Equals(other);
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

		public override int SquareDistanceCompare(UnsignedPoint other, long squareDistance)
		{
			var hPoint = other as HilbertPoint;
			if (hPoint == null)
				return base.SquareDistanceCompare(other, squareDistance);
			if (Triangulation.ArePointsFartherForSure(hPoint.Triangulation, squareDistance))
				return 1;
			if (Triangulation.ArePointsNearerForSure(hPoint.Triangulation, squareDistance))
				return -1;
			return Measure(other).CompareTo(squareDistance);
		}

		/// <summary>
		/// Can Triangulation bypass the need to compute the ful distance?
		/// 
		/// THis method is only used for testing.
		/// </summary>
		/// <param name="other">Other.</param>
		/// <param name="squareDistance">Square distance.</param>
		public bool Triangulatable(UnsignedPoint other, long squareDistance)
		{
			var hPoint = other as HilbertPoint;
			if (hPoint == null)
				return false;
			if (Triangulation.ArePointsFartherForSure(hPoint.Triangulation, squareDistance))
				return true;
			if (Triangulation.ArePointsNearerForSure(hPoint.Triangulation, squareDistance))
				return true;
			return false;
		}

		#endregion

		#region IMeasurable implementation

		/// <summary>
		/// Measure the square of the Cartesian distance between two points.
		/// </summary>
		/// <param name="reference">Second point for comparison.</param>
		/// <returns>Square of the Cartesian distance between the two points.</returns>
		public long Measure(HilbertPoint reference)
		{
			return base.Measure(reference);
		}

		#endregion

		/// <summary>
		/// Create a new point that has an extra dimension tacked on whose coordinate value is as given.
		/// 
		/// This recomputes the Hilbert index.
		/// </summary>
		/// <returns>The new point.</returns>
		/// <param name="coordinate">Coordinate value for new dimension.
		/// If this value is too great to be accomodated by BitsPerDimension, an exception is thrown.</param>
		public override UnsignedPoint AppendCoordinate(uint coordinate)
		{
			int limit = 1 << BitsPerDimension;
			if (limit <= coordinate)
				throw new ArgumentOutOfRangeException(
					nameof(coordinate), 
					$"Value must be smaller than 2^BitsPerDimension, which is {limit}");
			var p = LazyCoordinates().ToList();
			p.Add(coordinate);
			return new HilbertPoint(p, BitsPerDimension);
		}

	}
}
