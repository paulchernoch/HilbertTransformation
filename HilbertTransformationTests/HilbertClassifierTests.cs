using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class HilbertClassifierTests
	{

        [Test]
        /// <summary>
        /// Classifies random data with 100,000 points, 100 clusters, 1,000 dimensions - a large case.
        /// </summary>
        public void Classify_100000N_100K_1000D()
        {
            ClassifyCase(100000, 100, 1000);
        }

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
		/// Test whether a wide range of cluster densities in the same population can be handled by the clusterer.
		/// The sparsest and densest clusters have densities that differs by a factor of fifty.
		/// 
		/// The clusters have a continuous range of sizes (and hence densities), from 100 points to 5000 points.
		/// </summary>
		[Test]
		public void Classify_DensitySpread()
		{
			var clusterCount = 50;
			var dimensions = 100;
			var maxCoordinate = 1000;
			var acceptableBCubed = 0.99;
			var clusterSizes = new int[50];
			foreach (var i in Enumerable.Range(0, 50))
				clusterSizes[i] = 100 + (100 * i);
			
			var minClusterSize = clusterSizes.Min();
			var maxClusterSize = clusterSizes.Max();
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = maxCoordinate,
				MinClusterSize = minClusterSize,
				MaxClusterSize = maxClusterSize,
				ClusterSizes = clusterSizes
			};

			ClusterCore(data, acceptableBCubed);
		}

		private void ClusterCore(GaussianClustering data, double acceptableBCubed)
		{
			var expectedClusters = data.MakeClusters();
			var classifier = new HilbertClassifier(expectedClusters.Points(), 10);

			classifier.IndexConfig.UseSample = true;

			var actualClusters = classifier.Classify();
			var comparison = expectedClusters.Compare(actualClusters);

			var message = $"   Comparison of clusters: {comparison}.\n   Clusters expected/actual: {expectedClusters.NumPartitions}/{actualClusters.NumPartitions}.";
			Console.WriteLine(message);
			Console.WriteLine($"   Large clusters: {actualClusters.NumLargePartitions(classifier.OutlierSize)}");
			Assert.GreaterOrEqual(comparison.BCubed, acceptableBCubed, $"Clustering was not good enough. BCubed = {comparison.BCubed}");

		}


		/// <summary>
		/// Classifies a dataset from a university in Finland.
		/// See https://cs.joensuu.fi/sipu/datasets/dim256.txt.
		/// </summary>
		[Test]
		public void Classify_Finnish_D256_N1024_K16()
		{
			ClassifyCase(Datasets.D256_N1024_K16_Classified());
		}

		[Test]
		/// <summary>
		/// Classifies random data with 5,000 points, two non-overlapping clusters, 100 dimensions.
		/// </summary>
		public void Classify_TwoClustersNoOverlap()
		{
			ClassifyTwoClustersCase(5000, 100, 0.0);
		}

		[Test]
		/// <summary>
		/// Classifies random data with 5,000 points, two 50% overlapping clusters, 100 dimensions.
		/// 
		/// NOTE: Fifty percent overlap just means that the clusters are separated by half of the computed safe distance.
		/// The safe distance is how far apart the clusters must be so that the chance of a point from one cluster being 
		/// near enough to another cluster to be misclassified is remote.
		/// </summary>
		public void Classify_TwoClusters50PctOverlap()
		{
			ClassifyTwoClustersCase(5000, 100, 50.0);
		}

		[Test]
		public void Classify_TwoClusters50PctOverlapLoop()
		{
			ClassifyTwoClustersReapeatedly(20, 5000, 100, 50.0, 1000);
		}

		[Test]
		public void Classify_TwoClusters60PctOverlapLoop()
		{
			ClassifyTwoClustersReapeatedly(20, 5000, 100, 60.0, 1000);
		}

		/// <summary>
		/// At 65% overlap, the center of one hypersphere falls on 
		/// the principal radius of the other sphere. The fact that 
		/// many of the cases cluster well is surprising.
		/// </summary>
		[Test]
		public void Classify_TwoClusters65PctOverlapLoop()
		{
			ClassifyTwoClustersReapeatedly(20, 5000, 100, 65.0, 1000);
		}

		[Test]
		public void Classify_TwoClusters70PctOverlapLoop()
		{
			ClassifyTwoClustersReapeatedly(20, 5000, 100, 70.0, 1000);
		}

		[Test]
		/// <summary>
		/// Classifies random data with 5,000 points, two 60% overlapping clusters, 100 dimensions.
		/// 
		/// NOTE: Sixty percent overlap just means that the clusters are separated by 40% of the computed safe distance.
		/// </summary>
		public void Classify_TwoClusters60PctOverlap()
		{
			ClassifyTwoClustersCase(5000, 100, 60.0);
		}

		[Test]
		/// <summary>
		/// Classifies random data with 5,000 points, two 75% overlapping clusters, 100 dimensions.
		/// 
		/// NOTE: 75 percent overlap just means that the clusters are seprated by one-quarter of the computed safe distance.
		/// </summary>
		public void Classify_TwoClusters75PctOverlap()
		{
			ClassifyTwoClustersCase(5000, 100, 75.0);
		}

		[Test]
		public void Classify_TwoClustersVaryingOverlap()
		{
			var overlapPercents = new[] { 0.0, 45.0, 50.0, 55.0, 60.0, 62.5, 65.0, 67.5, 70.0, 72.5, 75.0 }; 
			ClassifyTwoClustersReport(50, 5000, 100, overlapPercents);
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

		/// <summary>
		/// Keeping other parameters fixed, this varies the number of bits used to represent the data,
		/// which influences how many fractal iterations must be performed when computing the HilbertCurve.
		/// 
		/// The primary goal of this test is to print performance timings to the output window to gauge
		/// how the algorithm scales as the number of bits increases, but it also checks of the clustering actually worked.
		/// </summary>
		[Test]
		public void ClassifyManyVaryingBits()
		{
			/* Results:
					N,K,D,B,Seconds
					50000,100,100,10,21.5084
					50000,100,100,11,19.516
					50000,100,100,12,14.9362
					50000,100,100,13,20.7784
					50000,100,100,14,17.1494
					50000,100,100,15,16.2896
					50000,100,100,16,14.5064
					50000,100,100,17,18.7614
					50000,100,100,18,25.8042
					50000,100,100,19,26.8106
					50000,100,100,20,23.9874
			 */
			var N = new[] { 50000 };
			var K = new[] { 100 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
			ClassifyMany(N, K, Dims, Bits, 5);
		}

		[Test]
		public void ClassifyManyVaryingDims()
		{
			/* Results:
					N,K,D,B,Seconds
					20000,100,10,10,0.9814
					20000,100,20,10,1.6458
					20000,100,30,10,1.8714
					20000,100,40,10,2.9108
					20000,100,50,10,2.9744
					20000,100,100,10,4.4344
					20000,100,200,10,7.294
					20000,100,400,10,17.633
			 */
			var N = new[] { 20000 };
			var K = new[] { 100 };
			var Dims = new[] { 10, 20, 30, 40, 50, 100, 200, 400 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits, 5);
		}

		[Test]
		public void ClassifyManyVaryingClusters()
		{
			/* Results:
					N,K,D,B,Seconds
					40000,10,100,10,6.6948
					40000,20,100,10,7.5846
					40000,30,100,10,7.2118
					40000,40,100,10,10.1868
					40000,50,100,10,9.9472
					40000,100,100,10,9.8176
					40000,200,100,10,8.6836
					40000,400,100,10,8.7228
			 */
			var N = new[] { 40000 };
			var K = new[] { 10, 20, 30, 40, 50, 100, 200, 400 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits, 5);
		}

		[Test]
		public void ClassifyManyVaryingNumPoints()
		{
            /* Results, using OptimalIndex on a MAC:
					N,K,D,B,Seconds
					2500,100,100,10,0.3852
					5000,100,100,10,0.9512
					10000,100,100,10,2.1208
					25000,100,100,10,5.1676
					50000,100,100,10,13.7236   .... kept finding improved indices
					100000,100,100,10,39.3096  .... Throttled to 2 threads to avoid out-of-memory
					200000,100,100,10,186.1292 .... Throttled to 1 thread to avoid out-of-memory, kept finding improved indices

               Using OptimalPermutation on a Windows Surface:
                    N,K,D,B,Seconds
                    2500,100,100,10,0.923
                    5000,100,100,10,1.9922
                    10000,100,100,10,4.0008
                    25000,100,100,10,10.4536
                    50000,100,100,10,11.1932
                    100000,100,100,10,16.387
                    200000,100,100,10,27.4966
                No need to throttle the thread-count, since it uses less memory.
			 */
            var N = new[] { 2500, 5000, 10000, 25000, 50000, 100000, 200000, 300000, 400000 };
			var K = new[] { 100 };
			var Dims = new[] { 100 };
			var Bits = new[] { 10 };
			ClassifyMany(N, K, Dims, Bits, 5);
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
			var perfectClusterings = 0;
			var totalClusterings = 0;
			foreach (var n in N)
				foreach (var k in K)
				{
					foreach (var d in Dims)
						foreach (var bits in Bits)
						{
							var max = (1 << bits) - 1;
							var seconds = 0.0;
							for (var iteration = 0; iteration < repeat; iteration++)
							{
								var results = ClassifyPerformance(n, k, d, 0, max);
								seconds += results.Item1;
								if (results.Item2)
									perfectClusterings++;
								totalClusterings++;
							}
							seconds /= repeat;
							var record = $"{n},{k},{d},{bits},{seconds}\n";
							report += record;
							Console.WriteLine(record);
						}
					Console.WriteLine(report);
				}
			Console.WriteLine("Done! Final report:");
			Console.WriteLine(report);
			var message = $"{totalClusterings - perfectClusterings} of {totalClusterings} Clusterings failed";
			Console.WriteLine(message);
			Assert.AreEqual(totalClusterings, perfectClusterings, message);
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
			ClusterCore(data, acceptableBCubed);
		}

		private void ClassifyCase(Classification<UnsignedPoint,string> expectedClusters, double acceptableBCubed = 0.99)
		{
			var maxCoordinate = expectedClusters.Points().Select(p => p.MaxCoordinate).Max();
			var bitsPerDimension = ((int)maxCoordinate).SmallestPowerOfTwo();
			var classifier = new HilbertClassifier(expectedClusters.Points(), bitsPerDimension);

			classifier.IndexConfig.UseSample = true;

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
		/// <returns>Time in seconds and an Boolean which is false if the clustering did not produce perfect results.</returns>
		private Tuple<double,bool> ClassifyPerformance(int numPoints, int clusterCount, int dimensions,
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
			classifier.IndexConfig.UseSample = true;
			var actualClusters = classifier.Classify();
			timer.Stop();
			var success = expectedClusters.IsSimilarTo(actualClusters);
			if (!success)
				Console.WriteLine($"Clustering was not perfect. # of Clusters actual/expected: {actualClusters.NumPartitions}/{expectedClusters.NumPartitions}");
			var seconds = timer.ElapsedMilliseconds / 1000.0;
			return new Tuple<double,bool>(seconds, success);
		}

		/// <summary>
		/// Test case that classifies two clusters that may partially overlap.
		/// </summary>
		/// <param name="numPoints">Number points.</param>
		/// <param name="dimensions">Dimensions.</param>
		/// <param name="overlapPercent">Overlap percent.</param>
		/// <param name="clusterSizeVariation">Cluster size variation.</param>
		/// <param name="maxCoordinate">Max coordinate.</param>
		/// <param name="acceptableBCubed">Acceptable BC ubed.</param>
		private void ClassifyTwoClustersCase(int numPoints, int dimensions, double overlapPercent,
						  int clusterSizeVariation = 0, int maxCoordinate = 1000, double acceptableBCubed = 0.99, bool useDensityClassifier = true)
		{
			var acceptablePrecision = 0.98; //TODO: Add a parameter for this
			var results = ClassifyTwoClustersHelper(numPoints, dimensions, overlapPercent, clusterSizeVariation, maxCoordinate, acceptablePrecision, useDensityClassifier);
			var comparison = results.Item1;
			var message = $"   Quality {results.Item4}. Comparison of clusters: {comparison}.\n   Clusters expected/actual: {results.Item2}/{results.Item3}.";
			Console.WriteLine(message);
			Assert.GreaterOrEqual(comparison.BCubed, acceptableBCubed, $"Clustering was not good enough. BCubed = {comparison.BCubed}");
		}

		private void ClassifyTwoClustersReapeatedly(int repeatCount, int numPoints, int dimensions, double overlapPercent,
				  int clusterSizeVariation = 0, int maxCoordinate = 1000, double acceptablePrecision = 0.98, bool useDensityClassifier = true)
		{
			var histogram = new Dictionary<SplitQuality, int>() 
			{ 
				{ SplitQuality.BadOverSplit, 0 },
				{ SplitQuality.BadSplit, 0 },
				{ SplitQuality.GoodOverSplit, 0 },
				{ SplitQuality.FairOverSplit, 0 },
				{ SplitQuality.GoodSplit, 0 },
				{ SplitQuality.PerfectSplit, 0 },
				{ SplitQuality.Unsplit, 0 }
			};

			for (var iRepeat = 0; iRepeat < repeatCount; iRepeat++)
			{
				var results = ClassifyTwoClustersHelper(numPoints, dimensions, overlapPercent, clusterSizeVariation, maxCoordinate, acceptablePrecision, useDensityClassifier);
				histogram[results.Item4] = histogram[results.Item4] + 1;
			}
			var message = "";
			foreach (var pair in histogram)
			{
				message += $"Quality: {pair.Value} times {pair.Key} \n";
			}
			Logger.Info(message);
			Assert.AreEqual(repeatCount, histogram[SplitQuality.PerfectSplit], message);
		}

		private string ClassifyTwoClustersReport(int repeatCount, int numPoints, int dimensions, double[] overlapPercents,
		  int clusterSizeVariation = 0, int maxCoordinate = 1000, double acceptablePrecision = 0.98, bool useDensityClassifier = true)
		{
			Logger.SetupForTests();
			var report = $"Repeat count = {repeatCount}  N = {numPoints}  D = {dimensions}  Max Coord = {maxCoordinate}  Acc Prec = {acceptablePrecision}\n\nOverlap Percent,Perfect Split,Good Split,Good Over-split,Fair Over-split,Bad Split,Unsplit\n";
			foreach (var overlapPercent in overlapPercents)
			{
				var histogram = new Dictionary<SplitQuality, int>()
				{
					{ SplitQuality.BadOverSplit, 0 },
					{ SplitQuality.BadSplit, 0 },
					{ SplitQuality.GoodOverSplit, 0 },
					{ SplitQuality.FairOverSplit, 0 },
					{ SplitQuality.GoodSplit, 0 },
					{ SplitQuality.PerfectSplit, 0 },
					{ SplitQuality.Unsplit, 0 }
				};

				for (var iRepeat = 0; iRepeat < repeatCount; iRepeat++)
				{
					var results = ClassifyTwoClustersHelper(numPoints, dimensions, overlapPercent, clusterSizeVariation, maxCoordinate, acceptablePrecision, useDensityClassifier);
					histogram[results.Item4] = histogram[results.Item4] + 1;
				}
				var h = histogram;
				report += $"{overlapPercent},{h[SplitQuality.PerfectSplit]},{h[SplitQuality.GoodSplit]},{h[SplitQuality.GoodOverSplit]},{h[SplitQuality.FairOverSplit]},{h[SplitQuality.BadSplit]},{h[SplitQuality.Unsplit]}\n";

				Logger.Info(report);
			}
			Logger.Info("DONE!");
			return report;
		}

		public enum SplitQuality
		{
			/// <summary>
			/// Perfect clustering and correct number of clusters
			/// </summary>
			PerfectSplit,
			/// <summary>
			/// Too many clusters, but each cluster is homogeneous
			/// </summary>
			GoodOverSplit,

			/// <summary>
			/// Too many clusters, homogeneity (precision) is good, but not perfect.
			/// </summary>
			FairOverSplit,

			/// <summary>
			/// Too many clusters, and some have mixtures of points from the two ideal clusters
			/// </summary>
			BadOverSplit,
			/// <summary>
			/// Correct number of clusters, but a few points out of place
			/// </summary>
			GoodSplit,
			/// <summary>
			/// Correct number of clusters, but many points out of place
			/// </summary>
			BadSplit,
			/// <summary>
			/// One big cluster (failure to discriminate)
			/// </summary>
			Unsplit
		}

		/// <summary>
		/// Perform a classification of two clusters that are near enough to each other to partially overlap, causing problems.
		/// 
		/// From this we can deduce which of six cases obtain (the SplitQuality).
		/// </summary>
		/// <returns>A Tuple with these parts:
		///   1) comparison of actual to expected (with its BCubed), 
		///   2) the expected number of clusters 
		///   3) the actual number of clusters
		///   4) a qualitative assessment of the results.
		/// </returns>
		/// <param name="numPoints">Number of points.</param>
		/// <param name="dimensions">Number of Dimensions.</param>
		/// <param name="overlapPercent">Overlap percent.</param>
		/// <param name="clusterSizeVariation">Cluster size variation.</param>
		/// <param name="maxCoordinate">Max value of any coordinate.</param>
		/// <param name="acceptablePrecision">Acceptable precision</param>
		/// <param name="useDensityClassifier">If set to <c>true</c> use density classifier.</param>
		private Tuple<ClusterMetric<UnsignedPoint,string>, int, int, SplitQuality> ClassifyTwoClustersHelper(int numPoints, int dimensions, double overlapPercent,
				  int clusterSizeVariation = 0, int maxCoordinate = 1000, double acceptablePrecision = 0.98, bool useDensityClassifier = true)
		{
			Logger.SetupForTests();
			var bitsPerDimension = maxCoordinate.SmallestPowerOfTwo();
			var clusterCount = 2;
			var minClusterSize = (numPoints / clusterCount) - clusterSizeVariation;
			var maxClusterSize = (numPoints / clusterCount) + clusterSizeVariation;
			var outlierSize = 5;
			var radiusShrinkage = 0.6; // 0.7 merges too many that belong apart!
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = maxCoordinate,
				MinClusterSize = minClusterSize,
				MaxClusterSize = maxClusterSize
			};
			var expectedClusters = data.TwoClusters(overlapPercent);

			Classification<UnsignedPoint, string> actualClusters;
			if (useDensityClassifier)
			{
				var hIndex = new HilbertIndex(expectedClusters, bitsPerDimension);
				var cc = new ClusterCounter { NoiseSkipBy = 10, OutlierSize = outlierSize, ReducedNoiseSkipBy = 1 };
				var count = cc.Count(hIndex.SortedPoints);

				var unmergeableSize = expectedClusters.NumPoints / 6;
				var densityClassifier = new DensityClassifier(hIndex, count.MaximumSquareDistance, unmergeableSize)
				{
					 MergeableShrinkage = radiusShrinkage
				};

				actualClusters = densityClassifier.Classify();
			}
			else {
				var classifier = new HilbertClassifier(expectedClusters.Points(), 10) { OutlierSize = outlierSize };
				//classifier.IndexConfig.NoiseSkipBy = 0;
				classifier.IndexConfig.UseSample = false;
				actualClusters = classifier.Classify();
			}

			var comparison = expectedClusters.Compare(actualClusters);
			SplitQuality qualitativeResult = SplitQuality.Unsplit;
			if (comparison.BCubed >= 1.0)
				qualitativeResult = SplitQuality.PerfectSplit;
			else if (actualClusters.NumPartitions == 1)
				qualitativeResult = SplitQuality.Unsplit;
			else if (actualClusters.NumPartitions > expectedClusters.NumPartitions && comparison.Precision >= 1.0)
				qualitativeResult = SplitQuality.GoodOverSplit;
			else if (actualClusters.NumPartitions > expectedClusters.NumPartitions && comparison.Precision >= acceptablePrecision)
				qualitativeResult = SplitQuality.FairOverSplit;
			else if (actualClusters.NumPartitions == expectedClusters.NumPartitions && comparison.Precision >= acceptablePrecision)
				qualitativeResult = SplitQuality.GoodSplit;
			else if (actualClusters.NumPartitions > expectedClusters.NumPartitions && comparison.Precision < 1.0)
				qualitativeResult = SplitQuality.BadOverSplit;
			else // Assume correct number of clusters.
				qualitativeResult = SplitQuality.BadSplit;

			Logger.Info($"  Quality: {qualitativeResult}  Comparison: {comparison}");
			
			return new Tuple<ClusterMetric<UnsignedPoint, string>, int, int, SplitQuality>(
				comparison, 
				expectedClusters.NumPartitions, 
				actualClusters.NumPartitions,
				qualitativeResult
			);
		}

        #region Clusters that are Chains, not spheres


        [Test]
        /// <summary>
        /// Classifies random data with 100,000 points, 50 clusters of 20 segments each, 100 dimensions.
        /// </summary>
        public void Classify_Chains_100000N_50K_20S_100D()
        {
            Logger.SetupForTests();
            ClassifyChainCase(100000, 50, 20, 100, 0, 10000, 0.98);
        }


        /// <summary>
        /// Create test data in known chained clusters, perform unattended clustering, and compare the results to the known clusters.
        /// The test passes if the BCubed value is high enough.
        /// </summary>
        /// <param name="numPoints">Number of points to cluster.</param>
        /// <param name="clusterCount">Cluster count.</param>
        /// <param name="chainLength">Number of segments in each chain.</param>
        /// <param name="dimensions">Dimensions per point.</param>
        /// <param name="clusterSizeVariation">Cluster size variation.
        ///  The average number of points per cluster is numPoints/clusterCount. 
        ///  The actual size of a given cluster will be permitted to vary by as much as ± clusterSizeVariation.
        /// </param>
        /// <param name="maxCoordinate">All points will have coordinate values in the range 0 to maxCoordinate.</param>
        /// <param name="acceptableBCubed">The comparison of the actual and expected clusters must yield a BCubed value
        /// that is this high or higher. A value of 1.0 means a perfect clustering, with no points out of place.</param>
        private void ClassifyChainCase(int numPoints, int clusterCount, int chainLength, int dimensions,
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
                MaxClusterSize = maxClusterSize,
                MaxDistanceStdDev = 300,
                MinDistanceStdDev = 150
            };
            ClusterChainCore(data, acceptableBCubed, chainLength);
        }

        private void ClusterChainCore(GaussianClustering data, double acceptableBCubed, int chainLength)
        {
            var expectedClusters = data.MakeChains(chainLength);
            var classifier = new HilbertClassifier(expectedClusters.Points(), 10);
            classifier.IndexConfig.UseSample = true;
            var actualClusters = classifier.Classify();
            var comparison = expectedClusters.Compare(actualClusters);

            var message = $"   Comparison of clusters: {comparison}.\n   Clusters expected/actual: {expectedClusters.NumPartitions}/{actualClusters.NumPartitions}.";
            Logger.Info(message);
            var message2 = $"   Large clusters: {actualClusters.NumLargePartitions(classifier.OutlierSize)}";
            Logger.Info(message2);
            var pointsInOutliers = actualClusters.LabelToPoints.Values
                .Select(values => values.Count())
                .Where(count => count < classifier.OutlierSize)
                .Sum();
            var message3 = $"   Points in Outliers/Total Point: {pointsInOutliers} / {actualClusters.NumPoints}";
            Logger.Info(message3);
            Assert.GreaterOrEqual(comparison.BCubed, acceptableBCubed, $"Clustering was not good enough. BCubed = {comparison.BCubed}");
        }
        #endregion
    }
}
