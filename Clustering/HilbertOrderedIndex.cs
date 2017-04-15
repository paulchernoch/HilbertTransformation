using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Clustering.HilbertIndex;

namespace Clustering
{
    /// <summary>
    /// A compact form of the HilbertIndex that does not use HilbertPoints, just UnsignedPoints.
    /// Each permutation of a Hilbert curve yields a HilbertIndex with different HilbertPoints (to represent the permutation),
    /// whereas every permutation of a Compact index holds just the same original points, hence uses much less memory.
    /// 
    /// If you create many HilbertOrderedIndex objects, the incremental memory requirement is proportional to N but not D.
    /// 
    /// This class permits most of the same operations supported by a HilbertIndex.
    /// </summary>
    public class HilbertOrderedIndex
    {

        /// <summary>
        /// Points sorted according to the Hilbert curve index.
        /// </summary>
        public List<UnsignedPoint> SortedPoints { get; set; }

        /// <summary>
        /// Points sorted the way the caller supplied them.
        /// </summary>
        public List<UnsignedPoint> UnsortedPoints { get; set; }

        /// <summary>
        /// Maps a point to its zero-based positions both before and after being sorted in Hilbert curve order.
        /// </summary>
        Dictionary<UnsignedPoint, Position> Index { get; set; }

        Dictionary<int, UnsignedPoint> IdsToPoints { get; set; }


        /// <summary>
        /// Count of points being indexed.
        /// </summary>
        public int Count { get { return UnsortedPoints.Count; } }

        /// <summary>
        /// Get the zero-based position of the point in SortedPoints.
        /// </summary>
        /// <param name="p">Point to lookup.</param>
        /// <returns>A zero-based position into the SortedPoints list.</returns>
        public int SortedPosition(UnsignedPoint p)
        {
            return Index[p].Sorted;
        }

        /// <summary>
        /// Get the zero-based position of the point in UnsortedPoints.
        /// </summary>
        /// <param name="p">Point to lookup.</param>
        /// <returns>A zero-based position into the UnsortedPoints list.</returns>
        public int UnsortedPosition(HilbertPoint p)
        {
            return Index[p].Original;
        }

        /// <summary>
        /// Lookup a point by its id.
        /// </summary>
        /// <returns>The point whose id matches the given id, or null.</returns>
        /// <param name="id">UniqueId of a point.</param>
        public UnsignedPoint FindById(int id)
        {
            UnsignedPoint p = null;
            IdsToPoints.TryGetValue(id, out p);
            return p;
        }

        public UnsignedPoint Equivalent(UnsignedPoint p)
        {
            return p;
        }

        /// <summary>
        /// Number of dimensions in each point.
        /// </summary>
        public int Dimensions { get { return UnsortedPoints[0].Dimensions; } }

        private void InitIndexing()
        {
            Index = new Dictionary<UnsignedPoint, Position>();
            foreach (var pointWithIndex in UnsortedPoints.Select((p, i) => new { Point = p, OriginalPosition = i }))
            {
                Index[pointWithIndex.Point] = new Position(pointWithIndex.OriginalPosition, -1);
            }
            foreach (var pointWithIndex in SortedPoints.Select((p, i) => new { Point = p, SortedPosition = i }))
            {
                Index[pointWithIndex.Point].Sorted = pointWithIndex.SortedPosition;
            }
            IdsToPoints = new Dictionary<int, UnsignedPoint>();
            foreach (var p in UnsortedPoints)
                IdsToPoints[p.UniqueId] = p;
        }

        #region Constructors


        /// <summary>
        /// Create a Compact from a HilbertIndex.
        /// </summary>
        /// <param name="index">Index to compact.</param>
        /// <param name="idToPoints">The key is the UniqueId and the value is the corresponding point.
        /// These points must be the UnsignedPoint analogs of the HilbertPoints in the HilbertIndex and share the same Ids.</param>
        public HilbertOrderedIndex(HilbertIndex index, Dictionary<int, UnsignedPoint> idToPoints)
        {
            UnsortedPoints = index.UnsortedPoints.Select(hp => idToPoints[hp.UniqueId]).ToList();
            SortedPoints = index.SortedPoints.Select(hp => idToPoints[hp.UniqueId]).ToList();
            InitIndexing();
        }

        #endregion

        /// <summary>
        /// Compose an enumerable that encompasses a range of points starting at the given point and running for the given length.
        /// If the point is too close to the end of the list in sorted order, fewer items than rangeLength may be returned.
        /// </summary>
        /// <param name="p">Point where range starts.</param>
        /// <param name="rangeLength">Range length.</param>
        public IEnumerable<UnsignedPoint> Range(UnsignedPoint p, int rangeLength)
        {
            var position = SortedPosition(p);
            var rangeStart = Math.Min(Math.Max(0, position - rangeLength / 2), Count - rangeLength);
            return SortedPoints.Skip(rangeStart).Take(rangeLength);
        }

        /// <summary>
        /// Find the points adjacent to the given point in the Hilbert ordering, then sort them by the cartesian distance, from nearest to farthest.
        /// </summary>
        /// <param name="point">Reference point to seek in the index.</param>
        /// <param name="rangeLength">Number of points to retrieve from the index. Half of these points will precede and half succeed the given point
        /// in the index, unless we are near the beginning or end of the index, in which case the range will be shifted.</param>
        /// <param name="includePointItself">If false, the reference point will not be present in the results.
        /// If true, the point will be present in the results.</param>
        /// <returns>The points nearest to the reference point in both Hilbert and Cartesian ordering, sorted from nearest to farthest.</returns>
        public IEnumerable<UnsignedPoint> NearestFromRange(UnsignedPoint point, int rangeLength, bool includePointItself = false)
        {
            rangeLength = includePointItself ? rangeLength : rangeLength + 1;
            var middlePosition = SortedPosition(point);
            var rangeStart = Math.Max(0, middlePosition - rangeLength / 2);
            return SortedPoints
                .Skip(rangeStart)
                .Take(rangeLength)
                .Where(p => includePointItself || !p.Equals(point))
                .OrderBy(p => p.Measure(point));
        }

        /// <summary>
        /// Find the K-nearest neighbors of a given point according to the cartesian distance between the point and its neighbors.
        /// 
        /// NOTE: This compares the point to all other points, hence is more costly than NearestFromRange but is guaranteed
        /// to find all near neighbors.
        /// </summary>
        /// <param name="point">Reference point whose neighbors are sought.</param>
        /// <param name="k">Number of nearest neighbors to find.</param>
        /// <param name="includePointItself">If false, the point is not considered its own neighbor and will not be present in the results.
        /// If true, the point is considered its own neighbor and will be present in the results,
        /// unless all the nearest neighbors are zero distance from this point, in which case it might not make the cut.</param>
        /// <returns>The nearest neighbors of the given point, sorted from nearest to farthest.</returns>
        public IEnumerable<UnsignedPoint> Nearest(UnsignedPoint point, int k, bool includePointItself = false)
        {
            return SortedPoints
                .Where(p => includePointItself || !p.Equals(point))
                .BottomN<UnsignedPoint, long>(point, k);
        }

        /// <summary>
        /// Find how accurate NearestFromRange is when searching for the neighbors of a single given reference point.
        /// This finds the true K-nearest neighbors of the reference point (using Nearest) 
        /// and the approximate K-nearest neighbors using the Hilbert index,
        /// then compare how accurate the Hilbert index was.
        /// </summary>
        /// <param name="point">Reference point whose neighbors are sought.</param>
        /// <param name="k">Number of nearest neighbors sought.</param>
        /// <param name="rangeLength">Number of points in the Hilbert index to sample.</param>
        /// <returns>A value from zero to 1.0, where 1.0 means perfectly accurate.</returns>
        public double Accuracy(UnsignedPoint point, int k, int rangeLength)
        {
            var allNeighbors = new HashSet<UnsignedPoint>();
            allNeighbors.UnionWith(Nearest(point, k));
            var matches = NearestFromRange(point, rangeLength).Count(allNeighbors.Contains);
            return matches / (double)k;
        }

        /// <summary>
        /// Unioning the results of several different indices, find the composite accuracy of using them all 
        /// in combination to find the nearest neighbors.
        /// </summary>
        /// <param name="indices">Indices.</param>
        /// <param name="point">Point whos enearest neighbors are sought.</param>
        /// <param name="k">Number of nearest neighbors who are sought.</param>
        /// <param name="rangeLength">Number of points to draw from each index.</param>
        /// <returns>A value from zero to 1.0, where 1.0 means perfectly accurate.</returns>
        public static double CompositeAccuracy(IList<HilbertOrderedIndex> indices, UnsignedPoint point, int k, int rangeLength)
        {
            // Note the tricky use of Equivalent. The points from different indices should not be directly compared,
            // so we need to map a point from the first index to the equivalent point in another, then map back
            // for the final tally.
            var allNeighbors = new HashSet<UnsignedPoint>();
            var firstIndex = indices[0];
            allNeighbors.UnionWith(firstIndex.Nearest(firstIndex.Equivalent(point), k));
            var fromRange = new HashSet<UnsignedPoint>();
            fromRange.UnionWith(
                indices.SelectMany(i => i.NearestFromRange(i.Equivalent(point), rangeLength)
                                   .Where(p => allNeighbors.Contains(firstIndex.Equivalent(p))))
            );
            return fromRange.Count() / (double)k;
        }

        /// <summary>
        /// Find how accurate NearestFromRange is on average when searching for the neighbors of every point (or a large sample of points).
        /// This finds the true K-nearest neighbors of each reference point (using Nearest) 
        /// and the approximate K-nearest neighbors using the Hilbert index,
        /// then computes how accurate the Hilbert index was.
        /// </summary>
        /// <param name="k">Number of nearest neighbors sought.</param>
        /// <param name="rangeLength">Number of points in the Hilbert index to sample.
        /// The higher the number, the poorer the performance but higher the accuracy.
        /// </param>
        /// <param name="sampleSize">If zero, then analyze all points.
        /// Otherwise, base the accuracy on a sample of the points.</param>
        /// <returns>A value from zero to 1.0, where 1.0 means perfectly accurate.</returns>
        public double Accuracy(int k, int rangeLength, int sampleSize = 0)
        {
            if (sampleSize == 0)
                sampleSize = Count;
            var avg = UnsortedPoints.Take(sampleSize).Select(p => Accuracy(p, k, rangeLength)).Average();
            return avg;
        }

        public static double CompositeAccuracy(IList<HilbertOrderedIndex> indices, int k, int rangeLength, int sampleSize = 0)
        {
            var firstIndex = indices[0];
            if (sampleSize == 0)
                sampleSize = firstIndex.Count;
            var avg = firstIndex.UnsortedPoints.Take(sampleSize).Select(p => CompositeAccuracy(indices, p, k, rangeLength)).Average();
            return avg;
        }

    }
}
