using System;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Cluster points according to their local density.
	/// 
	/// This is a bottom-up, density-based, agglomerative clustering algorithm.
	/// 
	/// This algorithm is designed to be used to divide a cluster formed by single-link agglomeration
	/// into smaller pieces IF there are subclusters with distinct density centers.
	/// 
	/// If this algorithm finds a single density center and all the points and small outliers adhere to it,
	/// it will yield a single cluster. But if two or more density centers are found and they cause 
	/// points to clump together around them, then multiple clusters will result.
	/// 
	/// All merges are performed in density order, with the points from the densest regions merged first.
	/// </summary>
	public class DensityClassifier
	{

		public Classification<UnsignedPoint, string> Clusters { get; private set; }

		public int WindowSize { get; set; } = 10;

		/// <summary>
		/// Two clusters of this size or greater will not be permitted to be merged.
		/// </summary>
		/// <value>The size of the unmergeable.</value>
		public int UnmergeableSize { get; set; }

		/// <summary>
		/// Points separated by this characteristic square distance or less may be merged into one cluster,
		/// unless that would cause two large clusters to be merged.
		/// 
		/// It is likely this was derived using a ClusterCounter.
		/// </summary>
		public long MergeSquareDistance { get; set; }

		/// <summary>
		/// All clusters smaller than this size are outliers and may be merged to the nearest cluster of a larger size.
		/// </summary>
		/// <value>The size of the outlier.</value>
		public int OutlierSize { get; set; } = 5;

		/// <summary>
		/// Determines how strongly the density rank of a point's neighbors affects its density rank.
		/// If zero, there is no effect.
		/// If one, the point's rank is averaged with the lowest rank in its neighborhood.
		/// If greater than one, the rank is skewed more and more towards the rank of its neighbors.
		/// 
		/// By boosting the rank of a point based on its neighbors, clusters are induced to form around
		/// seeds, where the highest density point in a neighborhood is the seed.
		/// </summary>
		public double NeighborhoodRankWeight { get; set; } = 3;

		public HilbertIndex Index { get; set; }

		/// <summary>
		/// The number of near neighboring points that must occupy the enclosing hypersphere
		/// used to determine density.
		/// </summary>
		public int NeighborCount { get; private set; } = 10;

		public DensityClassifier(HilbertIndex index, long mergeSquareDistance, int unmergeableSize)
		{
			Index = index;
			MergeSquareDistance = mergeSquareDistance;
			UnmergeableSize = unmergeableSize;
			int labelCounter = 1;
			Clusters = new Classification<UnsignedPoint, string>(Index.SortedPoints, p => (labelCounter++).ToString());
		}

		public Classification<UnsignedPoint, string> Classify()
		{
			//TODO: Implement density based clustering algorithm.

			// 1. Get the Neighborhood density for all points and rank them by that density, from densest to most diffuse.
			var hd = new HilbertDensity(Index, NeighborCount) { NeighborhoodRankWeight = NeighborhoodRankWeight };
			var ranks = hd.NeighborhoodRank(WindowSize);

			// 2. Sort all pairs of points that are adjacent in the Hilbert ordering by the lesser of the neighborhood 
			//    density rank for the two points, from high density (low rank) to low density (high rank).
			// 3. Evaluate the distances between pairs of points.
			//    If their distance is less than MergeSquareDistance, they may be merged unless
			//    that would cause two clusters each larger than UnmergeableSize to be merged.
			var points = Index.SortedPoints;
			var orderedMerges = Enumerable
				.Range(0, points.Count() - 1)
				.Select(i => new
				{
					Point1 = points[i],
					Point2 = points[i + 1],
					DensityRank = Math.Min(ranks[i], ranks[i + 1])
				})
				.Where(ppd => ppd.Point1.SquareDistanceCompare(ppd.Point2, MergeSquareDistance) <= 0)
				.OrderBy(ppd => ppd.DensityRank);

			// 4. Merge clusters that pass the test.
			foreach (var orderedMerge in orderedMerges)
				Merge(orderedMerge.Point1, orderedMerge.Point2);

			// 5. Merge all outliers to the nearest large cluster. 
			//    In the end, we will only have large clusters remaining.
			MergeOutliers();

			return Clusters;
		}

		/// <summary>
		/// Merge the clusters to which the two points belong, if their sizes permit.
		/// 
		/// No more than one of the clusters may have a size greater than or equal to UnmergeableSize.
		/// </summary>
		/// <param name="p1">Point belonging to first cluster to merge.</param>
		/// <param name="p2">Point belonging to second cluster to merge.</param>
		/// <returns>True if the merge was performed successfully, false otherwise.</returns>
		private bool Merge(UnsignedPoint p1, UnsignedPoint p2)
		{
			var category1 = Clusters.GetClassLabel(p1);
			var category2 = Clusters.GetClassLabel(p2);
			if (category1.Equals(category2))
				return false;
			var size1 = Clusters.PointsInClass(category1).Count;
			var size2 = Clusters.PointsInClass(category2).Count;
			if (size1 >= UnmergeableSize && size2 >= UnmergeableSize)
				return false;
			return Clusters.Merge(category1, category2);
		}

		/// <summary>
		/// Merges the outliers to their nearest neighboring large cluster.
		/// In this case, any cluster smaller than UnmergeableSize is considered an outlier.
		/// </summary>
		private void MergeOutliers()
		{
			var cc = new ClosestCluster<string>(Clusters);
			foreach (var cp in cc.FindClosestOutliers(Clusters.NumPartitions, long.MaxValue, UnmergeableSize))
				Merge(cp.Point1, cp.Point2);
		}
	}
}
