using System;
using System.Collections.Generic;
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
		/// Multiplied by the MergeSquareDistance to get the Neighborhood radius.
		/// </summary>
		public double NeighborhoodRadiusMultiplier { get; set; } = 2.0 / 5.0;

		/// <summary>
		/// Defines the radius around a point of the hypersphere to be searched for the neighbors
		/// that contribute to the density.
		/// </summary>
		public long NeighborhoodRadius { get { return (long)(MergeSquareDistance * NeighborhoodRadiusMultiplier); } }

		/// <summary>
		/// All clusters smaller than this size are outliers and may be merged to the nearest cluster of a larger size.
		/// </summary>
		/// <value>The size of the outlier.</value>
		public int OutlierSize { get; set; } = 5;

		/// <summary>
		/// Gets or sets the acceptable shrinkage ratio that permits two clusters to be combined.
		/// If two clusters were to be combined and that would shrink the radius of the combined cluster sufficiently,
		/// then such a combination would be performed.
		/// This should be a value between zero and one, probably in the range one half to 0.9, but 
		/// experiments are needed.
		/// 
		/// Setting this to zero means that no merging based on radius shrinkage will be attempted.
		/// </summary>
		public double MergeableShrinkage { get; set; } = 0;

		public HilbertIndex Index { get; set; }

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
			// 1. Get the Neighborhood density for all points and rank them by that density, from densest to most diffuse.
			var dm = new DensityMeter(Index, NeighborhoodRadius);

			// 2. Sort all pairs of points that are adjacent in the Hilbert ordering by the greater of the 
			//    density for the two points.
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
					Density = Math.Max(
						dm.EstimatedDensity(points[i],dm.Distances.WindowRadius), 
						dm.EstimatedDensity(points[i + 1], dm.Distances.WindowRadius)
					)
				})
				.Where(ppd => ppd.Point1.SquareDistanceCompare(ppd.Point2, MergeSquareDistance) <= 0) //TODO: Use dm.Distances???
				.OrderByDescending(ppd => ppd.Density);

			// 4. Merge clusters that pass the test.
			foreach (var orderedMerge in orderedMerges)
				Merge(orderedMerge.Point1, orderedMerge.Point2);

			// 5. Merge all outliers to the nearest large cluster. 
			//    In the end, we will only have large clusters remaining.
			MergeOutliers();

			// 6. Merge together clusters if it creates a new cluster with a radius smaller 
			//    than the sum of the radii of the component clusters.
			MergeByRadius();

			return Clusters;
		}

		/// <summary>
		/// Merge the clusters to which the two points belong, if their sizes permit.
		/// 
		/// No more than one of the clusters may have a size greater than or equal to UnmergeableSize.
		/// </summary>
		/// <param name="p1">Point belonging to first cluster to merge.</param>
		/// <param name="p2">Point belonging to second cluster to merge.</param>
		/// <param name="forceMerge">If true and UnmergeableSize is the sole obstacle to the merge, perform the merge anyways.
		/// If false, honor UnmergeableSize.</param>
		/// <returns>True if the merge was performed successfully, false otherwise.</returns>
		private bool Merge(UnsignedPoint p1, UnsignedPoint p2, bool forceMerge = false)
		{
			var category1 = Clusters.GetClassLabel(p1);
			var category2 = Clusters.GetClassLabel(p2);
			if (category1.Equals(category2))
				return false;
			var size1 = Clusters.PointsInClass(category1).Count;
			var size2 = Clusters.PointsInClass(category2).Count;
			if (size1 >= UnmergeableSize && size2 >= UnmergeableSize  && !forceMerge)
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

		protected class RadiusMergeCandidate: IComparable<RadiusMergeCandidate>
		{
			public string Label1 { get; set; }
			public UnsignedPoint Point1 { get; set; }

			public string Label2 { get; set; }
			public UnsignedPoint Point2 { get; set; }

			public ClusterRadius CombinedRadius { get; set; }

			public double Shrinkage { get; set; }

			public RadiusMergeCandidate(
				Classification<UnsignedPoint, string> clusters,
				string label1,
				ClusterRadius radius1,
				string label2,
				ClusterRadius radius2
			)
			{
				Label1 = label1;
				Point1 = clusters.PointsInClass(Label1).First();
				Label2 = label2;
				Point2 = clusters.PointsInClass(Label2).First();
				CombinedRadius = new ClusterRadius(clusters.PointsInClass(Label1), clusters.PointsInClass(Label2));
				Shrinkage = CombinedRadius.Shrinkage(radius1, radius2);
			}

			public int CompareTo(RadiusMergeCandidate other)
			{
				return Shrinkage.CompareTo(other.Shrinkage);
			}
		}

		/// <summary>
		/// Compare every cluster to every other cluster and decide if we should merge them based on 
		/// whether the radius of the combined cluster is less than the sum of the radii of the original clusters.
		/// </summary>
		/// <returns>True if any merges were performed, false otherwise.</returns>
		bool MergeByRadius()
		{
			int mergeCount = 0;
			if (MergeableShrinkage <= 0 || Clusters.NumPartitions == 1)
				return false;
			Timer.Start("Merge by radius");
			var Radii = new Dictionary<string, ClusterRadius>();
			foreach (var label in Clusters.ClassLabels())
				Radii[label] = new ClusterRadius(Clusters.PointsInClass(label).ToList());
			var potentialMerges = new List<RadiusMergeCandidate>();
			var minShrinkage = double.MaxValue;
			foreach (var label1 in Clusters.ClassLabels())
				foreach (var label2 in Clusters.ClassLabels().Where(label => label1.CompareTo(label) == -1))
				{
					var potentialMerge = new RadiusMergeCandidate(
						Clusters,
						label1,
						Radii[label1],
						label2,
						Radii[label2]
					);
					minShrinkage = Math.Min(minShrinkage, potentialMerge.Shrinkage);
					if (potentialMerge.Shrinkage <= MergeableShrinkage)
						potentialMerges.Add(potentialMerge);
				}
			//TODO: Should we process merges from low shrinkage to high, and regenerate results after each merge? 
			//      This is in case merging A + B is allowed and B + C is allowed, but 
			//      after A + B are merged to form D, C + D are not a good merge.
			//      For now, process all merges that pass the Shrinkage test.
			foreach (var potentialMerge in potentialMerges)
			{
				if (Merge(potentialMerge.Point1, potentialMerge.Point2, true))
					mergeCount++;
			}
			Logger.Info($"{mergeCount} cluster pairs successfully merged by radius, with {potentialMerges.Count} expected.");
			Logger.Info($"Radius shrinkage values: Best {minShrinkage} vs Permitted {MergeableShrinkage}");
			Timer.Stop("Merge by radius");
			return mergeCount > 0;
		}
	}
}
