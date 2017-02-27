using System;
using System.Linq;
using System.Collections.Generic;
using HilbertTransformation;

namespace Clustering
{
	
	/// <summary>
	/// Solve the Poly chromatic closest point problem either exactly or approximately.
	/// 
	/// Given points in multiple clusters (multiple 'colors'), find the pair of points that
	/// is closest together, with each point of a different color.
	/// 
	/// Exact and approximate measurements are provided.
	/// 
	/// The most interesting method is FindClosestClusters. It fits into an overall clustering algorithm 
	/// in the following way. The overall algorithm:
	///   1. A rough clustering is performed. 
	///      The result is full clusters, fragmented clusters that still need to be combined, and outliers.
	///   2. Clusters that are not outliers (i.e. having few members) are compared to one another. 
	///      If they are close enough, they are combined.
	///   3. Outliers are attached to the nearest non-outlier cluster.
	/// 
	/// FindClosestClusters is part of step 2. It finds candidates for merging, but does not actually perform the merge.
	/// </summary>
	/// <remarks>
	/// Alternative versions of several methods balance accuracy and execution time.
	/// Assume:
	///      - N is # of points
	///      - K is # of clusters and is less than SQRT(N).
	///      - P is average cluster size = N/K.
	///      - F is fragmentation of clusters when formed from consecutive points in Hilbert order (typically 2 to 10)
	/// 
	///   FindPairExhaustively, FindClusterExhaustively 
	///      - Slow but Fully accurate, 
	///      - time to find distance from one cluster to one other cluster = P^2
	///      - time to find distance from one cluster to all others ~ P(N-P) = (N/K)(N-N/K) = PN - P^2 ~ PN
	///      - time to find distance from every cluster to every cluster ~ (K(K-1)/2) * PN = K^2 * PN/2 = KN^2/2
	/// 
	///   FindPairByCentroids
	///      - Fast but Approximate, but closest distance is usually within a tenth of a percent or better.
	///      - time to find distance from one cluster to one other cluster = 2P
	///      - time to find distance from one cluster to all others = 2(K-1)P ~ 2N
	///      - time to find distance from every cluster to every cluster = 2(K^2)P = 2KN
	///   
	/// </remarks>
	public class ClosestCluster<TLabel> where TLabel : IEquatable<TLabel>, IComparable<TLabel>
	{
		/// <summary>
		/// Holds the results of a closest pair query, indicating two clusters (identified by their "color" labels),
		/// the points in each cluster that are closest to one another (either exactly or approximately),
		/// and the distance between those two points.
		/// </summary>
		public class ClosestPair : IComparable<ClosestPair>
		{
			public TLabel Color1;
			public UnsignedPoint Point1;
			public TLabel Color2;
			public UnsignedPoint Point2;
			public long SquareDistance;

			public ClosestPair(TLabel color1, UnsignedPoint p1, TLabel color2, UnsignedPoint p2, long sqDist)
			{
				Color1 = color1;
				Point1 = p1;
				Color2 = color2;
				Point2 = p2;
				SquareDistance = sqDist;
				Validate();
			}

			public ClosestPair(TLabel color1, UnsignedPoint p1, TLabel color2, UnsignedPoint p2)
			{
				Color1 = color1;
				Point1 = p1;
				Color2 = color2;
				Point2 = p2;
				SquareDistance = Point1.Measure(Point2);
				Validate();
			}

			public ClosestPair()
			{
				SquareDistance = long.MaxValue;
			}

			void Validate()
			{
				ValidateColor(Color1, "ClosestPair.Color1 should not be null");
				ValidateColor(Color2, "ClosestPair.Color2 should not be null");
			}

			public void ValidateColor(TLabel color, string msg)
			{
				if (EqualityComparer<TLabel>.Default.Equals(color, default(TLabel)))
					throw new ArgumentNullException(nameof(color), msg);
			}

			/// <summary>
			/// If Color1 does not match color1, swap Color1 and Color2 and Point1 with Point2.
			/// </summary>
			/// <param name="color1">The color that should match Color1.</param>
			public ClosestPair Swap(TLabel color1)
			{
				ValidateColor(Color1, "Swapping with null Color1");
				if (!Color1.Equals(color1))
				{
					var tempColor = Color1;
					Color1 = Color2;
					Color2 = tempColor;
					var tempPoint = Point1;
					Point1 = Point2;
					Point2 = tempPoint;
				}
				return this;
			}

			/// <summary>
			/// Since categories may have been merged since this was created, update Color1 and Color2
			/// to reflect the current categorization of Point1 and Point2.
			/// </summary>
			/// <param name="clusters">Current clustering of points.</param>
			public ClosestPair Relabel(Classification<UnsignedPoint, TLabel> clusters)
			{
				Color1 = clusters.GetClassLabel(Point1);
				Color2 = clusters.GetClassLabel(Point2);
				return this;
			}

			/// <summary>
			/// Count how many of the two clusters are outliers.
			/// 
			/// NOTE: It may be necessary to call Relabel first if any merges have occurred since this object was created,
			/// since Point1 and Point2 mayh now be in different clusters than before.
			/// </summary>
			/// <param name="clusters">Current clustering of points.</param>
			/// <param name="maxOutlierSize">Any cluster whose size does not equal or exceed this value is an outlier.</param>
			/// <returns>Zero if neither cluster is an outlier, otherwise one or two.</returns>
			public int CountOutliers(Classification<UnsignedPoint, TLabel> clusters, int maxOutlierSize)
			{
				var size1 = clusters.PointsInClass(Color1).Count;
				var size2 = clusters.PointsInClass(Color2).Count;
				return (size1 < maxOutlierSize ? 1 : 0) + (size2 < maxOutlierSize ? 1 : 0);
			}

			/// <summary>
			/// If both Colors match, the points have already been merged.
			/// </summary>
			public bool AreAlreadyMerged { get { return Color1.Equals(Color2); } }

			#region IComparable implementation

			public int CompareTo(ClosestPair other)
			{
				return SquareDistance.CompareTo(other.SquareDistance);
			}

			#endregion

			public override string ToString()
			{
				return string.Format("Clusters {0} and {1}, Sq Dist = {2}", Color1, Color2, SquareDistance);
			}

			/// <summary>
			/// Compose a concatenated key that combines Color1 and Color2 to use as the key in a Dictionary.
			/// 
			/// Do this such that the one earliest according to comparison collation order is first in the concatenation.
			/// This means that if Color1 and Color2 are swapped, the same key is generated, since the distance from A to B 
			/// is symmetric with the distance from B to A.
			/// </summary>
			/// <value>The compound key.</value>
			public string Key
			{
				get
				{
					var c1 = Color1.ToString();
					var c2 = Color2.ToString();
#pragma warning disable RECS0064 // Warns when a culture-aware 'string.CompareTo' call is used by default
					if (c1.CompareTo(c2) < 0)
#pragma warning restore RECS0064 // Warns when a culture-aware 'string.CompareTo' call is used by default
						return c1 + ":" + c2;
					else
						return c2 + ":" + c1;
				}
			}

		}

		/// <summary>
		/// Many points divided into clusters.
		/// </summary>
		/// <value>The clusters.</value>
		public Classification<UnsignedPoint, TLabel> Clusters { get; private set; }

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the ClosestCluster class.
		/// </summary>
		/// <param name="clusters">Classifies the points into distinct clusters.</param>
		public ClosestCluster(Classification<UnsignedPoint, TLabel> clusters)
		{
			Clusters = clusters;
		}

		#endregion

		#region Finding closest pair

		/// <summary>
		/// Finds exactly the two closest points (one of each color) and their square distance 
		/// using an exhaustive algorithm that compares the distances of every point in one cluster 
		/// to every point in the other.
		///
		/// This compares points in two of the clusters, ignoring points in all other clusters.
		/// </summary>
		/// <param name="color1">Label of the first cluster.</param>
		/// <param name="color2">Label of the second cluster.</param>
		/// <returns>The point with Color1, the point with Color2 and the square distance between them.</returns>
		public ClosestPair FindPairExhaustively(TLabel color1, TLabel color2)
		{
			var shortestDistance = long.MaxValue;
			UnsignedPoint p1Shortest = null;
			UnsignedPoint p2Shortest = null;
			foreach (var p1 in Clusters.PointsInClass(color1))
				foreach (var p2 in Clusters.PointsInClass(color2))
				{
					var d = p1.Measure(p2);
					if (d < shortestDistance)
					{
						shortestDistance = d;
						p1Shortest = p1;
						p2Shortest = p2;
					}
				}
			return new ClosestPair(color1, p1Shortest, color2, p2Shortest, shortestDistance).Swap(color1);
		}


		/// <summary>
		/// Searches for the point in the first cluster that is closest to a corresponding point in the second cluster
		/// and returns an approximate result. 
		/// 
		/// This finds the centroid C1 of the first cluster, then the point P2 in the second cluster closest to centroid C1, then the
		/// point P1 in the first cluster closest to P2. 
		/// 
		/// NOTE: If the two clusters overlap or are shaped irregularly, this is likely to return a poor result.
		/// If the clusters are spherical, the results are likely to be very good.
		/// </summary>
		/// <param name="color1">Indicates the first cluster to be searched.</param>
		/// <param name="color2">Indicates the second cluster to be searched.</param>
		/// <returns>An approximate result, inclusing one point from each cluster and the square of the distance between them.</returns>
		public ClosestPair FindPairByCentroids(TLabel color1, TLabel color2)
		{
			var points1 = Clusters.PointsInClass(color1);
			var points2 = Clusters.PointsInClass(color2);
			var c1 = UnsignedPoint.Centroid(points1);
			var p2 = points2
				.OrderBy(p => c1.Measure(p))
				.First()
			;
			var closest = points1
				.Select(p1 => new ClosestPair(color1, p1, color2, p2, p1.Measure(p2)))
				.OrderBy(cp => cp.SquareDistance)
				.First();
			return closest.Swap(color1);
		}

		/// <summary>
		/// Finds exactly the cluster closest to the cluster whose label matches color1 and the points
		/// in each cluster that are closest, along with the square distance between them.
		/// </summary>
		/// <param name="color1">Label of the cluster whose nearest neighbor is being sought.</param>
		/// <returns>A point in the cluster corresponding to color1, the closest point to it
		/// from another cluster, the square distance between the points, and the label of the other cluster.
		/// NOTE: ClosestPair.Color1 will equal color1.
		/// </returns>
		public ClosestPair FindClusterExhaustively(TLabel color1)
		{
			var shortest = new ClosestPair();
			foreach (var p1 in Clusters.PointsInClass(color1))
				foreach (var pc in Clusters.Points()
					.Select(p => new { Point = p, Color = Clusters.GetClassLabel(p) })
					.Where(pc => !color1.Equals(pc.Color)))
				{
					var d = p1.Measure(pc.Point);
					if (d < shortest.SquareDistance)
					{
						shortest.SquareDistance = d;
						shortest.Color1 = color1;
						shortest.Point1 = p1;
						shortest.Color2 = pc.Color;
						shortest.Point2 = pc.Point;
					}
				}
			//TODO: If there is only one cluster, the if statement above will not be triggered and shortest will
			//      be ill-defined and cause a Null Pointer exception in Swap.
			return shortest.Swap(color1);
		}

		#endregion



		#region Find Nearest neighbors of centroids

		public class ClusterCentroid: IComparable<ClusterCentroid>
		{
			public UnsignedPoint Centroid { get; set; }
			public TLabel ClusterLabel { get; set; }
			public int Count { get; set; }

			public override int GetHashCode() {return ClusterLabel.GetHashCode();}
			public override bool Equals(object obj)
			{
				var cc = obj as ClusterCentroid;
				return cc != null && ClusterLabel.Equals(cc.ClusterLabel);
			}

			public int CompareTo(ClusterCentroid other)
			{
				return ClusterLabel.CompareTo(other.ClusterLabel);
			}
		}

		/// <summary>
		/// Gets the centroids for each cluster.
		/// </summary>
		/// <returns>The centroids and their class labels.</returns>
		public List<ClusterCentroid> GetCentroids()
		{
			return Clusters
				.ClassLabels()
				.Select(label => new ClusterCentroid { 
					ClusterLabel = label, 
					Centroid = UnsignedPoint.Centroid(Clusters.PointsInClass(label)),
					Count = Clusters.PointsInClass(label).Count
				}).ToList();
		}

		public class CentroidPair:IComparable<CentroidPair>
		{
			public ClusterCentroid A { get; set; }
			public ClusterCentroid B { get; set; }
			public long Measure { get; set; }

			/// <summary>
			/// Sort first by distance, then by A.ClusterLabel, then by B.ClusterLabel.
			/// </summary>
			/// <returns>The to.</returns>
			/// <param name="other">Other.</param>
			public int CompareTo(CentroidPair other)
			{
				var cmp = Measure.CompareTo(other.Measure);
				if (cmp != 0) return cmp;
				cmp = A.ClusterLabel.CompareTo(other.A.ClusterLabel);
				if (cmp != 0) return cmp;
				return B.ClusterLabel.CompareTo(other.B.ClusterLabel);
			}
		}

		/// <summary>
		/// For every cluster, finds those neighboring clusters whose centroids are closest to one another.
		/// </summary>
		/// <returns>Pairs of closest clusters, the points in those clusters that are the basis of the judgment 
		/// and the distance between those pairs of points.</returns>
		/// <param name="maxNeighbors">No more than this number of neighbors will be returned for each cluster.</param>
		/// <param name="maxDistance">Two clusters will not be considered close neighbors if the
		/// square distance between their nearest points exceeds this value.</param>
		/// <param name="minSize">If a cluster has fewer than this number of points, exclude it from analysis.
		/// Such clusters are likely outliers and are often handled separately.</param>
		/// <param name="exact">If true, an exact computation is made when finding how close two clusters are,
		/// but the first elimination of clusters not near enough to compare is still inexact. 
		/// If false, the approximate distance between clusters will be found.</param>
		public List<ClosestPair> FindClosestClusters(int maxNeighbors, long maxDistance, int minSize, bool exact)
		{
			var closest = new List<ClosestPair>();
			var centroids = GetCentroids().Where(c => c.Count >= minSize).ToList();

			// We will compare every centroid to every other centroid (excluding small outliers)
			// but only go further and find the closest pair of points in the two clusters for 
			// the pairs whose centroids are among the closest.
			// countByLabel will tally how many comparisons have been done for a given label,
			// to ensure we do not exceed maxNeighbors.
			var countByLabel = new Dictionary<TLabel, int>();
			foreach (var label in Clusters.ClassLabels())
				countByLabel[label] = 0;

			var centroidDistances = new List<CentroidPair>();

			// Triangular loop comparison, so that if we compare cluster A to B, we do not also compare cluster B to A.
			for (var i = 0; i < centroids.Count - 1; i++)
				for (var j = i + 1; j < centroids.Count; j++)
				{
					var centroid1 = centroids[i];
					var centroid2 = centroids[j];
					var measure = centroid1.Centroid.Measure(centroid2.Centroid);
					centroidDistances.Add(new CentroidPair { A = centroid1, B = centroid2, Measure = measure });
				}
			foreach (var pair in centroidDistances.OrderBy(p => p))
			{
				int countA = countByLabel[pair.A.ClusterLabel];
				int countB = countByLabel[pair.B.ClusterLabel];
				if (countA < maxNeighbors || countB < maxNeighbors)
				{
					countByLabel[pair.A.ClusterLabel] = countA + 1;
					countByLabel[pair.B.ClusterLabel] = countB + 1;
					ClosestPair close;
					if (exact)
						close = FindPairExhaustively(pair.A.ClusterLabel, pair.B.ClusterLabel);
					else //TODO: We already computed the centroids, so pass them into FindPairByCentroids instead of recomputing there.
						close = FindPairByCentroids(pair.A.ClusterLabel, pair.B.ClusterLabel);
					if (close.SquareDistance <= maxDistance)
						closest.Add(close);
				}
			}
			return closest;
		}

		/// <summary>
		/// For every cluster that is not an outlier, finds neighboring outliers.
		/// 
		/// Unlike FindClosestClusters, exact distance from outlier clusters to large clusters is computed,
		/// never an approximation.
		/// 
		/// NOTE: A given outlier may be close to more than one large cluster.
		/// </summary>
		/// <returns>Pairs of closest clusters where one cluster is an outlier and the other is not,
		/// the points in those clusters that are the basis of the judgment 
		/// and the distance between those pairs of points.
		/// The list is sorted in increasing order by distance.</returns>
		/// <param name="maxNeighbors">No more than this number of neighbors will be returned for each cluster.</param>
		/// <param name="maxDistance">Two clusters will not be considered close neighbors if the
		/// square distance between their nearest points exceeds this value.</param>
		/// <param name="minSize">If a cluster has fewer than this number of points, it is an outlier.</param>
		public List<ClosestPair> FindClosestOutliers(int maxNeighbors, long maxDistance, int minSize)
		{
			var closest = new List<ClosestPair>();

			var centroids = GetCentroids();

			// We will compare every centroid to every other centroid (excluding small outliers)
			// but only go further and find the closest pair of points in the two clusters for 
			// the pairs whose centroids are among the closest.
			// countByLabel will tally how many comparisons have been done for a given label,
			// to ensure we do not exceed maxNeighbors.
			var countByLabel = new Dictionary<TLabel, int>();
			foreach (var label in Clusters.ClassLabels())
				countByLabel[label] = 0;

			var centroidDistances = new List<CentroidPair>();

			// Triangular loop comparison, so that if we compare cluster A to B, we do not also compare cluster B to A.
			for (var i = 0; i < centroids.Count - 1; i++)
				for (var j = i + 1; j < centroids.Count; j++)
				{
					var centroid1 = centroids[i];
					var centroid2 = centroids[j];
					// Only try pairs where one is an outlier and the other is not.
					if (centroid1.Count < minSize == centroid2.Count < minSize)
						continue;
					var measure = centroid1.Centroid.Measure(centroid2.Centroid);
					centroidDistances.Add(new CentroidPair { A = centroid1, B = centroid2, Measure = measure });
				}
			foreach (var pair in centroidDistances.OrderBy(p => p))
			{
				int countA = countByLabel[pair.A.ClusterLabel];
				int countB = countByLabel[pair.B.ClusterLabel];
				if (countA < maxNeighbors || countB < maxNeighbors)
				{
					countByLabel[pair.A.ClusterLabel] = countA + 1;
					countByLabel[pair.B.ClusterLabel] = countB + 1;

					var close = FindPairExhaustively(pair.A.ClusterLabel, pair.B.ClusterLabel);

					if (close.SquareDistance <= maxDistance)
						closest.Add(close);
				}
			}
			return closest.OrderBy(c => c.SquareDistance).ToList();
		}

		#endregion
	}
}

