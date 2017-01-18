using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HilbertTransformation
{
    /// <summary>
    /// Maps an immutable, N-dimensional point to a 1-Dimensional Hilbert index.
    /// 
    /// These points can be sorted by the HilbertIndex or their Cartesian distance to a given reference point.
    /// By sorting points by their HilbertIndex, the Nearest Neighbor problem can be solved more efficiently,
    /// since items whose 1-Dimensional HilbertIndex values are close are also close in N-Dimensional space.
    /// </summary>
	public class HilbertPoint : IEquatable<HilbertPoint>, IComparable<HilbertPoint>
    {
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
            _hashCode = ComputeHashCode();
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
            _hashCode = ComputeHashCode();
        }

        #endregion

        #region Equality and Hash Code

        private readonly int _hashCode;

        public override int GetHashCode() { return _hashCode; }

        private int ComputeHashCode()
        {
            var hash = HilbertIndex.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            var p = obj as HilbertPoint;
            return p != null && Equals(p);
        }

        public bool Equals(HilbertPoint other)
        {
            return HilbertIndex.Equals(other.HilbertIndex);
        }

        #endregion

        #region Comparing, Measuring Distance and Sorting

        /// <summary>
        /// Compare the HilbertIndex values of the two points.
        /// 
        /// This permits sorting by HilbertIndex.
        /// </summary>
        /// <param name="other">Second point in comparison.</param>
        /// <returns>-1 if this has a lower index, 0 if they match, and +1 if this has a higher index.</returns>
        public int CompareTo(HilbertPoint other)
        {
            return HilbertIndex.CompareTo(other.HilbertIndex);
        }

        /// <summary>
        /// Square of the cartesian distance between two points.
        /// </summary>
        /// <param name="other">Second point for distance computation.</param>
        /// <returns>The square of the distance between the two points.</returns>
        public long SquareDistance(HilbertPoint other)
        {
            long squareDistanceLoopUnrolled;
            if (true)
            {
                // Unroll the loop partially to improve speed. (2.7x improvement!)
                var x = Coordinates;
                var y = other.Coordinates;
                var distance = 0UL;
                var leftovers = x.Length%4;
                var dimensions = x.Length;
                var roundDimensions = dimensions - leftovers;

                for (var i = 0; i < roundDimensions; i += 4)
                {
                    var x1 = x[i];
                    var y1 = y[i];
                    var x2 = x[i + 1];
                    var y2 = y[i + 1];
                    var x3 = x[i + 2];
                    var y3 = y[i + 2];
                    var x4 = x[i + 3];
                    var y4 = y[i + 3];
                    var delta1 = x1 > y1 ? x1 - y1 : y1 - x1;
                    var delta2 = x2 > y2 ? x2 - y2 : y2 - x2;
                    var delta3 = x3 > y3 ? x3 - y3 : y3 - x3;
                    var delta4 = x4 > y4 ? x4 - y4 : y4 - x4;
                    distance += delta1*delta1 + delta2*delta2 + delta3*delta3 + delta4*delta4;
                }
                for (var i = roundDimensions; i < dimensions; i++)
                {
                    var xi = x[i];
                    var yi = y[i];
                    var delta = xi > yi ? xi - yi : yi - xi;
                    distance += delta*delta;
                }
                squareDistanceLoopUnrolled = (long)distance;
            }
            return squareDistanceLoopUnrolled;
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
        /// In-place sort of the points by their HilbertIndex.
        /// </summary>
        /// <param name="points">List of points to sort.</param>
        public static void SortByHilbertIndex(List<HilbertPoint> points)
        {
            points.Sort();
        }


        #endregion
    }
}
