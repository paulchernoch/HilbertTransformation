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

		public ClusterCounter()
		{
			OutlierSize = 1;
			NoiseSkipBy = 0;
		}

		public ClusterCount Count(IList<HilbertPoint> points)
		{
			var neighborDistances = new List<long>();
			HilbertPoint previousPoint = null;
			foreach (var point in points)
			{
				if (previousPoint != null)
					neighborDistances.Add(point.Measure(previousPoint));
				previousPoint = point;
			}

			var indexOfMaximumIncrease = 0;
			var indexOfMaximumRatio = 0;
			var maxIncrease = 0L;
			var maxRatio = 0.0;

			var numPoints = points.Count();
			var sortedDistances = neighborDistances.OrderBy(p => p).ToList();
			for (var iDistance = 1 + NoiseSkipBy; iDistance < sortedDistances.Count; iDistance++)
			{
				var distance = sortedDistances[iDistance];
				var previousDistance = sortedDistances[iDistance - 1 - NoiseSkipBy];
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

						if (iDistance > numPoints / 2 && maxRatio > 5)
							break;
					}
				}
			}

			int indexToUse;
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

			indexToUse = Math.Max(0, indexToUse - NoiseSkipBy - 1);

			var maximumSquareDistance = sortedDistances[indexToUse];

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
	}
}
