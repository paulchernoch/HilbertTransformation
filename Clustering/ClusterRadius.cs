using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Finds the centroid, maximum radius and average radius of a group of points.
	/// </summary>
	public class ClusterRadius
	{
		/// <summary>
		/// Maximum distance (not square distance) from centroid to the points.
		/// </summary>
		public double MaximumRadius { get; set; }

		/// <summary>
		/// Mean distance (not square distance) from centroid to the points.
		/// </summary>
		/// <value>The mean radius.</value>
		public double MeanRadius { get; set; }

		/// <summary>
		/// Centroid of the points.
		/// </summary>
		public UnsignedPoint Centroid { get; set; }

		public ClusterRadius(IList<UnsignedPoint> points)
		{
			Centroid = UnsignedPoint.Centroid(points);
			var radiusSum = 0.0;
			MaximumRadius = 0;
			foreach (var point in points)
			{
				var distance = Centroid.Distance(point);
				MaximumRadius = Math.Max(MaximumRadius, distance);
				radiusSum += distance;
			}
			if (points.Count > 0)
				MeanRadius = radiusSum / points.Count;
		}

		/// <summary>
		/// Finds the radius and centroid of the union of the points in the two clusters.
		/// </summary>
		/// <param name="cluster1">First cluster to combine.</param>
		/// <param name="cluster2">Second cluster to combine.</param>
		public ClusterRadius(IEnumerable<UnsignedPoint> cluster1, IEnumerable<UnsignedPoint> cluster2): this(cluster1.Concat(cluster2).ToList())
		{
		}

		/// <summary>
		/// Assuming this ClusterRadius to be for a combination of two clusters, compare its radius to the 
		/// sum of radii for the two clusters tentatively combined to form it and return the ratio as the shrinkage.
		/// 
		/// This is to be used to help the DensityClassifier decide if two clusters it has carved out of a single original cluster
		/// should really be put back together.
		/// 
		/// Instead of comparing the maximum radius or the mean radius, the two values are blended together.
		/// The reason is that outliers can inflate the radius of a cluster, while the mean could grossly understate 
		/// the spatial extent of a cluster.
		/// </summary>
		/// <returns>The shrinkage. If the radius of the combined cluster exceeds the sum of the radii of its parts,
		/// this is a value greater than one.
		/// If the combination has a lower radius, this value is less than one.
		/// Only if the value is lower than one is it appropriate to merge the cluster.
		/// How much less than one is a good value is a matter for further research.
		/// </returns>
		/// <param name="part1">Statistics about the first component cluster.</param>
		/// <param name="part2">Statistics about the second component cluster.</param>
		public double Shrinkage(ClusterRadius part1, ClusterRadius part2)
		{
			var combined = this;
			var partsMaximumSum = part1.MaximumRadius + part2.MaximumRadius;
			var partsMeanSum = part1.MeanRadius + part2.MeanRadius;
			//TODO: Research how to weight the values. Use 2:1 weighting for now. 1:1 did not work as well as expected.
			var partsSumWeightedRadius = (2 * partsMaximumSum + partsMeanSum) / 3;
			var combinedWeightedRadius = (2 * combined.MaximumRadius + combined.MeanRadius) / 3;
			if (combinedWeightedRadius == 0) return 0;
			return combinedWeightedRadius / partsSumWeightedRadius;
		}
	}
}
