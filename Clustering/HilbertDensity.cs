using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Estimate the local density of points near a given point, and the average density across all points.
	/// 
	/// This derives the square of the radius of the hypersphere around each point that contains
	/// the desired number of neighboring points. Thus the measure of density is a square distance, not 
	/// points per unit volume. The smaller the square distance, the higher the density.
	/// (The volumes in hyperspace are huge, hence would lead to minuscule values.)
	/// 
	/// SortByDensity can be used to find the points where the data is densest, to drive a 
	/// density-based clustering algorithm.
	/// </summary>
	public class HilbertDensity
	{
		private HilbertIndex Index { get; set; }

		/// <summary>
		/// Points to study, sorted in Hilbert Order.
		/// </summary>
		private List<HilbertPoint> SortedPoints { get { return Index.SortedPoints; } }

		/// <summary>
		/// The number of near neighboring points that must occupy the enclosing hypersphere
		/// used to determine density.
		/// </summary>
		public int NeighborCount { get; private set; }

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

		#region Neigborhood Property and computation

		private long _neighborhood = -1L;

		/// <summary>
		/// The average square radius of the hypersphere that encloses NeighborCount points.
		/// </summary>
		public long Neighborhood { 
			get
			{
				if (_neighborhood < 0)
					_neighborhood = EstimateNeighborhood();
				return _neighborhood;
			}
		} 

		/// <summary>
		/// Estimate the square radius of the hypersphere around the average point necessary
		/// to include a number of neighbors equaling NeighborCount.
		/// </summary>
		private long EstimateNeighborhood()
		{
			var sum = 0L;
			var count = 0;
			var windowSize = 2 * NeighborCount + 1;
			// We average the square radius, instead of squaring the average radius. 
			// This will produce a larger number.
			for (var i = 0; i < SortedPoints.Count - windowSize; i++)
			{
				var centerPoint = SortedPoints[i + NeighborCount];
				var sqDistances = new List<long>();
				for (var j = 0; j < 2 * NeighborCount + 1; j++)
				{
					if (j == NeighborCount) continue; // Exclude the centerPoint itself.
					var p = SortedPoints[i + j];
					sqDistances.Add(centerPoint.Measure(p));
				}
				sqDistances.Sort();
				sum += sqDistances[NeighborCount];
				count++;
			}
			return ((sum + count - 1) / count);
		}

		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Clustering.HilbertDensity"/> class.
		/// </summary>
		/// <param name="index">Hilbet index of points.</param>
		/// <param name="neighborCount">Determine the size of the neighborhood using this count of points.
		/// The average radius of the hypersphere (squared) that contains this many points is the Neighborhood.
		/// </param>
		public HilbertDensity(HilbertIndex index, int neighborCount = 10)
		{
			Index = index;
			NeighborCount = neighborCount;
		}

		/// <summary>
		/// Estimates the number of neighbors within the Neighborhood for all points.
		/// 
		/// This uses the Hilbert curve order to find neighbors, and might miss some neighbors,
		/// so is an estimate. It is a lower bound.
		/// </summary>
		/// <param name="windowSize">The number of points that are close along the Hilbert curve and will
		/// be compared for distance is twice this windowSize. If you give a window size equal to N/2 or greater,
		/// all points will be compared and the result will not be an estimate, it will be accurate.
		/// </param>
		/// <returns>Key is the point, value is its neighbor count</returns>
		public Dictionary<HilbertPoint, int> EstimateNeighbors(int windowSize)
		{
			var neighborCounts = new Dictionary<HilbertPoint, int>();
			foreach(var pair in SortedPoints.Select((p,i) => new { Point = p, Position = i }))
				neighborCounts[pair.Point] = EstimateNeighbors(pair.Position, windowSize);
			return neighborCounts;
		}

		/// <summary>
		/// Sort the points by decreasing neighborhood density.
		/// </summary>
		/// <returns>The points sorted by decreasing density.</returns>
		/// <param name="windowSize">Window size for measurement.</param>
		public IEnumerable<HilbertPoint> SortByDensity(int windowSize)
		{
			return SortedPoints
				.Select((p, i) => new { Point = p, Neighbors = EstimateNeighbors(i, windowSize) })
				.OrderByDescending(pair => pair.Neighbors)
				.Select(pair => pair.Point);
		}

		/// <summary>
		/// For each point taken in Hilbert order, find its rank in terms of density.
		/// Each rank relates to point at the corresponding position in the OrderedPoints.
		/// </summary>
		/// <returns>A list of one-based ranks.</returns>
		/// <param name="windowSize">Window size.</param>
		public int[] DensityRank(int windowSize)
		{
			var sortedByDensity = SortByDensity(windowSize).ToList();
			var ranks = new int[sortedByDensity.Count];
			foreach (var pair in SortByDensity(windowSize).Select((p, i) => new { Point = p, Rank = i + 1 }))
				ranks[Index.SortedPosition(pair.Point)] = pair.Rank;
			return ranks;
		}

		/// <summary>
		/// Reduce the rank for points who are near other points with low rank to encourage growth near high density regions.
		/// </summary>
		/// <returns>The ranks of corresponding points in HilbertIndex order.</returns>
		/// <param name="windowSize">Window size. 
		/// If ten, then the ten points before and ten points after a given point are the window.
		/// All points in the window are influenced by the rank of the point at the center of the window. </param>
		public int[] NeighborhoodRank(int windowSize)
		{
			var densityRank = DensityRank(windowSize);
			var neighborhoodRank = (int[])densityRank.Clone();
			neighborhoodRank[0] = neighborhoodRank.Take(windowSize + 1).Min();
			foreach (var i in Enumerable.Range(0, neighborhoodRank.Length))
			{
				var start = Math.Max(0,i - windowSize);
				var stop = Math.Min(i + windowSize, neighborhoodRank.Length);
				var minRankInNeighborhood = densityRank.Skip(start).Take(stop - start).Min();
				if (minRankInNeighborhood < densityRank[i])
					neighborhoodRank[i] = (int)((NeighborhoodRankWeight * minRankInNeighborhood + densityRank[i]) / (NeighborhoodRankWeight + 1));
			}
			return neighborhoodRank;
		}

		/// <summary>
		/// Estimates the number of points near the indicated point whose square distance does not exceed Neighborhood.
		/// </summary>
		/// <returns>The count of neighbors, which does not include the point itself. 
		/// The value can range from zero to 2 * windowSize.</returns>
		/// <param name="pointIndex">Indicates a point by its position in the Index.</param>
		/// <param name="windowSize">The number of points that are close along the Hilbert curve and will
		/// be compared for distance is twice this windowSize. If you give a window size equal to N/2 or greater,
		/// all points will be compared and the result will not be an estimate, it will be accurate.
		/// </param>
		public int EstimateNeighbors(int pointIndex, int windowSize)
		{
			var centerPoint = SortedPoints[pointIndex];
			return SortedPoints
				.Skip(Math.Max(0, pointIndex - windowSize))
				.Take(2 * windowSize + 1)
				.Where(p => p.UniqueId != centerPoint.UniqueId && centerPoint.SquareDistanceCompare(p, Neighborhood) <= 0)
				.Count();
		}

		/// <summary>
		/// Counts the neighbors exactly.
		/// 
		/// Note: This is much slower than EstimateNeighbors. Use in testing to prove the algorithm.
		/// </summary>
		/// <returns>The neighbors.</returns>
		/// <param name="pointIndex">Point index.</param>
		public int CountNeighbors(int pointIndex)
		{
			var centerPoint = SortedPoints[pointIndex];
			return SortedPoints
				.Where(p => p.UniqueId != centerPoint.UniqueId && centerPoint.SquareDistanceCompare(p, Neighborhood) <= 0)
				.Count();
		}

		/// <summary>
		/// Estimates the number of points near the indicated point whose square distance does not exceed Neighborhood.
		/// </summary>
		/// <returns>The count of neighbors, which does not include the point itself. 
		/// The value can range from zero to 2 * windowSize.</returns>
		/// <param name="centerPoint">Point to find neighbors for.</param>
		/// <param name="windowSize">The number of points that are close along the Hilbert curve and will
		/// be compared for distance is twice this windowSize. If you give a window size equal to N/2 or greater,
		/// all points will be compared and the result will not be an estimate, it will be accurate.
		/// </param>
		public int EstimateNeighbors(HilbertPoint centerPoint, int windowSize)
		{
			var index = Index.SortedPosition(centerPoint);
			return EstimateNeighbors(index, windowSize);
		}


	}
}
