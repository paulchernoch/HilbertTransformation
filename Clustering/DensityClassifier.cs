using System;
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
			var hd = new HilbertDensity(Index, NeighborCount);
			var ranks = hd.NeighborhoodRank(WindowSize);

			// 2. Sort all pairs of points that are adjacent in the Hilbert ordering by the lesser of the neighborhood 
			//    density for the two points, from high density to low density.

			// 3. Evaluate the distances between pairs of points.
			//    If their distance is less than MergeSquareDistance, they may be merged unless
			//    that would cause two clusters each larger than UnmergeableSize to be merged.

			// 4. Merge clusters that pass the test.

			// 5. Merge all outliers to the nearest large cluster. 
			//    In the end, we will only have large clusters remaining.

			return Clusters;
		}

		//TODO: MergeOutliers
	}
}
