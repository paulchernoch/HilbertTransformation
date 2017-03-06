using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Using a HilbertIndex, classify neighboring points into groups according to the Cartesian distance between them.
	/// 
	/// This is a bottom-up, agglomerative clustering algorithm.
	/// </summary>
	/// <remarks>
	/// The algorithm:
	/// 
	///   1) Receive N D-dimensional points (UniqueIntegerPoint) with non-negative, integral coordinates.
	///   2) Initially classify each point in its own cluster (Classification).
	///   3) Create multiple alternate Hilbert curves (HilbertIndex).
	///      Each is derived by a different permutation of the coordinates of the points, yielding corresponding HilbertPoints.
	///   4) Evaluate each HilbertIndex and find the one that predicts the lowest number of clusters K (OptimalIndex).
	///   5) This analysis also yields a characteristic distance S. All points closer to one another than this distance
	///      shall be regarded as potentially belonging to the same cluster.
	///   6) Pass over the points in Hilbert order. Every consescutive pair closer than the distance S is merged into the
	///      same cluster.
	///   7) Find the distance from the Centroid of each non-outlier cluster to every other large cluster (ClosestCluster).
	///   8) For the closest neighboring large clusters, probe deeper and find the pair of points, one drawn from each of two clusters,
	///      that is closest and their separation s. This is called the poly-chromatic, closest point problem (ClosestCluster).
	///   9) If a pair of clusters is closer than S (s ≤ S), merge them, transitively (HilbertClassifier). 
	///      Thus if point A is near B and B is near C, then A, B, and C will end up in the same cluster.
	///  10) For all the remaining outliers (small clusters), merge them with the nearest large cluster unless their distance
	///      is too great (HilbertClassifier). (Deciding what "too far apart" means is tricky.)
	///      Do not permit this phase to cause two large clusters to be joined to each other.
	/// 
	/// This algorithm does not guarantee that:
	/// 
	///   A) Two points with distance s ≤ S will end up in the same cluster. 
	///      (An exhaustive search would guarantee this - very expensive.) 
	/// 
	///   B) Two clusters that should be separate are kept separate. 
	///      (Noise, dramatic variation in point density among different clusters, irregular cluster shape, etc.)
	/// 
	///   C) Outliers that should not be part of a large cluster at all will be kept separate.
	/// 
	/// To deal with such problems, additional algorithms must be applied:
	/// 
	///   1) Remove Redundant dimensions.
	///      If a dimension is a linear sum or product of one or more other dimensions, it adds no information
	///      and should be dropped.
	/// 
	///   2) Scale dimensions.
	///      Adjust the metric by scaling some dimensions to increase their effect.
	///      Some dimensions are more important for clustering and should have a linear multiplier applied before clustering.
	/// 
	///   3) Characteristic Distance. 
	///      Better inference of the proper characteristic distance S by using a filter to tease signal out of noise.
	///
	///   4) Random Subsets. Perform clustering against a series of random subset of the data, then use statistics to
	///      keep apart points that too often end up in separate clusters. This may break apart "bridges"
	///      between separate clusters.
	///
	///   5) Divide Clusters. 
	///      Follow-up using a bisecting, divisive clustering algorithm that identifies clusters to break apart
	///      and splits them. Examples:  
	///         - Bisecting K-means
	///         - Principal Direction Divisive Partitioning (PDDP) 
	///      See http://www-users.cs.umn.edu/~boley/publications/papers/SIAM_Choose.pdf
	/// 
	/// 
	/// </remarks>
	public class HilbertClassifier
	{
		/// <summary>
		/// Use this to configure the OptimalIndex call.
		/// 
		/// Note that the index search can have an OutlierSize different from the main clustering algorithm.
		/// </summary>
		public class IndexBudget: IEquatable<IndexBudget>
		{
			public int IndexCount { get; set; } = 1;
			public int OutlierSize { get; set; } = 5;
			public int NoiseSkipBy { get; set; } = 10;
			public int MaxTrials { get; set; } = 1000;
			public int MaxIterationsWithoutImprovement { get; set; } = 3;
			public bool UseSample { get; set; } = false;

			public override bool Equals(object obj)
			{
				return Equals(obj as IndexBudget);
			}

			public bool Equals(IndexBudget other)
			{
				if (other == null)
					return false;
				return IndexCount == other.IndexCount
					  && OutlierSize == other.OutlierSize
					  && NoiseSkipBy == other.NoiseSkipBy
					  && MaxTrials == other.MaxTrials
					  && MaxIterationsWithoutImprovement == other.MaxIterationsWithoutImprovement
					  && UseSample == other.UseSample
				;
			}
		}

		/// <summary>
		/// Used to configure the call to OptimalIndex.Search or SearchMany.
		/// </summary>
		/// <value>The index config.</value>
		public IndexBudget IndexConfig { get; set; } = new IndexBudget();

		/// <summary>
		/// Organizes the points into clusters.
		/// </summary>
		public Classification<UnsignedPoint, string> Clusters { get; set; }

		private Dictionary<int, UnsignedPoint> IdsToPoints { get; set; }

		/// <summary>
		/// Transform the points in Clusters into HilbertPoints (if necessary).
		/// </summary>
		private List<HilbertPoint> HilbertPoints
		{
			get
			{
				return Clusters
					.Points()
					.Select(p => HilbertPoint.CastOrConvert(p, BitsPerDimension, true))
					.ToList();
			}
		}

		/// <summary>
		/// If a cluster has this many points or fewer, it is considered an outlier.
		/// </summary>
		public int OutlierSize { get; set; } = 5;

		/// <summary>
		/// When reducing the first cut Clusters (from the HilbertIndex) to a smaller set, 
		/// this sets a limit on how many nearest neighboring clusters will be compared to each cluster.
		/// A low number increases speed and decreases accuracy.
		/// </summary>
		public int MaxNeighborsToCompare { get; set; } = 5;

		public int BitsPerDimension { get; set; }

		/// <summary>
		/// Points separated by this characteristic square distance or less should be merged into one cluster.
		/// If not a positive number, this is derived (the preferred method, which adapts to the data) and set automatically.
		/// </summary>
		public long MergeSquareDistance { get; set; } = 0;

		/// <summary>
		/// If true, the exact distance between clusters is computed, otherwise a faster approximation is used.
		/// </summary>
		public bool UseExactClusterDistance { get; set; } = false;

		private HilbertIndex BestIndex { get; set; }

		/// <summary>
		/// This is multiplied by MergeSquareDistance to derive the maximum square distance that an outlier may be
		/// from a neighboring cluster and still be permitted to merge.
		/// </summary>
		/// <value>The outlier distance multiplier.</value>
		public double OutlierDistanceMultiplier { get; set; } = 5;

		public HilbertClassifier(IEnumerable<UnsignedPoint> points, int bitsPerDimension)
		{
			var labelCounter = 1;
			// Steps 1 and 2 (Receive points and classify all points in separate clusters of size one).
			Clusters = new Classification<UnsignedPoint, string>(points, p => (labelCounter++).ToString());
			BitsPerDimension = bitsPerDimension;

			IdsToPoints = new Dictionary<int, UnsignedPoint>();
			foreach (var p in Clusters.Points())
				IdsToPoints[p.UniqueId] = p;
		}

		/// <summary>
		/// Perform unassisted classification of points.
		/// </summary>
		public Classification<UnsignedPoint, string> Classify()
		{
			//   3) Create multiple HilbertIndexes.
			//   4) Find best HilbertIndex and find the one that predicts the lowest number of clusters K (OptimalIndex).
			//   5) Set the characteristic merge distance S (MergeSquareDistance).
			//TODO: Support formation and use of more than one HilbertIndex, to respect IndexBudget.IndexCount.
			var optimum = OptimalIndex.Search(
				HilbertPoints,
				IndexConfig.OutlierSize,
				IndexConfig.NoiseSkipBy,
				IndexConfig.MaxTrials,
				IndexConfig.MaxIterationsWithoutImprovement,
				IndexConfig.UseSample
			);
			BestIndex = optimum.Index;
			MergeSquareDistance = optimum.MergeSquareDistance;

			//   6) Pass over the points in Hilbert order. Every consescutive pair closer than the distance S is merged into the
			//      same cluster.
			MergeByHilbertIndex(BestIndex);

			//   7) Find the distance from the Centroid of each non-outlier cluster to every other large cluster (ClosestCluster).
			//   8) For the closest neighboring large clusters, probe deeper and find the pair of points, 
			//      one drawn from each of two clusters, that is closest and their separation s (square Cartesian distance). 
			//   9) If a pair of clusters is closer than S (s ≤ S), merge them, transitively. 
			var cc = new ClosestCluster<string>(Clusters);
			var closeClusterPairs = cc.FindClosestClusters(MaxNeighborsToCompare, MergeSquareDistance, OutlierSize, UseExactClusterDistance);
			var clusterMerges = 0;
			foreach (var pair in closeClusterPairs.Where(p => p.SquareDistance <= MergeSquareDistance))
			{
				pair.Relabel(Clusters);
				if (Clusters.Merge(pair.Color1, pair.Color2))
					clusterMerges++;
			}

			//  10) Merge outliers with neighboring clusters. 
			//      For all the remaining outliers (small clusters), merge them with the nearest large cluster 
			//      unless their distance is too great (MergeSquareDistance * OutlierDistanceMultiplier). 
			//      Do not permit this phase to cause two large clusters to be joined to each other.
			var maxOutlierMergeDistance = (long)(MergeSquareDistance * OutlierDistanceMultiplier);
			var outlierMerges = MergeOutliers(maxOutlierMergeDistance);

			Console.WriteLine($"   {clusterMerges} Cluster merges, {outlierMerges} Outlier merges");

			return Clusters;
		}

		/// <summary>
		/// Merge into one cluster all pairs of points that are adjacent to one another in Hilbert curve order
		/// if they are not too far apart.
		/// 
		/// If the ideal number of clusters is K, this first pass often reduces the points to 2K clusters or fewer,
		/// excluding the outliers.
		/// </summary>
		/// <param name="hIndex">HilbertIndex to use for the ordering of points.</param>
		private void MergeByHilbertIndex(HilbertIndex hIndex)
		{
			var unsortedPoints = Clusters.Points().ToArray();
			var sorter = new KeySorter<HilbertPoint, UnsignedPoint>(hp => hp.UniqueId, p => p.UniqueId);
			var sortedPoints = sorter.Sort(unsortedPoints, hIndex.SortedPoints);
			UnsignedPoint prevPoint = null;
			UnsignedPoint lastMerged = null;
			// About "revisitations". If the Hilbert order leaves a cluster to visit an outlier, 
			// then returns to "revisit" the same cluster, the revisitation logic may sometimes capture this and perform a merge. 
			// This test is only performed when consecutive points could not be joined, hence is 
			// proportional to K, the number of clusters, not N, the number of points.
			var revisitations = 0;
			foreach (var currPoint in sortedPoints)
			{
				if (prevPoint != null)
				{
					if (MergeIfNear(prevPoint, currPoint))
						lastMerged = currPoint;
					else if (lastMerged != null && MergeIfNear(lastMerged, currPoint)) {
						lastMerged = currPoint;
						revisitations++;
					}
				}
				prevPoint = currPoint;
			}
			Console.WriteLine($"{revisitations} Revisitation{revisitations != 1 ? 's' : ' ' } in MergeByHilbertIndex");
		}

		/// <summary>
		/// Merge the clusters containing two points if the distance separating them does not exceed MergeSquareDistance.
		/// The points given here may be HilbertPoints frmo a HilbertIndex or UnsignedPoints already present in the Classification.
		/// In case of the former, a lookup is performed based on the id to find the proper UnsignedPoint corresponding to the HilbertPoint.
		/// </summary>
		/// <returns><c>true</c>, if a new merge performed, <c>false</c> if too far to merge or already merged.</returns>
		/// <param name="p1">First point to compare.</param>
		/// <param name="p2">Second point.</param>
		/// <param name="maxSquareDistance">If a positive value, use this as the maximum distance permitted between points.
		/// Otherwise, use MergeSquareDistance.</param>
		private bool MergeIfNear(UnsignedPoint p1, UnsignedPoint p2, long maxSquareDistance = 0)
		{
			var p1InClusters = IdsToPoints[p1.UniqueId];
			var p2InClusters = IdsToPoints[p2.UniqueId];
			maxSquareDistance = (maxSquareDistance <= 0) ? MergeSquareDistance : maxSquareDistance;
			if (p1InClusters.SquareDistanceCompare(p2InClusters, maxSquareDistance) <= 0)
			{
				var c1 = Clusters.GetClassLabel(p1InClusters);
				var c2 = Clusters.GetClassLabel(p2InClusters);
				return Clusters.Merge(c1, c2);
			}
			else
				return false;
		}

		/// <summary>
		/// Merges the small outlier clusters with nearby larger clusters.
		/// </summary>
		/// <returns>The number of outlier clusters merged.</returns>
		/// <param name="maxOutlierMergeDistance">An outlier will only be merged if its distance from 
		/// its nearest cluster does not exceed this square distance.</param>
		public int MergeOutliers(long maxOutlierMergeDistance)
		{
			var mergesDone = 0;
			var cc = new ClosestCluster<string>(Clusters);
			var closeOutlierPairs = cc.FindClosestOutliers(
				MaxNeighborsToCompare,
				maxOutlierMergeDistance,
				OutlierSize
			);
			foreach (var pair in closeOutlierPairs)
			{
				pair.Relabel(Clusters);
				// We do not want an outlier to cause the merger of two large clusters
				// if each of the large clusters is near the outlier but not near each other.
				// Thus, once the outlier is merged with the nearer of its neighbors,
				// it will be ruled out from firther merges.
				if (pair.CountOutliers(Clusters, OutlierSize) != 1)
					continue;
				if (Clusters.Merge(pair.Color1, pair.Color2))
					mergesDone++;
			}
			return mergesDone;
		}
	}
}
