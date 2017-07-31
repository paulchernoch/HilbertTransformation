using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;

namespace HilbertTransformation
{
    /// <summary>
    /// A multi-dimensional point where most values are missing, and all missing values are assumed to have the same value,
    /// such as zero.
    /// </summary>
    public class SparsePoint : UnsignedPoint
    {

        #region Attributes (DimensionIndices, Coordinates, MissingValue)

        /// <summary>
        /// Each index in this array corresponds to a value in SparseCoordinates.
        /// Together they specify the sparse values in the point.
        /// The coordinate at any index not in this array is assumed to equal MissingValue.
        /// </summary>
        private int[] DimensionIndices { get; set; }

        /// <summary>
        /// Coordinate values that correspond to DimensionIndices.
        /// </summary>
        public uint[] SparseCoordinates { get; set; }

        /// <summary>
        /// Expand the sparse coordinate array to a full size array.
        /// </summary>
        public override uint[] Coordinates
        {
            get
            {
                return _coordinates = _coordinates ?? ExpandSparseCoordinates(DimensionIndices, SparseCoordinates, Dimensions, MissingValue);
            }
            protected set { _coordinates = value; }
        }

        private static uint[] ExpandSparseCoordinates(IList<int> dimensionIndices, IList<uint> sparseCoordinates, int dimensions, uint missingValue)
        {
            var coordinates = new uint[dimensions];
            if (missingValue != 0)
                for (var i = 0; i < dimensions; i++)
                    coordinates[i] = missingValue;

            for (var iSparse = 0; iSparse < dimensionIndices.Count; iSparse++)
            {
                coordinates[dimensionIndices[iSparse]] = sparseCoordinates[iSparse];
            }
            return coordinates;
        }

        private static uint[] ExpandSparseCoordinates(Dictionary<int, uint> sparseCoordinates, int dimensions, uint missingValue)
        {
            return ExpandSparseCoordinates(
                  sparseCoordinates.Keys.OrderBy(k => k).ToArray()
                , sparseCoordinates.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray()
                , dimensions       
                , missingValue
            );
        }

        public override IEnumerable<uint> LazyCoordinates()
        {
            var i = 0;
            for (var iDim = 0; iDim < Dimensions; iDim++)
            {
                if (i < DimensionIndices.Length && iDim == DimensionIndices[i])
                    yield return SparseCoordinates[i++];
                else
                    yield return MissingValue;
            }
        }

        /// <summary>
        /// If a coordinate is missing, assume it has this value.
        /// </summary>
        public uint MissingValue { get; private set; } = 0;

        #endregion


        #region Constructors and Clone

        /// <summary>
        /// Create a SparsePoint from a Dictionary and optionally assign it an auto-incremented id.
        /// </summary>
        /// <param name="sparseCoordinates">Key is the zero-based dimension index, value is the coordinate value.</param>
        /// <param name="dimensions">Total number of dimensions.</param>
        /// <param name="missingValue">If a dimension is not included in the dictionary, use this value.</param>
        /// <param name="optionalId">If not -1, use this as the UniqueId instead of an auto-incremented value.</param>
        public SparsePoint(Dictionary<int,uint> sparseCoordinates, int dimensions, uint missingValue, int optionalId = -1) : 
            base(ExpandSparseCoordinates(sparseCoordinates, dimensions, missingValue)
                , Max(missingValue, sparseCoordinates.Values.Max())
                , SparseSquareMagnitude(sparseCoordinates, dimensions, missingValue)
                , optionalId)
        => InitSparsePoint(missingValue
            , sparseCoordinates.Keys.OrderBy(k => k).ToArray()
            , sparseCoordinates.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray());

        public SparsePoint(IList<int> dimensionIndices, IList<uint> sparseCoordinates, int dimensions, uint missingValue, int optionalId = -1) :
            base(ExpandSparseCoordinates(dimensionIndices, sparseCoordinates, dimensions, missingValue)
                , Max(missingValue, sparseCoordinates.Max())
                , SparseSquareMagnitude(sparseCoordinates, dimensions, missingValue)
                , optionalId)
        => InitSparsePoint(missingValue, dimensionIndices.ToArray(), sparseCoordinates.ToArray());
        

        protected SparsePoint(SparsePoint original): base(original) 
        => InitSparsePoint(original.MissingValue, original.DimensionIndices, original.SparseCoordinates);

        private void InitSparsePoint(uint missingValue, int[] dimensionIndices, uint[] sparseCoordinates)
        {
            MissingValue = missingValue;
            DimensionIndices = dimensionIndices;
            SparseCoordinates = sparseCoordinates;
            // Having used the expanded coordinates for hashcode and other purposes, we can discard them to save memory.
            // They will be reconstructed later when accessed as needed.
            Coordinates = null;
        }

        /// <summary>
        /// Clone this instance, but generate a new UniqueId.
        /// </summary>
        public override object Clone() {
            return new SparsePoint(this);
        }

        #endregion


        /// <summary>
        /// Access the coordinate values as signed integers.
        /// </summary>
        /// <param name="i">Index, which must be between zero and Dimension - 1, inclusive.</param>
        /// <returns>Coordinate value as an integer.</returns>
        public override int this[int i] {
            get {
                var sIndex = Array.BinarySearch<int>(DimensionIndices, i);
                if (sIndex < 0)
                    return (int)MissingValue;
                return (int)SparseCoordinates[sIndex];
            }
        }

        #region Distance 

        /// <summary>
        /// Compute the square of the distance to the origin when many values are missing.
        /// </summary>
        /// <param name="sparseCoordinates">Coordinates that have values. The key is the dimension index, the
        /// value is the coordinate value.</param>
        /// <param name="dimensions">Number of total dimensions, many of which may lack values.</param>
        /// <param name="missingValue">For every coordinate missing a value, assume this value.</param>
        /// <returns>Square of the distance to the origin.</returns>
        private static long SparseSquareMagnitude(Dictionary<int, uint> sparseCoordinates, int dimensions, uint missingValue)
        {
            var missingCount = dimensions - sparseCoordinates.Count;
            var mag = (long)missingValue * missingValue * missingCount;
            foreach (var x in sparseCoordinates.Values)
                mag += x * x;
            return mag;
        }

        private static long SparseSquareMagnitude(IList<uint> sparseCoordinates, int dimensions, uint missingValue)
        {
            var missingCount = dimensions - sparseCoordinates.Count;
            var mag = (long)missingValue * missingValue * missingCount;
            foreach (var x in sparseCoordinates)
                mag += x * x;
            return mag;
        }

        /// <summary>
        /// Square of the cartesian distance between two points.
        /// </summary>
        /// <param name="other">Second point for distance computation.</param>
        /// <returns>The square of the distance between the two points.</returns>
        public override long SquareDistance(UnsignedPoint other)
        {
            var distance = 0L;
            var otherSparsePoint = other as SparsePoint;
            if (otherSparsePoint == null)
            {
                // This point is sparse but the other is not.
                //
                //  Distance = |A|² + |B|²  - 2A·B
                //
                //  Assume that A is this sparse vector. If MissingValue = 0, then the vector dot product 
                //  A*B is zero for most coordinates. Thus we only need to correct for the few coordinates
                //  which are not zero.
                var dotProduct = 0L;
                distance = SquareMagnitude + other.SquareMagnitude;
                if (MissingValue == 0)
                {
                    for (var iSparse = 0; iSparse < SparseCoordinates.Length; iSparse++)
                        dotProduct += (long)SparseCoordinates[iSparse] * other[DimensionIndices[iSparse]];      
                }
                else
                {
                    var iSparse = 0;
                    var iDimSparse = DimensionIndices[iSparse];
                    var xSparse = (long)SparseCoordinates[iSparse];
                    for (var iDim = 0; iDim < Dimensions; iDim++)
                    {
                        long x;
                        if (iDim == iDimSparse)
                        {
                            x = xSparse;
                            iSparse++;
                            if (iSparse < DimensionIndices.Length)
                            {
                                iDimSparse = DimensionIndices[iSparse];
                                xSparse = SparseCoordinates[iSparse];
                            }
                        }
                        else
                            x =  MissingValue;
                        dotProduct += x * other.Coordinates[iDim];
                    }
                }
                distance -= 2 * dotProduct;
            }
            else
            {
                // Both points are sparse
                var i = 0;
                var j = 0;
                while (i < DimensionIndices.Length || j < otherSparsePoint.DimensionIndices.Length)
                {
                    long delta;
                    int indexDiff;
                    if (i >= DimensionIndices.Length)
                        indexDiff = 1;
                    else if (j >= otherSparsePoint.DimensionIndices.Length)
                        indexDiff = -1;
                    else
                        indexDiff = DimensionIndices[i] - otherSparsePoint.DimensionIndices[j];
                    if (indexDiff == 0)
                    {
                        var x = DimensionIndices.Length == 0 ? MissingValue : SparseCoordinates[i];
                        var y = otherSparsePoint.DimensionIndices.Length == 0 ? otherSparsePoint.MissingValue : otherSparsePoint.SparseCoordinates[j];
                        delta = (long)x - (long)y;
                        i++;
                        j++;
                    }
                    else if (indexDiff < 0)
                    {
                        var x = DimensionIndices.Length == 0 ? MissingValue : SparseCoordinates[i];
                        delta = (long)x - MissingValue;
                        i++;
                    }
                    else
                    {
                        var y = otherSparsePoint.DimensionIndices.Length == 0 ? otherSparsePoint.MissingValue : otherSparsePoint.SparseCoordinates[j];
                        delta = (long)y - MissingValue;
                        j++;
                    }
                    distance += delta * delta;
                }
            }
            return distance;
        }

        public override int SquareDistanceCompare(UnsignedPoint other, long squareDistance)
        {
            var otherSparsePoint = other as SparsePoint;
            if (other == null)
                return base.SquareDistanceCompare(other, squareDistance);
            var actualSqDistance = SquareDistance(other);
            return actualSqDistance.CompareTo(squareDistance);
        }
    }

    #endregion
}
