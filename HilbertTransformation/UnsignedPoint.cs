using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HilbertTransformation.Random;
using static System.Math;

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
		public int UniqueId { get; protected set; }

		protected static int NextId() { return Interlocked.Increment(ref _counter); }

		#endregion

		#region Properties (Coordinates, Dimensions, etc)

        /// <summary>
        /// Holds the coordinate values for the point.
        /// 
        /// A sparse subclass may use this to hold just the non-missing values.
        /// </summary>
        protected uint[] _coordinates;

		/// <summary>
		/// Coordinates of the point in N-space.
        /// 
        /// Subclasses that use a sparse representation will be required to fully realize the
        /// coordinates in a non-sparse array.
		/// </summary>
		public virtual uint[] Coordinates { 
			get { return _coordinates; } 
			protected set { _coordinates = value; } 
		}

        /// <summary>
        /// Permit iterating over coordinate values without having to fully realize the array of coordinates, in case
        /// a subclass is sparse. 
        /// </summary>
        public virtual IEnumerable<uint> LazyCoordinates()
        {
            foreach (var x in _coordinates)
                yield return x;
        }

		/// <summary>
		/// Number of dimensions for the point.
		/// </summary>
		public int Dimensions { get; private set; }

        /// <summary>
        /// Maximum value in the Coordinates array.
        /// </summary>
        public long MaxCoordinate { get; private set; } = 0;

        /// <summary>
        /// Square of the distance from the point to the origin.
        /// </summary>
        public long SquareMagnitude { get; protected set; } = 0;

		double _magnitude = double.NaN;

		/// <summary>
		/// Cartesian distance from the point to the origin.
		/// </summary>
		/// <value>The magnitude.</value>
		public double Magnitude { 
			get
			{
				// Lazy computation.
				if (double.IsNaN(_magnitude))
					_magnitude = Math.Sqrt(SquareMagnitude);
				return _magnitude;
			}
		}

        /// <summary>
        /// This should not be called in any contructor invoked by a subclass that
        /// overrides LazyCoordinates since that method is not available
        /// until after the object is contructed. The subclass should call the method
        /// itself to initialize the hashcode.
        /// </summary>
		protected void InitInvariants(bool lazy = false)
        {
            if (lazy)
            {
                _hashCode = ComputeHashCode(LazyCoordinates(), Dimensions);
                if (MaxCoordinate == 0 && SquareMagnitude == 0)
                {
                    foreach (var x in LazyCoordinates())
                    {
                        MaxCoordinate = Max(MaxCoordinate, x);
                        SquareMagnitude += x * (long)x;
                    }
                }
            }
            else
            {
                _hashCode = ComputeHashCode(_coordinates, Dimensions);
                if (MaxCoordinate == 0 && SquareMagnitude == 0)
                {
                    foreach (var x in _coordinates)
                    {
                        MaxCoordinate = Max(MaxCoordinate, x);
                        SquareMagnitude += x * (long)x;
                    }
                }
            }
        }

		#endregion

		#region Indexers

		/// <summary>
		/// Access the coordinate values as signed integers.
		/// </summary>
		/// <param name="i">Index, which must be between zero and Dimensions - 1, inclusive.</param>
		/// <returns>Coordinate value as an integer.</returns>
		public virtual int this[int i] { get { return (int)Coordinates[i]; } }

		#endregion

		#region Constructors and helpers

		/// <summary>
		/// Construct a UnsignedPoint given its N-dimensional coordinates as unsigned integers.
		/// </summary>
		/// <param name="coordinates">Coordinate values as unsigned integers.</param>
		public UnsignedPoint(uint[] coordinates): this(coordinates, 0L, 0L)
		{
			InitInvariants();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:HilbertTransformation.UnsignedPoint"/> class.
		/// </summary>
		/// <param name="coordinates">Coordinates.</param>
		/// <param name="id">Optional Identifier. If not specified, an id is automatically generated.</param>
		public UnsignedPoint(IList<uint> coordinates, int id = -1): this(coordinates.ToArray())
		{
			if (id >= 0)
				UniqueId = id;
		}

		/// <summary>
		/// Construct a UnsignedPoint given its N-dimensional coordinates as signed integers.
		/// </summary>
		/// <param name="coordinates">Coordinate values as signed integers.</param>
		public UnsignedPoint(int[] coordinates) : this(coordinates.Select(i => (uint)i).ToArray())
		{
		}

		/// <summary>
		/// Conversion constructor used when converting from UnsignedPoint to HilbertPoint and acquiring a new id.
		/// </summary>
		/// <param name="coordinates">Coordinates of the point.</param>
		/// <param name="maxCoordinate">Maximum value of all items in Coordinates array.</param>
		/// <param name="squareMagnitude">Square of the distance to the origin.</param>
		protected UnsignedPoint(uint[] coordinates, long maxCoordinate, long squareMagnitude)
		{
			UniqueId = NextId();
			_coordinates = coordinates;
            Dimensions = _coordinates.Length;
            _hashCode = ComputeHashCode(coordinates, Dimensions);
			MaxCoordinate = maxCoordinate;
			SquareMagnitude = squareMagnitude;
		}

		/// <summary>
		/// Construct an UnsignedPoint by supplying most of its attributes, even its UniqueId.
		/// 
		/// This is useful when permuting a HilbertPoint.
		/// </summary>
		/// <param name="coordinates">Coordinates.</param>
		/// <param name="maxCoordinate">Max value of any coordinate.</param>
		/// <param name="squareMagnitude">Square magnitude of distance to origin.</param>
		/// <param name="uniqueId">Unique identifier.</param>
		protected UnsignedPoint(uint[] coordinates, long maxCoordinate, long squareMagnitude, int uniqueId)
		{
			UniqueId = uniqueId >= 0 ? uniqueId : NextId();
			_coordinates = coordinates;
            Dimensions = _coordinates.Length;
            _hashCode = ComputeHashCode(coordinates, Dimensions);
			MaxCoordinate = maxCoordinate;
			SquareMagnitude = squareMagnitude;
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
		protected static uint[] PermuteAndMakeUnsigned(IList<int> p, Permutation<int> permutation)
		{
			return permutation.ApplyToArray<uint>(p, (int x) => (uint) x);
		}

		#region Implement Clone interface

		/// <summary>
		/// Clone constructor.
		/// </summary>
		/// <param name="original">Source from which to copy.</param>
		protected UnsignedPoint(UnsignedPoint original)
			:this((uint[])original.Coordinates.Clone(), original.MaxCoordinate, original.SquareMagnitude)
		{
		}

		/// <summary>
		/// Clone this instance, but generate a new UniqueId.
		/// </summary>
		public virtual object Clone() { return new UnsignedPoint(this); }

        #endregion

        #endregion

        #region Equality and Hash Code

        private int _hashCode;

        private static int ComputeHashCode(IEnumerable<uint> vector, int vectorLength)
        {
            uint seed = (uint)vectorLength;
            foreach (var i in vector)
            {
                seed ^= i + 0x9e3779b9 + (seed << 6) + (seed >> 2);
            }
            return (int)seed;
        }

		public override int GetHashCode() { return _hashCode; }

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
		public virtual int CompareTo(UnsignedPoint other)
		{
			return UniqueId.CompareTo(other.UniqueId);
		}

		/// <summary>
		/// Square of the cartesian distance between two points.
		/// </summary>
		/// <param name="other">Second point for distance computation.</param>
		/// <returns>The square of the distance between the two points.</returns>
		public virtual long SquareDistance(UnsignedPoint other)
		{
			return SquareDistanceDotProduct(
				Coordinates, other.Coordinates, SquareMagnitude, other.SquareMagnitude, MaxCoordinate, other.MaxCoordinate
			);
		}

		/// <summary>
		/// Computes the square of the distance between two points using the dot product and precomputed square distances
		/// from the points to the origin.
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
			long sqDist;
			if (xMax * yMax * unroll < uint.MaxValue)
				sqDist = SquareDistanceDotProductNoOverflow(x, y, xMag2, yMag2);
			else {
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
				sqDist = xMag2 + yMag2 - 2L * (long)dotProduct;
			}
			return sqDist;
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
		/// Compare whether the distance between two points is less than, equal to, or greater than a given square distance.
		/// </summary>
		/// <returns>-1 if the square Distance is less than squareDistance.
		/// 0 if the square Distance matches squareDistance.
		/// +1 if the square Distance is greater than squareDistance. </returns>
		/// <param name="other">Other.</param>
		/// <param name="squareDistance">Square distance.</param>
		public virtual int SquareDistanceCompare(UnsignedPoint other, long squareDistance)
		{
			// If two points are on a one-dimensional number line, then if they are both on the same side of the origin
			// (both positive, for example), then the difference between their distances from the origin 
			// equals their distance from each other.
			// Contrariwise, if they are on opposite sides of the origin, their distance from each other is the sum
			// of their distances from the origin. Thus the possible range for their distance from each other is:
			//
			//    |A-B| <= Distance <= A+B
			// 
			// where A is the magnitude of the first vector and B is the magnitude of the second vector.
			// If two points are in N-dimensional space and we exclude points in the negative quadrants, 
			// the extreme distance is when each point lies along an axis and the two axes are different:
			//                           ________
			//                          / 2    2
			//    |A-B| <= Distance <= √(A  + B )
			//
			// The compensation for not permitting negative values is this:
			//
			//           2     2    2
			//    (A + B)  - (A  + B ) = 2AB
			//
			// By using precomputed values for the magnitudes of points A and B, we can sometimes deduce
			// whether their distance is less than, equal to, or greater than a given threshhold distance
			// without having to compute the actual distance.
			// Because of the poor contrast in distances between points in high-dimensional space, this
			// optimization is likely to become less useful as the number of dimensions increases.
			// 
			// First: Lower bound on distance
			var delta = Magnitude - other.Magnitude;
			var low = (long)Math.Floor(delta * delta);
			if (squareDistance < low)
				return 1;

			// Second: Upper bound on distance
			var high = SquareMagnitude + other.SquareMagnitude;
			if (squareDistance > high)
				return -1;

			var trueSquareDistance = SquareDistance(other);
			return trueSquareDistance.CompareTo(squareDistance);
		}

		#region IMeasurable implementation

		/// <summary>
		/// Measure the square of the Cartesian distance between two points.
		/// </summary>
		/// <param name="reference">Second point for comparison.</param>
		/// <returns>Square of the Cartesian distance between the two points.</returns>
		public long Measure(UnsignedPoint reference)
		{
			return SquareDistance(reference);
		}

		#endregion

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
			return (int)LazyCoordinates().Max();
		}


		public int GetDimensions()
		{
			return Dimensions;
		}

		public IEnumerable<int> GetCoordinates()
		{
			return LazyCoordinates().Select(u => (int)u);
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
            var dim = 0;
			foreach (var x in LazyCoordinates().Take(limit))
			{
                dim++;
				if (dim > 0) sb.Append(',');
				sb.Append(x);
			}
			if (limit < Dimensions)
				sb.Append(",...");
			sb.Append(']');
			return sb.ToString();
		}

		#endregion

		/// <summary>
		/// Create a new point that has an extra dimension tacked on whose coordinate value is as given.
		/// </summary>
		/// <returns>The new point.</returns>
		/// <param name="point">Point to extend.</param>
		/// <param name="coordinate">Coordinate value for new dimension.</param>
		public virtual UnsignedPoint AppendCoordinate(uint coordinate)
		{
			var p = Coordinates.ToList();
			p.Add(coordinate);
			return new UnsignedPoint(p);
		}

		/// <summary>
		/// Compute the Centroid of a collection of points.
		/// </summary>
		/// <param name="points">Points whose centroid is sought.</param>
		/// <returns>The centroid of the collection of points, or null if the collection is empty.
		/// If the points are a subtype of UnsignedPoint, this will NOT preserve the type of the result.
		/// </returns>
		public static UnsignedPoint Centroid(IEnumerable<UnsignedPoint> points)
		{
			double[] sums = null;
			var numPoints = 0;
			foreach (var p in points)
			{
				if (sums == null)
				{
					sums = new double[p.Dimensions];
				}
                var dim = 0;
				foreach (var x in p.LazyCoordinates())
					sums[dim++] += x;
				numPoints++;
			}
			if (numPoints == 0) return null;
			var coords = new uint[sums.Length];
			for (var dim = 0; dim < sums.Length; dim++)
				coords[dim] = (uint)Math.Round(sums[dim] / numPoints);
			return new UnsignedPoint(coords);
		}


	}
}
