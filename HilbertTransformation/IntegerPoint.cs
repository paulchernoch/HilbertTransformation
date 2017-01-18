using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Multidimensional point with integer coordinates.
    /// </summary>
    public class IntegerPoint : IMeasurable<IntegerPoint, long>, IEquatable<IntegerPoint>, ICloneable, IHaveIntegerCoordinates
    {
        /// <summary>
        /// When calling the parameterless ToString method, use only show this many coordinates.
        /// </summary>
        public static readonly int MaxCoordinatesToShow = 10;

        /// <summary>
        /// Coordinate values.
        /// </summary>
        public readonly int[] Coordinates;

        /// <summary>
        /// Number of dimension for point.
        /// </summary>
        public readonly int Dimensions;

        private readonly int _hashCode;

        public IntegerPoint(IEnumerable<int> coordinates)
        {
            Coordinates = coordinates.ToArray();
            Dimensions = Coordinates.Length;
            _hashCode = HashCodeFromData(Coordinates);
        }

		/// <summary>
		/// Cartesian distance between two points, which is the square root of the Measure.
		/// </summary>
		/// <param name="reference">Second point for comparison.</param>
		public double Distance(IntegerPoint reference)
		{
			return Math.Sqrt(Measure(reference));
		}

        /// <summary>
        /// Measure the square of the Cartesian distance between two points.
        /// </summary>
        /// <param name="reference">Second point for comparison.</param>
        /// <returns>Square of the Cartesian distance between the two points.</returns>
        public long Measure(IntegerPoint reference)
        {
            var dimensions = Dimensions;
            var x = Coordinates;
            var y = reference.Coordinates;

            var squareDistance = 0L;
            var leftovers = dimensions % 4;
            var roundDimensions = dimensions - leftovers;
            // Unroll the loop partially for speed.
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
                var delta1 = x1 - y1;
                var delta2 = x2 - y2;
                var delta3 = x3 - y3;
                var delta4 = x4 - y4;
                squareDistance += delta1 * delta1 + delta2 * delta2 + delta3 * delta3 + delta4 * delta4;
            }
            for (var i = roundDimensions; i < dimensions; i++)
            {
                var delta = x[i] - y[i];
                squareDistance += delta * delta;
            }
            return squareDistance;
        }


        #region Equals, GetHashCode, ToString, Clone

        public override bool Equals(object obj)
        {
            return Equals(obj as IntegerPoint);
        }

        public virtual bool Equals(IntegerPoint other)
        {
            if (other == null) return false;
            if (GetHashCode() != other.GetHashCode()) return false;
            var length = Dimensions;
            if (other.Dimensions != length) return false;
            var thisCoordinates = Coordinates;
            var otherCoordinates = other.Coordinates;
            for (var i = 0; i < length; i++)
                if (thisCoordinates[i] != otherCoordinates[i]) return false;
            return true;
        }

        private static int HashCodeFromData(int[] coordinates)
        {
            var code = 17;
            foreach (var i in coordinates)
                code = code * 23 | i;
            return code;
        }

        protected virtual int ComputeHashCode()
        {
            return HashCodeFromData(Coordinates);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

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

        public virtual object Clone()
        {
            return new IntegerPoint((int[])Coordinates.Clone());
        }

        #endregion




        #region IHaveIntegerCoordinates interface

        /// <summary>
        /// Get the largest value from among all the coordinates.
        /// </summary>
        /// <returns>The largest coordinate value.</returns>
        public int Range()
        {
            return Coordinates.Max();
        }


        public int GetDimensions()
        {
            return Dimensions;
        }

        public IEnumerable<int> GetCoordinates()
        {
            return Coordinates;
        }

        #endregion
    }

    /// <summary>
    /// Permits one to distinguish one IntegerPoint from another using an auto-generated unique id.
    /// 
    /// This is necessary when performing nearest neighbor searches if there is a possibility of multiple records 
    /// resolving to the same IntegerPoint, since Dictionaries are used as concordances.
    /// 
    /// This differs from IntegerPoint only in its Equals methods. Two UniqueIntegerPoints are not
    /// equal unless they share the same UniqueId. However, their Measure does not take account of the
    /// UniqueId.
    /// </summary>
    public class UniqueIntegerPoint : IntegerPoint
    {
        /// <summary>
        /// Auto-incrementing counter to use for generating unique ids.
        /// 
        /// This is incremented in a thread-safe manner.
        /// </summary>
        private static int _counter;

        private readonly int _uniqueId;

        /// <summary>
        /// Unique id for point.
        /// </summary>
        public int UniqueId { get { return _uniqueId; } }

        public UniqueIntegerPoint(IEnumerable<int> coordinates)
            : base(coordinates)
        {
            _uniqueId = NextId();
        }

        #region Equals, GetHashCode, Clone

        public override bool Equals(object obj)
        {
            return Equals(obj as UniqueIntegerPoint);
        }

        public override bool Equals(IntegerPoint other)
        {
            var other2 = other as UniqueIntegerPoint;
            return other2 != null && _uniqueId == other2._uniqueId;
        }

        private static int NextId(){ return Interlocked.Increment(ref _counter); }

        protected override int ComputeHashCode(){ return NextId(); }

        public override int GetHashCode(){ return _uniqueId; }

        public override object Clone(){ return new UniqueIntegerPoint((int[])Coordinates.Clone()); }

        #endregion
    }
}
