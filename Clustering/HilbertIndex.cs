using System;
using System.Collections.Generic;
using System.Linq;

using HilbertTransformation;
using HilbertTransformation.Random;

namespace Clustering
{
	/// <summary>
	/// Provides bi-directional mapping between a HilbertPoint and an IntegerPoint, and 
	/// sorts the points in Hilbert order.
	/// </summary>
    public class HilbertIndex
    {
		/// <summary>
		/// Identifies in which zero-based position each point can be found prior to being indexed (Original)
		/// and after it is indexed (Sorted).
		/// </summary>
		public class Position
		{
			/// <summary>
			/// Zero-based position the point occupied before the index was constructed.
			/// </summary>
			public int Original { get; set; }

			/// <summary>
			/// Zero-based position the point occupies after the index is constructed, once sorted in Hilbert curve order.
			/// </summary>
			public int Sorted { get; set; }

			public Position(int original, int sorted)
			{
				Original = original;
				Sorted = sorted;
			}
		}

		/// <summary>
		/// Points sorted according to the Hilbert curve index.
		/// </summary>
		public List<HilbertPoint> SortedPoints { get; set; }

		/// <summary>
		/// Points sorted the way the caller supplied them.
		/// </summary>
		public List<HilbertPoint> UnsortedPoints { get; set; }

		/// <summary>
		/// Maps a point to its zero-based positions both before and after being sorted in Hilbert curve order.
		/// </summary>
        Dictionary<HilbertPoint, Position> Index { get; set; }

		Dictionary<int, HilbertPoint> IdsToPoints { get; set; }


		/// <summary>
		/// Count of points being indexed.
		/// </summary>
		public int Count { get { return UnsortedPoints.Count; } }

        /// <summary>
        /// Get the zero-based position of the point in SortedPoints.
        /// </summary>
        /// <param name="p">Point to lookup.</param>
        /// <returns>A zero-based position into the SortedPoints list.</returns>
        public int SortedPosition(HilbertPoint p)
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
		/// <param name="id">UniqueId of a HilbertPoint.</param>
		public HilbertPoint FindById(int id)
		{
			HilbertPoint p = null;
			IdsToPoints.TryGetValue(id, out p);
			return p;
		}

		/// <summary>
		/// If two indices were composed from the same points but with their coordinates differently permuted, 
		/// the corresponding points retain the same UniqueId (which isn't so unique after all). 
		/// This will look up the corresponding point in this index of a point frmo another index.
		/// </summary>
		/// <param name="p">P.</param>
		public HilbertPoint Equivalent(HilbertPoint p)
		{
			return FindById(p.UniqueId);
		}

        /// <summary>
        /// Number of dimensions in each point.
        /// </summary>
        public int Dimensions { get { return UnsortedPoints[0].Dimensions; } }

        /// <summary>
        /// Number of bits used to encode each dimension of each point.
        /// </summary>
        public int BitsPerDimension { get { return UnsortedPoints[0].BitsPerDimension; } }

		private void InitIndexing()
		{
			Index = new Dictionary<HilbertPoint, Position>();
			foreach (var pointWithIndex in UnsortedPoints.Select((p, i) => new { Point = p, OriginalPosition = i }))
			{
				Index[pointWithIndex.Point] = new Position(pointWithIndex.OriginalPosition, -1);
			}
			SortedPoints = UnsortedPoints.ToList();
			SortedPoints.Sort();
			foreach (var pointWithIndex in SortedPoints.Select((p, i) => new { Point = p, SortedPosition = i }))
			{
				Index[pointWithIndex.Point].Sorted = pointWithIndex.SortedPosition;
			}
			IdsToPoints = new Dictionary<int, HilbertPoint>();
			foreach (var p in UnsortedPoints)
				IdsToPoints[p.UniqueId] = p;
		}

		private static int FindBitsPerDimension(IEnumerable<UnsignedPoint> points)
		{
			return HilbertPoint.FindBitsPerDimension((int)points.Select(p => p.MaxCoordinate).Max());
		}

		#region Constructors

		/// <summary>
		/// Create an index of all the points in a Classification, optionally adding a new dimension to each point to hold 
		/// that point's classification index.
		/// </summary>
		/// <param name="clusters">Clusters of points, which could be UnsignedPoints or HilbertPoints.</param>
		/// <param name="bitsPerDimension">Bits per dimension to use when transforming UnsignedPoints into HilbertPoints,
		/// should that be necessary. 
		/// If a non-positive number, compute the value by studying the data, using the smallest number capable of accommodating
		/// the largest coordinate values.</param>
		/// <param name="addClassificationDimension">If set to <c>true</c> add a classification dimension to the end of each point.
		/// The value will be the index of that point's cluster. Cluster ordering is arbitrary and dependent on the order that
		/// the set Classification.LabelToPoints.Values iterates over them.</param>
        public HilbertIndex(Classification<UnsignedPoint, string> clusters, int bitsPerDimension = 0, bool addClassificationDimension = false)
        {
			if (bitsPerDimension <= 0)
				bitsPerDimension = FindBitsPerDimension(clusters.Points());

			UnsortedPoints = new List<HilbertPoint>();
			foreach (var clusterWithNumber in clusters.LabelToPoints.Values.Select((c,i) => new { Cluster = c, Index = (uint)i }))
			{
				UnsortedPoints.AddRange(
					clusterWithNumber.Cluster
					                 .Select(p => addClassificationDimension ? p.AppendCoordinate(clusterWithNumber.Index) : p)
									 .Select(p => HilbertPoint.CastOrConvert(p, bitsPerDimension, true))
				);
			}
			InitIndexing();
        }

		public HilbertIndex(IEnumerable<HilbertPoint> points)
		{
			UnsortedPoints = points.ToList();
			InitIndexing();
		}

		/// <summary>
		/// Create a new index based on an existing one, having all the same points, but with their coordinates permuted.
		/// 
		/// All the points in the new index will share the same UniqueIds as their corresponding points in the original.
		/// To map from a point in tone index to the similar point in the other:
		///   
		///   var p2 = hilbertIndex2.Equivalent(p1);
		/// </summary>
		/// <param name="original">Original.</param>
		/// <param name="permutation">Permutation.</param>
		public HilbertIndex(HilbertIndex original, Permutation<uint> permutation)
		{
			UnsortedPoints = original.UnsortedPoints.Select(p => p.Permute(permutation)).ToList();
			InitIndexing();
		}

		public HilbertIndex(IList<HilbertPoint> points, Permutation<uint> permutation)
		{
			UnsortedPoints = points.Select(p => p.Permute(permutation)).ToList();
			InitIndexing();
		}

		#endregion

		/// <summary>
		/// Compose an enumerable that encompasses a range of points starting at the given point and running for the given length.
		/// If the point is too close to the end of the list in sorted order, fewer items than rangeLength may be returned.
		/// </summary>
		/// <param name="p">Point where range starts.</param>
		/// <param name="rangeLength">Range length.</param>
        public IEnumerable<HilbertPoint> Range(HilbertPoint p, int rangeLength)
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
        public IEnumerable<HilbertPoint> NearestFromRange(HilbertPoint point, int rangeLength, bool includePointItself = false)
        {
            rangeLength = includePointItself ? rangeLength : rangeLength + 1;
			var middlePosition = SortedPosition(point);
			var rangeStart = Math.Max(0,middlePosition - rangeLength / 2);
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
        public IEnumerable<HilbertPoint> Nearest(HilbertPoint point, int k, bool includePointItself = false)
        {
            return SortedPoints
				.Where(p => includePointItself || !p.Equals(point))
				.BottomN<HilbertPoint,long>(point,k);
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
        public double Accuracy(HilbertPoint point, int k, int rangeLength)
        {
            var allNeighbors = new HashSet<HilbertPoint>();
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
        public static double CompositeAccuracy(IList<HilbertIndex> indices, HilbertPoint point, int k, int rangeLength)
        {
			// Note the tricky use of Equivalent. The points from different indices should not be directly compared,
			// so we need to map a point from the first index to the equivalent point in another, then map back
			// for the final tally.
            var allNeighbors = new HashSet<HilbertPoint>();
			var firstIndex = indices[0];
			allNeighbors.UnionWith(firstIndex.Nearest(firstIndex.Equivalent(point), k));
            var fromRange = new HashSet<HilbertPoint>();
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

        public static double CompositeAccuracy(IList<HilbertIndex> indices, int k, int rangeLength, int sampleSize = 0)
        {
            var firstIndex = indices[0];
            if (sampleSize == 0)
                sampleSize = firstIndex.Count;
            var avg = firstIndex.UnsortedPoints.Take(sampleSize).Select(p => CompositeAccuracy(indices, p, k, rangeLength)).Average();
            return avg;
        }

		/// <summary>
		/// Set the number of points to triangulate on when comparing distances.
		/// </summary>
		/// <param name="numTriangulationPoints">Number of triangulation points.</param>
		public HilbertIndex SetTriangulation(int numTriangulationPoints)
		{
			foreach (var point in UnsortedPoints)
				point.NumTriangulationPoints = numTriangulationPoints;
			return this;
		}

        public HilbertOrderedIndex Compress(Dictionary<int, UnsignedPoint> idsToPoints)
        {
            return new HilbertOrderedIndex(this, idsToPoints);
        }

    }
}
