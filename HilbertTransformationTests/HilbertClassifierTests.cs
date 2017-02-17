using System;
using System.Diagnostics;
using Clustering;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class HilbertClassifierTests
	{
		[Test]
		/// <summary>
		/// Classifies random data with 10,000 points, 30 clusters, 100 dimensions.
		/// </summary>
		public void Classify_10000N_30K_100D()
		{
			ClassifyCase(10000, 30, 100);
		}

		[Test]
		/// <summary>
		/// Classifies random data with 20,000 points, 50 clusters, 100 dimensions.
		/// </summary>
		public void Classify_20000N_50K_100D()
		{
			ClassifyCase(20000, 50, 100);
		}

		[Test]
		/// <summary>
		/// Classifies random data with 50,000 points, 100 clusters, 50 dimensions.
		/// </summary>
		public void Classify_50000N_100K_50D()
		{
			ClassifyCase(50000, 100, 50);
		}

		/// <summary>
		/// Perform classifications for many sizes of problem and record the timings.
		/// </summary>
		/// <remarks>
		/*
N,K,D,B,Seconds
10000,25,25,10,0.828
10000,25,25,15,0.568
10000,25,25,20,0.814
10000,25,50,10,1.292
10000,25,50,15,0.97
10000,25,50,20,2.404
10000,25,100,10,2.278
10000,25,100,15,1.947
10000,25,100,20,2.261
10000,25,200,10,2.392
10000,25,200,15,2.407
10000,25,200,20,3.783
10000,25,400,10,7.088
10000,25,400,15,4.845
10000,25,400,20,8.203
10000,50,25,10,0.515
10000,50,25,15,1.14
10000,50,25,20,0.643
10000,50,50,10,0.668
10000,50,50,15,1.518
10000,50,50,20,1.02
10000,50,100,10,0.871
10000,50,100,15,2.454
10000,50,100,20,1.819
10000,50,200,10,1.503
10000,50,200,15,5.118
10000,50,200,20,3.157
10000,50,400,10,3.297
10000,50,400,15,4.924
10000,50,400,20,9.63
10000,100,25,10,0.636
10000,100,25,15,0.892
10000,100,25,20,0.792
10000,100,50,10,0.899
10000,100,50,15,0.641
10000,100,50,20,1.079
10000,100,100,10,3.122
10000,100,100,15,2.156
10000,100,100,20,3.406
10000,100,200,10,2.691
10000,100,200,15,4.704
10000,100,200,20,3.062
10000,100,400,10,4.467
10000,100,400,15,5.792
10000,100,400,20,5.142
10000,200,25,10,1.11
10000,200,25,15,1.194
10000,200,25,20,1.311
10000,200,50,10,1.228
10000,200,50,15,1.653
10000,200,50,20,1.226
10000,200,100,10,3.937
10000,200,100,15,1.57
10000,200,100,20,2.083
10000,200,200,10,5.114
10000,200,200,15,7.075
10000,200,200,20,4.368
10000,200,400,10,7.878
10000,200,400,15,6.64
10000,200,400,20,9
10000,400,25,10,1.655
10000,400,25,15,1.772
10000,400,25,20,1.695
10000,400,50,10,1.629
10000,400,50,15,2.645
10000,400,50,20,2.837
10000,400,100,10,2.761
10000,400,100,15,3.463
10000,400,100,20,4.08
10000,400,200,10,2.785
10000,400,200,15,3.036
10000,400,200,20,7.613
10000,400,400,10,6.811
10000,400,400,15,8.192
10000,400,400,20,10.842
20000,25,25,10,1.833
20000,25,25,15,1.806
20000,25,25,20,1.749
20000,25,50,10,3.023
20000,25,50,15,3.267
20000,25,50,20,4.608
20000,25,100,10,3.304
20000,25,100,15,6.111
20000,25,100,20,7.547
20000,25,200,10,7.259
20000,25,200,15,8.313
20000,25,200,20,11.957
20000,25,400,10,7.612
20000,25,400,15,11.059
20000,25,400,20,25.19
20000,50,25,10,1.708
20000,50,25,15,1.307
20000,50,25,20,2.063
20000,50,50,10,3.062
20000,50,50,15,2.158
20000,50,50,20,4.143
20000,50,100,10,3.687
20000,50,100,15,3.996
20000,50,100,20,4.59
20000,50,200,10,4.227
20000,50,200,15,4.868
20000,50,200,20,7.114
20000,50,400,10,9.171
20000,50,400,15,10.009
20000,50,400,20,8.742
20000,100,25,10,1.584
20000,100,25,15,1.131
20000,100,25,20,2.627
20000,100,50,10,2.55
20000,100,50,15,4.117
20000,100,50,20,3.653
20000,100,100,10,4.035
20000,100,100,15,6.5
20000,100,100,20,2.782
20000,100,200,10,8.571
20000,100,200,15,7.246
20000,100,200,20,8.535
20000,100,400,10,9.006
20000,100,400,15,9.096
20000,100,400,20,16.247
20000,200,25,10,2.562
20000,200,25,15,2.378
20000,200,25,20,1.89
20000,200,50,10,4.051
20000,200,50,15,2.299
20000,200,50,20,4.264
20000,200,100,10,5.87
20000,200,100,15,3.008
20000,200,100,20,9.162
20000,200,200,10,7.919
20000,200,200,15,12.939
20000,200,200,20,11.175
20000,200,400,10,16.882
20000,200,400,15,17.237
20000,200,400,20,11.421
20000,400,25,10,1.834
20000,400,25,15,3.145
20000,400,25,20,3.547
20000,400,50,10,3.577
20000,400,50,15,4.949
20000,400,50,20,3.456
20000,400,100,10,4.442
20000,400,100,15,4.698
20000,400,100,20,7.803
20000,400,200,10,9.081
20000,400,200,15,11.32
20000,400,200,20,11.004
20000,400,400,10,17.964
20000,400,400,15,11.745
20000,400,400,20,19.42
50000,25,25,10,3.637
50000,25,25,15,2.402
50000,25,25,20,5.291
50000,25,50,10,5.555
50000,25,50,15,3.224
50000,25,50,20,9.893
50000,25,100,10,11.204
50000,25,100,15,7.651
50000,25,100,20,7.925
50000,25,200,10,13.956
50000,25,200,15,15.408
50000,25,200,20,14.807
50000,25,400,10,29.085
50000,25,400,15,26.927
50000,25,400,20,40.165
50000,50,25,10,4.814
50000,50,25,15,3.844
50000,50,25,20,6.733
50000,50,50,10,11.411
50000,50,50,15,8.132
50000,50,50,20,9.329
50000,50,100,10,8.35
50000,50,100,15,22.891
50000,50,100,20,12.515
50000,50,200,10,20.18
50000,50,200,15,17.393
50000,50,200,20,15.707

*/
		/// </remarks>
		[Test]
		public void ClassifyManyWithTimings()
		{
			var N = new[] { 10000, 20000, 50000, 100000 };
			var K = new[] { 25, 50, 100, 200, 400 };
			var Dims = new[] { 25, 50, 100, 200, 400 };
			var Bits = new[] { 10,15,20 };
			ClassifyMany(N, K, Dims, Bits);
		}

		[Test]
		public void ClassifyManyVaryingBits()
		{
			/* Results:
N,K,D,B,Seconds
50000,100,100,10,12.048
50000,100,100,11,10.434
50000,100,100,12,18.828
50000,100,100,13,10.374
50000,100,100,14,8.623
50000,100,100,15,6.655
50000,100,100,16,10.111
50000,100,100,17,10.881
50000,100,100,18,19.897
50000,100,100,19,16.08
50000,100,100,20,9.26
			 */
			var N = new[] { 50000 };
			var K = new[] { 100 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
			ClassifyMany(N, K, Dims, Bits);
		}

		[Test]
		public void ClassifyManyVaryingDims()
		{
			/* Results:
N,K,D,B,Seconds
20000,100,10,10,1.123
20000,100,20,10,1.22
20000,100,30,10,2.547
20000,100,40,10,4.128
20000,100,50,10,2.35
20000,100,100,10,4.84
20000,100,200,10,6.046
20000,100,400,10,24.478
			 */
			var N = new[] { 20000 };
			var K = new[] { 100 };
			var Dims = new[] { 10, 20, 30, 40, 50, 100, 200, 400 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits);
		}

		[Test]
		public void ClassifyManyVaryingClusters()
		{
			/* Results:
N,K,D,B,Seconds
40000,10,100,10,6.198
40000,20,100,10,9
40000,30,100,10,9.572
40000,40,100,10,8.145
40000,50,100,10,14.072
40000,100,100,10,12.072
40000,200,100,10,8.319
40000,400,100,10,9.297
			 */
			var N = new[] { 40000 };
			var K = new[] { 10, 20, 30, 40, 50, 100, 200, 400 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits);
		}

		[Test]
		public void ClassifyManyVaryingNumPoints()
		{
			/* Results:
N,K,D,B,Seconds
2500,100,100,10,0.46525
5000,100,100,10,0.84025
10000,100,100,10,1.67375
25000,100,100,10,4.38575
50000,100,100,10,12.204
100000,100,100,10,21.417
200000,100,100,10,96.0385  .... Has to throttle down to a single thread to avoid out-of-memory
			 */
			var N = new[] { 2500, 5000, 10000, 25000, 50000, 100000, 200000 };
			var K = new[] { 100 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits, 4);
		}

		/// <summary>
		/// This failing case probably runs out of memory. Trying to reduce memory usage.
		/// </summary>
		[Test]
		public void ClassifyManyOutOfMemory()
		{
			var N = new[] { 200000 };
			var K = new[] { 100 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits, 4);
		}

		/// <summary>
		/// Run many classifications over random test data and record the average timings.
		/// </summary>
		/// <param name="N">Variation in Number of points to classify.</param>
		/// <param name="K">Variation in Number of Clusters.</param>
		/// <param name="Dims">Variation in number of Dimensions per point.</param>
		/// <param name="Bits">Variation in number of Bits needed to represent each coordinate value.</param>
		/// <param name="repeat">Number of times to repeat each test. The average execution time is recorded.</param>
		private void ClassifyMany(int[] N, int[] K, int[] Dims, int[] Bits, int repeat = 1)
		{
			var report = "N,K,D,B,Seconds\n";
			Console.WriteLine("N,K,D,B,Seconds");
			foreach (var n in N)
				foreach (var k in K)
				{
					foreach (var d in Dims)
						foreach (var bits in Bits)
						{
							var max = (1 << bits) - 1;
							var seconds = 0.0;
							for (var iteration = 0; iteration < repeat; iteration++)
								seconds += ClassifyPerformance(n, k, d, 0, max);
							seconds /= repeat;
							var record = $"{n},{k},{d},{bits},{seconds}\n";
							report += record;
							Console.WriteLine(record);
						}
					Console.WriteLine(report);
				}
			Console.WriteLine("Done! Final report:");
			Console.WriteLine(report);
		}

		/// <summary>
		/// Create test data in known clusters, perform unattended clustering, and compare the results to the known clusters.
		/// The test passes if the BCubed value is high enough.
		/// </summary>
		/// <param name="numPoints">Number of points to cluster.</param>
		/// <param name="clusterCount">Cluster count.</param>
		/// <param name="dimensions">Dimensions per point.</param>
		/// <param name="clusterSizeVariation">Cluster size variation.
		///  The average number of points per cluster is numPoints/clusterCount. 
		///  The actual size of a given cluster will be permitted to vary by as much as ± clusterSizeVariation.
		/// </param>
		/// <param name="maxCoordinate">All points will have coordinate values in the range 0 to maxCoordinate.</param>
		/// <param name="acceptableBCubed">The comparison of the actual and expected clusters must yield a BCubed value
		/// that is this high or higher. A value of 1.0 means a perfect clustering, with no points out of place.</param>
		private void ClassifyCase(int numPoints, int clusterCount, int dimensions, 
		                          int clusterSizeVariation = 0, int maxCoordinate = 1000, double acceptableBCubed = 0.99)
		{
			var minClusterSize = (numPoints / clusterCount) - clusterSizeVariation;
			var maxClusterSize = (numPoints / clusterCount) + clusterSizeVariation;
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = maxCoordinate,
				MinClusterSize = minClusterSize,
				MaxClusterSize = maxClusterSize
			};
			var expectedClusters = data.MakeClusters();
			var classifier = new HilbertClassifier(expectedClusters.Points(), 10);
			var actualClusters = classifier.Classify();
			var comparison = expectedClusters.Compare(actualClusters);

			var message = $"   Comparison of clusters: {comparison}.\n   Clusters expected/actual: {expectedClusters.NumPartitions}/{actualClusters.NumPartitions}.";
			Console.WriteLine(message);
			Console.WriteLine($"   Large clusters: {actualClusters.NumLargePartitions(classifier.OutlierSize)}");
			Assert.GreaterOrEqual(comparison.BCubed, acceptableBCubed, $"Clustering was not good enough. BCubed = {comparison.BCubed}"); 
		}

		/// <summary>
		/// Create test data in known clusters, perform unattended clustering, time the process.
		/// Make no attempt to verify the correctness of the result.
		/// The timing does not include the creation of the test data, just the clustering.
		/// </summary>
		/// <param name="numPoints">Number of points to cluster.</param>
		/// <param name="clusterCount">Cluster count.</param>
		/// <param name="dimensions">Dimensions per point.</param>
		/// <param name="clusterSizeVariation">Cluster size variation.
		///  The average number of points per cluster is numPoints/clusterCount. 
		///  The actual size of a given cluster will be permitted to vary by as much as ± clusterSizeVariation.
		/// </param>
		/// <param name="maxCoordinate">All points will have coordinate values in the range 0 to maxCoordinate.</param>
		/// <returns>Time in seconds.</returns>
		private double ClassifyPerformance(int numPoints, int clusterCount, int dimensions,
								  int clusterSizeVariation = 0, int maxCoordinate = 1000)
		{
			var minClusterSize = (numPoints / clusterCount) - clusterSizeVariation;
			var maxClusterSize = (numPoints / clusterCount) + clusterSizeVariation;
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = maxCoordinate,
				MinClusterSize = minClusterSize,
				MaxClusterSize = maxClusterSize
			};
			var expectedClusters = data.MakeClusters();
			var timer = new Stopwatch();
			timer.Start();
			var classifier = new HilbertClassifier(expectedClusters.Points(), 10);
			var actualClusters = classifier.Classify();
			timer.Stop();
			if (actualClusters.NumPartitions != expectedClusters.NumPartitions)
				Console.WriteLine($"# of Clusters actual/expected: {actualClusters.NumPartitions}/{expectedClusters.NumPartitions}");
			var seconds = timer.ElapsedMilliseconds / 1000.0;
			return seconds;
		}
	}
}
