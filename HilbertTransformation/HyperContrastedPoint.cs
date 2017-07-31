using HilbertTransformation.Cache;
using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Math;

namespace HilbertTransformation
{
    public class HyperContrastedPoint : UnsignedPoint
    {

        #region Cache

        /// <summary>
        /// Cache that holds the coordinates for HyperContrastedPoints.
        /// 
        /// Callers should Resize the cache to suit their needs.
        /// </summary>
        public static PseudoLRUCache<uint[]> Cache { get; private set; } = new PseudoLRUCache<uint[]>(10000);

        #endregion

        private PseudoLRUCache<uint[]>.CacheItem CoordinateHolder { get; set; }

        public override uint[] Coordinates
        {
            get { return CoordinateHolder.GetOrCreate(() => LazyCoordinates().ToArray()); }
            protected set { CoordinateHolder.Item = value; }
        }

        /// <summary>
        /// Each index in this array corresponds to a value in _coordinates.
        /// Together they specify the sparse values in the point.
        /// The coordinate at any index not in this array is assumed to equal MissingValue.
        /// </summary>
        private int[] DimensionIndices { get; set; }

        /// <summary>
        /// Coordinate values that correspond to DimensionIndices.
        /// </summary>
        public uint[] SparseCoordinates { get; set; }

        /// <summary>
        /// Every missing coordinate (i.e. one not present in DimensionIndices) is assumed to be a value drawn uniformly randomly from this array.
        /// Once chosen, that value never changes for the given coordinate.
        /// </summary>
        public uint[] MissingValues { get; private set; }

        #region Random numbers

        /// <summary>
        /// Used to generate random values for missing coordinates.
        /// </summary>
        private UniqueSeedRandom RandomNumbers { get; set; }

        #endregion

        #region Coordinate Iteration

        public override IEnumerable<uint> LazyCoordinates()
        {
            return CoordinateSequence(DimensionIndices, SparseCoordinates, Dimensions, MissingValues, RandomNumbers);
        }

        private static IEnumerable<uint> CoordinateSequence(
              int[] dimensionIndices
            , uint[] sparseCoordinates
            , int dimensions
            , uint[] missingValues
            , UniqueSeedRandom randomNumbers)
        {
            var iSparse = 0;
            var iDim = 0;
            var r = randomNumbers.Sequence(missingValues.Length);
            foreach (var iMissing in r)
            {
                while (iSparse < dimensionIndices.Length && iDim == dimensionIndices[iSparse])
                {
                    yield return sparseCoordinates[iSparse];
                    iSparse++;
                    iDim++;
                }
                if (iDim < dimensions)
                {
                    yield return missingValues[iMissing];
                    iDim++;
                }
                if (iDim >= dimensions)
                    break;
            }
        }

        #endregion

        #region Constructors and initializers



        public HyperContrastedPoint(IEnumerable<int> dimensionIndices, IList<uint> sparseCoordinates, int dimensions, uint[] missingValues, int optionalId = -1) :
        this(new UniqueSeedRandom(), dimensionIndices.ToArray(), sparseCoordinates.ToArray(), dimensions, missingValues, optionalId)
        {
        }

        private HyperContrastedPoint(UniqueSeedRandom randomNumbers, int[] dimensionIndices, uint[] sparseCoordinates, int dimensions, uint[] missingValues, int optionalId) :
            base(CoordinateSequence(dimensionIndices, sparseCoordinates, dimensions, missingValues, randomNumbers).ToArray(), optionalId)
        {
            RandomNumbers = randomNumbers;
            DimensionIndices = dimensionIndices;
            SparseCoordinates = sparseCoordinates;
            MissingValues = missingValues;
            CoordinateHolder = Cache.Add(_coordinates);
            _coordinates = null;
        }



        #endregion

    }
}
