using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	// For paper: "Clustering via Nonparametric Density Estimation: The R Package pdfCluster"
	// by Adelchi Azzalini of Universit`a di Padova and Giovanna Menardi of Universit`a di Padova

	/// <summary>
	/// Measure the local density of points in the neighborhood of a given point in multidimensional space
	/// using a HilbertIndex.
	/// 
	/// Accurate and estimated measurements are offered. The accurate measurements are not commensurate with 
	/// the estimated; the goal is a qualitative measure that orders points in a similar sequence. 
	/// 
	/// The accurate measurement compares a point's distance to all other points (very expensive) and counts
	/// how many points fall within a supplied radius.
	/// 
	/// The estimated measurement applies a formula to the distances between the point and a window of near neighbors
	/// drawn from the HilbertIndex.
	/// </summary>
	public class DensityMeter
	{
		/// <summary>
		/// Order the points whose density is to be measured according to the Hilbert curve.
		/// </summary>
		public HilbertIndex Index { get; private set; }

		public int Count { get { return Index.SortedPoints.Count; } }

		/// <summary>
		/// Remember for reuse the distance from some points to other points.
		/// 
		/// Only square distances that do not exceed NeighborhoodRadius are recorded, in order to save on 
		/// memory.
		/// </summary>
		public DistanceMemo Distances { get; private set; }

		/// <summary>
		/// When computing the accurate density, every point whose square distance to the search point is less
		/// than this distance will be counted.
		/// 
		/// A good value to use is a small multiple of the MaximumSquareDistance derived using a ClusterCounter.
		/// </summary>
		public long NeighborhoodRadius { get; set; }

		/// <summary>
		/// Used to estimate a density value using a series of distances from one point to some of its neighbors.
		/// 
		/// There is a case when this is ignored.
		/// </summary>
		/// <value>The estimator.</value>
		public Func<IEnumerable<long>, long> Estimator { get; set; }

		public DensityMeter(HilbertIndex index, long neighborhoodRadius, int memoWindowRadius = 0)
		{
			Index = index;
			NeighborhoodRadius = neighborhoodRadius;
			Estimator = null; // distances => distances.Count(d => d <= NeighborhoodRadius);
			Distances = new DistanceMemo(Index, neighborhoodRadius, memoWindowRadius);
		}

		/// <summary>
		/// Count exactly the number of points that are near the given point, that is, have a square distance
		/// that does not exceed NeighborhoodRadius.
		/// 
		/// This may require the comparison of the point to every other point, which is expensive.
		/// However, if this is performed for several points, many of the distance computations will be reused.
		/// 
		/// This value can be used as a density for density-based clustering.
		/// </summary>
		/// <returns>The count of neighbors.</returns>
		/// <param name="point">Point whose neighbors we must count.</param>
		public int ExactNeighbors(HilbertPoint point)
		{
			return Distances.Neighbors(point, true);
		}

		/// <summary>
		/// Estimates the density of points local to the given point.
		/// </summary>
		/// <returns>The density.</returns>
		/// <param name="point">Point whose local density is sought.</param>
		/// <param name="windowRadius">The distance to twice this many points will be measured,
		/// half to the left and half to the right of the given point along the Hilbert curve.</param>
		public long EstimatedDensity(HilbertPoint point, int windowRadius)
		{
			if (Estimator != null)
			{
				var windowSize = Math.Min(Count, 2 * windowRadius + 1);
				var iPoint1 = Index.SortedPosition(point);
				var start = Math.Max(0, iPoint1 - windowRadius);
				start = Math.Min(start, Count - windowSize);
				return Estimator(
					Enumerable
					.Range(start, windowSize)
					.Where(i => i != iPoint1)
					.Select(iPoint2 => Distances.Measure(iPoint1, iPoint2, false)));
			}
			else {
				// If our windowRadius and the one used by Distances are not the same, adjust the memo.
				if (Distances.WindowRadius != windowRadius)
					Distances.WindowRadius = windowRadius;
				return Distances.NeighborsInWindow(point);
			}
		}

		public class PointDensityRank
		{
			public HilbertPoint Point { get; set; }
			public long Density { get; set; }
			public int Rank { get; set; }
		}

		/// <summary>
		/// Sort the points in descending order by an estimate of density.
		/// </summary>
		/// <returns>The points and their associated rank and density.</returns>
		public IEnumerable<PointDensityRank> RankByEstimatedDensity(int windowRadius)
		{
			return Rank(windowRadius, true);
		}

		/// <summary>
		/// Sort the points in descending order by the exact number of neighbors it has that are nearer than NeighborhoodRadius.
		/// </summary>
		/// <returns>The points and their associated rank and density.</returns>
		public IEnumerable<PointDensityRank> RankByExactNeighbors()
		{
			return Rank(0, false);
		}

		IEnumerable<PointDensityRank> Rank(int windowRadius, bool estimated)
		{
			var previousDensity = long.MaxValue;
			var rank = 0;
			var sortedByDensity = Enumerable
				.Range(0, Count)
				.Select(i =>
				{
					var p = Index.SortedPoints[i];
					return new
					{
						Point = p,
						Density = estimated ? EstimatedDensity(p, windowRadius) : ExactNeighbors(p)
					};
				})
				.OrderByDescending(pair => pair.Density)
				.Select(pair =>
				{
					// Ties get the same rank.
					if (previousDensity.CompareTo(pair.Density) == -1)
						rank++;
					previousDensity = pair.Density;
					return new PointDensityRank { Point = pair.Point, Density = pair.Density, Rank = rank };
				});
			return sortedByDensity;
		}

	}
}
