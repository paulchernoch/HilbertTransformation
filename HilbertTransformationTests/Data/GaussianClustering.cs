using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using HilbertTransformation.Random;
using HilbertTransformation;
using Clustering;

namespace HilbertTransformationTests.Data
{
	/// <summary>
	/// Create a set of clustered test data consisting of a specified number of clusters of varying sizes.
	/// Each cluster is shaped like an ellipsoid with each dimension having a separate standard deviation for its radius.
	/// The clusters are spaced out so that none are likely to overlap.
	/// </summary>
	public class GaussianClustering
	{
		#region Properties that affect how the random data is created

		/// <summary>
		/// This many distinct clusters of points will be created.
		/// </summary>
		public int ClusterCount = 20;
		/// <summary>
		/// All points will have this many dimensions.
		/// </summary>
		public int Dimensions = 1000;
		public int MaxDistanceStdDev = 30;
		public int MinDistanceStdDev = 10;
		/// <summary>
		/// No cluster will have more than this number of points (excluding noise).
		/// </summary>
		public int MaxClusterSize = 2000;

		/// <summary>
		/// No cluster will have fewer than this number of points (excluding noise).
		/// </summary>
		public int MinClusterSize = 100;

		/// <summary>
		/// The percentage of uniform randomly distributed extra points added to the Gaussian data to confuse the clustering algorithm. 
		/// </summary>
		public double NoisePercentage;

		/// <summary>
		/// All coordinate values in all points will fall between zero and this value, inclusive.
		/// </summary>
		public int MaxCoordinate = 100;

		/// <summary>
		/// Used to generate random numbers.
		/// </summary>
		public FastRandom r;

		#endregion

		#region Properties that affect subsequent clustering


		public int NumberOfHilbertIndices = 1;
		public bool OnlyUseHilbertCurve;
		public bool UseDimensionSelector;

		/// <summary>
		/// After the initial phase of clustering, any cluster with this many members or fewer is considered an outlier, and handled specially.
		/// </summary>
		public int OutlierSize;

		/// <summary>
		/// Passed to SingleHilbertClassifier.BilinearDecline to determine what percentage of potential merges at the median distance between points
		/// will be accepted.
		/// 
		/// If the data has no noise, this should be 100%.
		/// If the data has noise, adjust this to prevent merges that tend to join clusters that oughjt to remain separate.
		/// </summary>
		public double MergeAcceptancePercentage = 100;

		/// <summary>
		/// Used as a factor in the AcceptRejectedMerge delegate. Must be greater than one.
		/// The larger the value, the more merges are prevented.
		/// For example, if the value is ten, one cluster must be at least ten times as large as the other if they are to be merged.
		/// The more noise in the data, the larger this should be.
		/// </summary>
		public double MergeAcceptanceRatio = 10;

		/// <summary>
		/// Gets or sets the connectedness score percentile that indicates whether a cluster is well-connected to its neighbors.
		/// If the score percentile is higher than this, the cluster is well-connected.
		/// </summary>
		public int WellConnectedPercentile { get; set; }

		/// <summary>
		/// Minumum number of points in one cluster that must be within a certain distance of a like number of points in a second cluster
		/// in order to permit them to be merged.
		/// 
		/// This only applies to one of the final merge phases.
		/// </summary>
		/// <value>The minimum number of nearby points that must exist in each of two clusters being merged.</value>
		public int MinimumConnections { get; set; }

		#endregion

		#region The noise

		public HashSet<UnsignedPoint> Noise { get; set; }


		#endregion

		public GaussianClustering()
		{
			Noise = new HashSet<UnsignedPoint> ();
			WellConnectedPercentile = 20;
			MinimumConnections = 4;
		}


		/// <summary>
		/// Generate random points clumped into individual, well-separated, Gaussian clusters with optional uniform noise added.
		/// 
		/// </summary>
		/// <returns>Points that are grouped into clusters and stored in a Classification.</returns>
		public Classification<UnsignedPoint, string> MakeClusters()
		{
			var clusters = new Classification<UnsignedPoint, string>();
			r = new FastRandom();
			//var z = new ZigguratGaussianSampler();
			var farthestDistanceFromClusterCenter = 0.0;

			var minDistance = EllipsoidalGenerator.MinimumSeparation(MaxDistanceStdDev, Dimensions);
			var centerGenerator = new DiffuseGenerator(Dimensions, minDistance)
			{
				// Keep the centers of the clusters away from the edge, so that points do not go out of bounds and have their coordinates truncated.
				Minimum = MaxDistanceStdDev,
				Maximum = MaxCoordinate - MaxDistanceStdDev
			};
			var iCluster = 0;
			var clusterCenters = new Dictionary<string, UnsignedPoint> ();
			foreach (var clusterCenter in centerGenerator.Take(ClusterCount).Where(ctr => ctr != null))
			{
				var centerPoint = new UnsignedPoint(clusterCenter);
				var clusterSize = r.Next(MinClusterSize, MaxClusterSize);
				var pointGenerator = new EllipsoidalGenerator(clusterCenter, RandomDoubles(Dimensions, MinDistanceStdDev, MaxDistanceStdDev, r), Dimensions);
				var clusterId = iCluster.ToString();
				foreach (var iPoint in Enumerable.Range(1, clusterSize))
				{
					UnsignedPoint p;
					clusters.Add(
						p = new UnsignedPoint(pointGenerator.Generate(new int[Dimensions])),
						clusterId
					);
					var distance = Math.Sqrt(centerPoint.Measure(p));
					farthestDistanceFromClusterCenter = Math.Max(farthestDistanceFromClusterCenter, distance);
				}
				clusterCenters[clusterId] = centerPoint;
				iCluster++;
			}
			AddNoise ((int) Math.Floor(clusters.NumPoints * NoisePercentage / 100), clusterCenters, clusters);
			Debug.WriteLine("Test data: Farthest Distance from center = {0:N2}. Minimum Distance Permitted between Clusters = {1:N2}. Max Standard Deviation = {2}", 
				farthestDistanceFromClusterCenter, 
				minDistance,
				MaxDistanceStdDev
			);
			return clusters;
		}

		/// <summary>
		/// Add noise points to the data and classify each noise point with the nearest cluster center.
		/// </summary>
		/// <param name="noisePointsToAdd">Number of noise points to add.</param>
		/// <param name="clusterCenters">Cluster centers for each cluster, where the key is the cluster id.</param>
		/// <param name="clusters">The noise points will be added to these clusters.</param>
		private void AddNoise(int noisePointsToAdd, Dictionary<string, UnsignedPoint> clusterCenters, Classification<UnsignedPoint, string> clusters)
		{
			if (noisePointsToAdd <= 0)
				return;
			var pccp = new PolyChromaticClosestPoint<string> (clusters);
			var closest = new List<Tuple<String,String>> ();
			// Find the nearest neighboring cluster to each cluster.
			// We will be choosing random noise points positioned in the space between clusters that are near neighbors.
			foreach (var clusterId in clusters.ClassLabels()) 
			{
				var cp = pccp.FindClusterApproximately (clusterId).Swap(clusterId);
				closest.Add(new Tuple<string,string>(cp.Color1, cp.Color2));
			}

			// We need to pick random points from each cluster, so must convert from Sets to Lists for performance.
			var clustersAsLists = new Dictionary<string, List<UnsignedPoint>> ();
			foreach (var pair in clusters.LabelToPoints) 
				clustersAsLists [pair.Key] = pair.Value.ToList ();	

			// Pick random pairs of clusters that are close neighbors.
			// Then pick a random point from each cluster and compute a weighted average of the two points.
			// This will construct noise points that tend to form a filament between two clusters.
			// Such connecting filaments pose the greatest likelihood of merging two distinct
			// clusters into one, the very error that must be compensated for by an improved algorithm.
			for (var i = 0; i < noisePointsToAdd; i++) 
			{
				var whereToAdd = closest [r.Next (closest.Count)];
				// The weight will range from 0.18 to 0.82 so as to keep most noise points from being inside a cluster,
				// which would make them non-noisy.
				var weight1 = r.NextDouble() * 0.64 + 0.18;
				var weight2 = 1.0 - weight1;
				var c1 = clustersAsLists[whereToAdd.Item1];
				var c2 = clustersAsLists[whereToAdd.Item2];
				var p1 = c1[r.Next(c1.Count)];
				var p2 = c2[r.Next(c2.Count)];
				var vRandom = new int[Dimensions];
				for (var iDim = 0; iDim < vRandom.Length; iDim++)
					vRandom [iDim] = (int)(weight1 * p1.Coordinates [iDim] + weight2 * p2.Coordinates [iDim]);
				var pRandom = new UnsignedPoint (vRandom);
				var d1 = c1.Select(p => pRandom.Measure(p)).Min();
				var d2 = c2.Select(p => pRandom.Measure(p)).Min();
				var cRandom = d1 < d2 ? whereToAdd.Item1 : whereToAdd.Item2;
				clusters.Add(pRandom, cRandom);
				Noise.Add (pRandom);
			}
		}

		/// <summary>
		/// Create an array of random doubles with values chosen uniformly between min (inclusive) and max (exclusive).
		/// </summary>
		/// <param name="size">Size of array created.</param>
		/// <param name="min">Lowest value that may be used in the returned array (inclusive).</param>
		/// <param name="max">Highest value that may be used in the returned array (exclusive).</param>
		/// <param name="r">If null, create a new random number genreator to use, otherwise use the supplied generator.</param>
		/// <returns>Array of random doubles.</returns>
		private static double[] RandomDoubles(int size, double min, double max, FastRandom r = null)
		{
			r = r ?? new FastRandom();
			var a = new double[size];
			for (var i = 0; i < size; i++)
				a[i] = min + (max - min) * r.NextDouble();
			return a;
		}
	}
}

