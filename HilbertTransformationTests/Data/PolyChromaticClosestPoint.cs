using System;
using System.Linq;
using System.Collections.Generic;
using HilbertTransformation;
using Clustering;

namespace HilbertTransformationTests.Data
{
	// NOTE: The overarching purpose is to cluster points. In order to create good test data for our tests
	//       of an algorithm that clusters points, we need another algorithm that also clusters points.
	//       This clustering algorithm is slow and imperfect, but good enough for making test data.


	/// <summary>
	/// Solve the Poly chromatic closest point problem.
	/// 
	/// Given points in multiple clusters (multiple 'colors'), find the pair of points that
	/// is closest together, with each point of a different color.
	/// 
	/// Exact and approximate measurements are provided.
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
	///      - Fully accurate, 
	///      - time to find distance from one cluster to one other cluster = P^2
	///      - time to find distance from one cluster to all others ~ P(N-P) = (N/K)(N-N/K) = PN - P^2 ~ PN
	///      - time to find distance from every cluster to every cluster ~ (K(K-1)/2) * PN = K^2 * PN/2 = KN^2/2
	/// 
	///   FindPairByCentroids
	///      - Approximate, but closest distance is usually within a tenth of a percent or better.
	///      - time to find distance from one cluster to one other cluster = 2P
	///      - time to find distance from one cluster to all others = 2(K-1)P ~ 2N
	///      - time to find distance from every cluster to every cluster = 2(K^2)P = 2KN
	///   
	///   FindPairApproximately, FindClusterApproximately, FindAllClustersApproximately
	///      - Approximate, where closest distance may be too high by 1% to 7%
	///      - time to find distance from one cluster to one other cluster = 2F
	///      - time to find distance from one cluster to all others = 2FK
	///      - time to find distance from every cluster to every cluster = (FK)^2
	/// </remarks>
	public class PolyChromaticClosestPoint<TLabel> where TLabel : IEquatable<TLabel>
	{
		/// <summary>
		/// Holds the results of a closest pair query, indicating two clusters (identified by their "color" labels),
		/// the points in each cluster that are closest to one another (either exactly or approximately),
		/// and the distance between those two points.
		/// </summary>
		public class ClosestPair: IComparable<ClosestPair>
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
			}

			public ClosestPair(TLabel color1, UnsignedPoint p1, TLabel color2, UnsignedPoint p2)
			{
				Color1 = color1;
				Point1 = p1;
				Color2 = color2;
				Point2 = p2;
				SquareDistance = Point1.Measure(Point2);
			}

			public ClosestPair()
			{
				SquareDistance = long.MaxValue;
			}

			/// <summary>
			/// If Color1 does not match color1, swap Color1 and Color2 and Point1 with Point2.
			/// </summary>
			/// <param name="color1">The color that should match Color1.</param>
			public ClosestPair Swap(TLabel color1)
			{
				if (!Color1.Equals (color1)) 
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
				Color1 = clusters.GetClassLabel (Point1);
				Color2 = clusters.GetClassLabel (Point2);
				return this;
			}

			/// <summary>
			/// Count how many of the two clusters are outliers.
			/// 
			/// NOTE: It may be necessary to call Relabel first if any merges have occurred since this object was created,
			/// since Point1 and Point2 mayh now be in different clusters than before.
			/// </summary>
			/// <param name="clusters">Current clustering of points.</param>
			/// <param name="maxOutlierSize">Any cluster whose size does not exceed this value is an outlier.</param>
			/// <returns>Zero if neither cluster is an outlier, otherwise one or two.</returns>
			public int CountOutliers(Classification<UnsignedPoint, TLabel> clusters, int maxOutlierSize)
			{
				var size1 = clusters.PointsInClass (Color1).Count;
				var size2 = clusters.PointsInClass (Color2).Count;
				return (size1 <= maxOutlierSize ? 1 : 0) + (size2 <= maxOutlierSize ? 1 : 0);
			}

			/// <summary>
			/// If both Colors match, the points have already been merged.
			/// </summary>
			public bool AreAlreadyMerged { get { return Color1.Equals (Color2); } }

			#region IComparable implementation

			public int CompareTo (ClosestPair other)
			{
				return SquareDistance.CompareTo(other.SquareDistance);
			}

			#endregion

			public override string ToString ()
			{
				return string.Format ("Clusters {0} and {1}, Sq Dist = {2}", Color1, Color2, SquareDistance);
			}

			/// <summary>
			/// Compose a concatenated key that combines Color1 and Color2 to use as the key in a Dictionary.
			/// 
			/// Do this such that the one earliest according to comparison collation order is first in the concatenation.
			/// This means that if Color1 and Color2 are swapped, the same key is generated, since the distance from A to B 
			/// is symmetric with the distance from B to A.
			/// </summary>
			/// <value>The compound key.</value>
			public string Key { 
				get 
				{ 
					var c1 = Color1.ToString ();
					var c2 = Color2.ToString ();
#pragma warning disable RECS0064 // Warns when a culture-aware 'string.CompareTo' call is used by default
					if (c1.CompareTo (c2) < 0)
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
		public Classification<UnsignedPoint,TLabel> Clusters { get; private set; }

		/// <summary>
		/// The same points as are found in the Clusters, sorted according to a Hilbert curve or other
		/// locality-preserving ordering.
		/// </summary>
		/// <value>The sorted points.</value>
		public List<UnsignedPoint> SortedPoints { get; private set; }

		/// <summary>
		/// Initializes a new instance of the PolyChromaticClosestPoint class
		/// and sorting the points according to the default Hilbert curve.
		/// </summary>
		/// <param name="clusters">Classifies the points into distinct clusters.</param>
		public PolyChromaticClosestPoint (Classification<UnsignedPoint,TLabel> clusters)
		{
			Clusters = clusters;
			SortPoints ();
		}

		/// <summary>
		/// Sorts the points according to their position in a Hilbert curve.
		/// </summary>
		private void SortPoints() 
		{
			var maxValue = Clusters.Points ().SelectMany (p => p.Coordinates).Max ();
			var bitsPerDimension = ((int)maxValue + 1).SmallestPowerOfTwo ();
			var index = new Dictionary<HilbertPoint, UnsignedPoint> ();
			var hPoints = new List<HilbertPoint> ();
			foreach (UnsignedPoint p in Clusters.Points()) 
			{
				var hp = new HilbertPoint (p.Coordinates, bitsPerDimension);
				index [hp] = p;
				hPoints.Add (hp);
			}
			hPoints.Sort ();
			SortedPoints = hPoints.Select (hp => index [hp]).ToList ();
		}

		/// <summary>
		/// Initializes a new instance of the PolyChromaticClosestPoint class
		/// with the supplied ordering of points.
		/// </summary>
		/// <param name="clusters">Classifies the points into distinct clusters.</param>
		/// <param name="sortedPoints">Points sorted according a locality-preserving ordering like a Hilbert curve.
		/// Every point in clusters must also be found in sortedPoints.</param>
		public PolyChromaticClosestPoint (Classification<UnsignedPoint,TLabel> clusters, IEnumerable<UnsignedPoint> sortedPoints)
		{
			Clusters = clusters;
			SortedPoints = sortedPoints as List<UnsignedPoint> ?? sortedPoints.ToList ();
		}

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
			foreach(var p1 in Clusters.PointsInClass(color1))
				foreach(var p2 in Clusters.PointsInClass(color2))
				{
					var d = p1.Measure (p2);
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
		/// Finds an approximately closest pair of points (one of each color) by using the ordering found in SortedPoints.
		/// 
		/// This compares points in two of the clusters, ignoring points in all other clusters.
		/// </summary>
		/// <param name="color1">Label of the first cluster.</param>
		/// <param name="color2">Label of the second cluster.</param>
		/// <returns>The point with Color1, the point with Color2 and the square distance between them.
		/// </returns>
		public ClosestPair FindPairApproximately(TLabel color1, TLabel color2)
		{
			var shortest = new ClosestPair();
			UnsignedPoint prevP = null;
			TLabel prevColor = default(TLabel);
			foreach(var pc in SortedPoints
				.Select(p => new { Point = p, Color = Clusters.GetClassLabel(p) })
				.Where(pc => pc.Color.Equals(color1) || pc.Color.Equals(color2)))
			{
				if (prevP != null && !prevColor.Equals(pc.Color)) 
				{
					var d = pc.Point.Measure (prevP);
					if (d < shortest.SquareDistance) 
					{
						shortest.SquareDistance = d;
						shortest.Color1 = prevColor;
						shortest.Point1 = prevP;
						shortest.Color2 = pc.Color;
						shortest.Point2 = pc.Point;
					}
				}
				prevP = pc.Point;
				prevColor = pc.Color;
			}
			return shortest.Swap(color1);
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
			var points1 = Clusters.PointsInClass (color1);
			var points2 = Clusters.PointsInClass (color2);
			var c1 = UnsignedPoint.Centroid(points1);
			var p2 = points2
				.OrderBy (p => c1.Measure(p))
				.First ()
			;
			var closest = points1.Select (p1 => new ClosestPair (color1, p1, color2, p2, p1.Measure (p2))).OrderBy (cp => cp.SquareDistance).First ();
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
			foreach(var p1 in Clusters.PointsInClass(color1))
				foreach(var pc in Clusters.Points()
					.Select(p => new { Point = p, Color = Clusters.GetClassLabel(p) })
					.Where(pc => !color1.Equals(pc.Color)))
				{
					var d = p1.Measure (pc.Point);
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

		/// <summary>
		/// Finds approximately the cluster nearest to the given cluster.
		/// </summary>
		/// <param name="color1">Identifies the cluster being queried.</param>
		/// <returns>The closest cluster and the points from each cluster that were closest.</returns>
		public ClosestPair FindClusterApproximately(TLabel color1)
		{
			var shortest = new ClosestPair();
			var clusterAtPosition = new TLabel[SortedPoints.Count];

			int segmentStart = 0;
			int segmentEnd = -1;
			for (var i = 0; i <= SortedPoints.Count; i++) 
			{
				TLabel currColor;
				if (i < SortedPoints.Count)
					clusterAtPosition [i] = currColor = Clusters.GetClassLabel (SortedPoints [i]);
				else
					currColor = color1;
				var currColorMatches = currColor.Equals (color1);
				if (!currColorMatches) 
				{
					// Extend the contiguous segment of points that are not color1.
					segmentEnd = i;
				} 
				else
				{
					// A contiguous segment of points that are not color1 is sandwiched between 
					// two points that are color1, except possibly at the beginning and end of the list of SortedPoints.
					// Iterate both forwrd and backward through the points in the segment. 
					// If multiple points from the same second color are found in the segment,
					// only compare the nearest point in sequence to each end of the segment.
					// For example, if the sequence of colors (1,2,3) for points A thru J is this:
					//     Points:  A B C D E F G H I J
					//     Colors:  1 2 2 2 3 3 3 2 2 1
					// We will compare the distances between A & B, A & E, G & J, and I & J,
					// because they are likeliest to be the closest points to one or the other endpoints (A & J).
					// Thus at most one point of each color found in the segment will be compared to the
					// first point of color1, and at most one point of each color will be compared to the last
					// point of color1.

					var clustersSeen = new HashSet<TLabel> ();
					if (segmentStart > 0 && segmentEnd >= segmentStart && segmentEnd < SortedPoints.Count - 1) 
					{
						var p1 = SortedPoints [segmentStart - 1];
						for (var j = segmentStart; j <= segmentEnd; j++)
						{
							var color2 = clusterAtPosition [j];
							if (!clustersSeen.Contains (color2)) 
							{
								var p2 = SortedPoints [j];
								var d = p1.Measure (p2);
								if (shortest.SquareDistance > d) 
								{
									shortest.SquareDistance = d;
									shortest.Color1 = color1;
									shortest.Point1 = p1;
									shortest.Color2 = color2;
									shortest.Point2 = p2;
								}
								clustersSeen.Add (color2);
							}
						}
					}

					clustersSeen = new HashSet<TLabel> ();
					if (segmentStart >= 0 && segmentEnd < SortedPoints.Count - 1) 
					{
						var p1 = SortedPoints [segmentEnd + 1];
						for (var j = segmentEnd; j >= segmentStart; j--)
						{
							var color2 = clusterAtPosition [j];
							if (!clustersSeen.Contains (color2)) 
							{
								var p2 = SortedPoints [j];
								var d = p1.Measure (p2);
								if (shortest.SquareDistance > d) 
								{
									shortest.SquareDistance = d;
									shortest.Color1 = color1;
									shortest.Point1 = p1;
									shortest.Color2 = color2;
									shortest.Point2 = p2;
								}
								clustersSeen.Add (color2);
							}
						}
					}

					segmentStart = i + 1;
					segmentEnd = -1;
				}
			}

			return shortest.Swap(color1);
		}

		/// <summary>
		/// Less efficient way to do the same thing as FindClusterApproximately.
		/// </summary>
		/// <param name="color1">Cluster id for the first cluster to compare.</param>
		/// <returns>Results that identify which other cluster is closest to the given cluster.</returns>
		public ClosestPair FindClusterIteratively(TLabel color1)
		{
			var shortestPair = new ClosestPair ();
			foreach(var color2 in Clusters.ClassLabels().Where(c => !c.Equals(color1)))
			{
				var closestForColor = FindPairApproximately(color1, color2);
				if (shortestPair.SquareDistance > closestForColor.SquareDistance)
					shortestPair = closestForColor;
			}

			return shortestPair;
		}

		/// <summary>
		/// Approximates the closest distance between every cluster and every other cluster.
		/// 
		/// If there are currently K clusters, this will return at most K(K-1)/2 ClosestPairs, unsorted.
		/// If an upper limit on the square distance is supplied, fewer may be returned.
		/// </summary>
		/// <param name="maxSquareDistance">If omitted, no restriction on distance is applied.
		/// If supplied, no measurement of the closest distance between two colors will
		/// be returned if those two colors are farther apart than this distance.
		/// </param>
		/// <returns>ClosestPairs for every pair of colors, unsorted. 
		/// If a distance is returned for colors "A" and "B", one will not be returned for colors "B" and "A",
		/// since the distance is symmetric.</returns>
		public IEnumerable<ClosestPair> FindAllClustersApproximately(long maxSquareDistance = long.MaxValue)
		{
			var shortList = new List<UnsignedPoint> ();
			shortList.Add (SortedPoints [0]);
			var prevClass = Clusters.GetClassLabel (SortedPoints [0]);
			var currClass = Clusters.GetClassLabel (SortedPoints [1]);

			// shortList will have all the SortedPoints with some removed.
			// If a point is in the same class as both its predecessor and successor, remove it. 
			// These are more likely to be interior points in a cluster, and not border points.
			// The likely number of points that survive elimination is 2*F*K, 
			// where K is the current number of clusters and F is the degree of cluster fragmentation.
			// If all the points in  cluster are contiguous in SortedPoints, the fragmentation of that cluster is one.
			// Given the twistiness of a Hilbert curve (or any fractal, space-filling curve), it is common
			// for a cluster to be fragmented into from 2 to 10 parts, so F ranges from 2 to 10, with 4 or 5 being most common.
			//
			// Assume that an initial pass of clustering has already been performed and it
			// created several partial clusters for every ideal cluster.
			for (var iPoint = 1; iPoint < SortedPoints.Count - 1; iPoint++) 
			{
				var nextClass = Clusters.GetClassLabel (SortedPoints [iPoint + 1]);
				if (!currClass.Equals (prevClass) || !currClass.Equals (nextClass)) 
					shortList.Add (SortedPoints[iPoint]);
				prevClass = currClass;
				currClass = nextClass;
			}
			shortList.Add (SortedPoints.Last ());

			var shortestDistances = new Dictionary<string, ClosestPair> ();
			for (var i1 = 0; i1 < shortList.Count - 1; i1++)
			{
				var point1 = shortList [i1];
				var color1 = Clusters.GetClassLabel (point1);
				var colorsAlreadySeen = new HashSet<TLabel> ();
				foreach (var pair in shortList
					.Skip(i1+1)
					.Select(p => new { point2 = p, color2 = Clusters.GetClassLabel (p) })
					.TakeWhile(pair => !pair.color2.Equals(color1)) )
				{
					if (!colorsAlreadySeen.Contains (pair.color2))
					{
						var potentialClosest = new ClosestPair (color1, point1, pair.color2, pair.point2);
						if (potentialClosest.SquareDistance > maxSquareDistance)
							continue;
						ClosestPair currentClosest;
						var key = potentialClosest.Key;
						if (!shortestDistances.TryGetValue (key, out currentClosest)) 
							shortestDistances [key] = potentialClosest;
						else if (currentClosest.CompareTo(potentialClosest) > 0)
							shortestDistances [key] = potentialClosest;
						colorsAlreadySeen.Add (pair.color2);
					}
				}
			}
			return shortestDistances.Values;
		}
			
	}
}

