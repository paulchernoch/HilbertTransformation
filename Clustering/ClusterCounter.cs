using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Given a list of points sorted according a Hilbert curve, estimate how many clusters there are and 
	/// how near points should be to be considered part of the same cluster.
	/// 
	/// This estimate will be an upper bound on the true number of clusters, which is likely to be 
	/// three or four times smaller.
	/// 
	/// These results can then be fed into a clustering algorithm to identify a more accurate count.
	/// </summary>
	public class ClusterCounter
	{

		/// <summary>
		/// Results of calling ClusterCounter.Count, which includes an estimate 
		/// of how many clusters the points might be grouped into.
		/// </summary>
		public class ClusterCount
		{
			/// <summary>
			/// Inferred upper bound on the number of clusters, excluding outliers.
			/// Clusters whose size is less than a specified threshhold are considered outliers.
			/// </summary>
			public int CountExcludingOutliers { get; set; }

			/// <summary>
			/// Inferred upper bound on the number of clusters, including outliers.
			/// </summary>
			public int CountIncludingOutliers { get; set; }

			/// <summary>
			/// The largest square distance between two points that are likely to belong in the same cluster.
			/// </summary>
			public long MaximumSquareDistance { get; set; }

			/// <summary>
			/// Median value of the square distance between points, excluding points whose square distance exceeds MaximumSquareDistance.
			/// </summary>
			/// <value>The median square distance.</value>
			public long MedianSquareDistance { get; set; }

			/// <summary>
			/// Approximate number of outlying points. 
			/// 
			/// Once the points are divvied up into provisional clusters, if that cluster's size does not exceed OutlierSize,
			/// it's members are tallied as outliers. 
			/// </summary>
			public int Outliers { get; set; }

			public override string ToString()
			{
				return $"[ClusterCount. Excluding outliers={CountExcludingOutliers}, Including outliers={CountIncludingOutliers}. Square Distance: Maximum={MaximumSquareDistance}, Median={MedianSquareDistance}]. Outliers={Outliers}"; 
			}
		}

		/// <summary>
		/// If the number of points tentatively clustered together is less than or equal to this number, 
		/// the points are considered outliers and the cluster not counted. 
		/// </summary>
		public int OutlierSize { get; set; }

		/// <summary>
		/// Influences the count by widening the range of indices over which the maximum jump in distance is calculated.
		/// 
		/// If there is no noise and all the clusters are well separated, use zero.
		/// </summary>
		/// <remarks>
		/// When comparing consecutive distances after they are sorted, skip over this many items to discover
		/// how fast the distances are changing. 
		/// If all clusters are well separated and there are no noise points, zero works best. 
		/// Otherwise, choose a larger value. This value should be less than any expected number of clusters. 
		/// Try ten. The more noise there is in the data, the higher a value you will need to use.
		/// 
		/// Careful: this will increase the number of estimated clusters and prevent early merges. 
		/// </remarks>
		public int NoiseSkipBy { get; set; }

		/// <summary>
		/// Sets a lower value for NoiseSkipBy in case the observed estimated cluster count is less than
		/// 2 * NoiseSkipBy. The reason for this is that if the true number of clusters is low compared to 
		/// NoiseSkipBy, then the estimate of cluster count will be off by a lot.
		/// Example: If the true number of clusters is 5 and NoiseSkipBy is 10, the estimate will be at least
		/// 15, if not higher.
		/// 
		/// If this property is negative, do not reduce NoiseSkipBy.
		/// If this property is non-negative and the observed estimated cluster count is less than 2 * NoiseSkipBy,
		/// reduce the effective NoiseSkipBy to this value.
		/// </summary>
		/// <value>The reduced noise skip by.</value>
		public int ReducedNoiseSkipBy { get; set; }

		/// <summary>
		/// Track the lowest cluster count seen across many iterations. 
		/// </summary>
		/// <value>The lowest count seen.</value>
		public int LowestCountSeen { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Clustering.ClusterCounter"/> class with default values for
		/// OutlierSize (1) and NoiseSkipBy (0).
		/// 
		/// NOTE: My personal favorite values are OutlierSize = 5 and NoisesSkipBy = 10.
		/// </summary>
		public ClusterCounter()
		{
			OutlierSize = 1;
			NoiseSkipBy = 0;
			ReducedNoiseSkipBy = 0;
			LowestCountSeen = int.MaxValue;
		}

		/// <summary>
		/// Estimate how many clusters there are that have a member count equal to or greater than the OutlierSize.
		/// </summary>
		/// <param name="points">Points to study.</param>
		public ClusterCount Count(IReadOnlyList<UnsignedPoint> points)
		{
			var neighborDistances = new List<long>();
            UnsignedPoint previousPoint = null;
			foreach (var point in points)
			{
				if (previousPoint != null)
					neighborDistances.Add(point.Measure(previousPoint));
				previousPoint = point;
			}

			var numPoints = points.Count();
			//TODO: This is an N*Log(N) sort, which we can avoid. Assume that K ≤ √N and perform a Top-N select
			//      which gives us the most likely region for the jump in distances that we seek.
			//      Search for the jump in that data and derive a cluster estimate K from the shorter list.
			//      If we do this, the sort requires N*Log(√N).
			//      If we have some other upper limit of K' given to us, we can use that and get N*Log(K') performance.
			var sortedDistances = neighborDistances.OrderBy(p => p).ToList();
			var noiseSkipByToUse = 0;

			if (ReducedNoiseSkipBy >= 0 && LowestCountSeen < 2 * NoiseSkipBy)
				noiseSkipByToUse = ReducedNoiseSkipBy;
			else
				noiseSkipByToUse = NoiseSkipBy;

			int indexToUse;
			var maximumSquareDistance = FindMaximumSquareDistance(sortedDistances, noiseSkipByToUse, out indexToUse);

			// Every gap between successive points that exceeded our distance threshhold splits the
			// points into a new cluster, but only IF we consider outliers to make up their own clusters.
			var upperBoundCount = numPoints - indexToUse + 1;

			// However, if we do not want to count outliers as making up their own clusters, we need
			// to only count a gap as making a new cluster if the previous cluster size was big enough to make it
			// NOT an outlier.
			var currentSize = 1;
			var outliers = 0;
			var upperBoundCountExcludingOutliers = 0;
			foreach (var distance in neighborDistances)
			{
				if (distance <= maximumSquareDistance)
					currentSize++;
				else {
					
					if (currentSize > OutlierSize)
						upperBoundCountExcludingOutliers++; // A count of clusters
					else
						outliers += currentSize; // A count of individual points
					currentSize = 1;
				}
			}
			if (currentSize > OutlierSize)
				upperBoundCountExcludingOutliers++;
			else
				outliers += currentSize;

			return new ClusterCount
			{
				CountExcludingOutliers = upperBoundCountExcludingOutliers,
				CountIncludingOutliers = upperBoundCount,
				MaximumSquareDistance = maximumSquareDistance,
				MedianSquareDistance = sortedDistances[indexToUse / 2],
				Outliers = outliers
			};

		}

		/// <summary>
		/// Finds the maximum square distance between neighboring points that should be considered part of the same cluster.
		/// 
		/// This is the critical distance needed for single-link agglomerative clustering.
		/// </summary>
		/// <returns>The maximum square distance.</returns>
		/// <param name="sortedDistances">Squared distances between successive points that were first sorted according to the Hilbert curve,
		/// subsequently sorted by square distance from low to high.</param>
		/// <param name="noiseSkipByToUse">Noise skip by to use.</param>
		/// <param name="indexToUse">Index at which the distance abruptly jumps from a low to a much higher value.
		/// This value can subsequently be used to infer the cluster count.</param>
		private static long FindMaximumSquareDistance(List<long> sortedDistances, int noiseSkipByToUse, out int indexToUse)
		{
			var indexOfMaximumIncrease = 0;
			var indexOfMaximumRatio = 0;
			var maxIncrease = 0L;
			var maxRatio = 0.0;
			var numPoints = sortedDistances.Count() + 1;

			for (var iDistance = 1 + noiseSkipByToUse; iDistance < sortedDistances.Count; iDistance++)
			{
				var distance = sortedDistances[iDistance];
				var previousDistance = sortedDistances[iDistance - 1 - noiseSkipByToUse];
				var diff = distance - previousDistance;
				if (diff > maxIncrease)
				{
					maxIncrease = diff;
					indexOfMaximumIncrease = iDistance;
				}
				if (previousDistance > 1 && iDistance > 10)
				{
					var ratio = distance / (double)previousDistance;
					if (ratio > maxRatio)
					{
						maxRatio = ratio;
						indexOfMaximumRatio = iDistance;

						//TODO: The test "maxRatio > 5" could cause problems if we have clusters with highly varying densities.
						//      Make it a paramter?
						if (iDistance > numPoints / 2 && maxRatio > 5)
							break;
					}
				}
			}

			// If the two measures agree, we have an unambiguous choice.
			if (indexOfMaximumIncrease == indexOfMaximumRatio)
				indexToUse = indexOfMaximumIncrease;
			// If the highest ratio in length between one distance and the next is at an early index,
			// it is likely because we skipped from a really low value (like 1) to another really low value (like 10)
			// which only looks like a large jump because the values are so small.
			else if (indexOfMaximumRatio < numPoints / 2)
				indexToUse = indexOfMaximumIncrease;
			// Once we get near the end of the series of distances, the jumps between successive
			// distances can become large, but their relative change is small,
			// so rely on the ratio instead.
			else indexToUse = indexOfMaximumRatio;

			indexToUse = Math.Max(0, indexToUse - noiseSkipByToUse - 1);

			var maximumSquareDistance = sortedDistances[indexToUse];

			return maximumSquareDistance;
		}
	}
}
