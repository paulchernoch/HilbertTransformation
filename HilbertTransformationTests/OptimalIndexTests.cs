using System;
using NUnit.Framework;
using HilbertTransformationTests.Data;
using Clustering;
using HilbertTransformation;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using HilbertTransformation.Random;

namespace HilbertTransformationTests
{
	/// <summary>
	/// Using OptimalIndex, perform a search for the optimal HilbertIndex against different sizes of problem.
	/// </summary>
	[TestFixture]
	public class OptimalIndexTests
	{
		[Test]
		public void For_50Dim_5000Pts_50Clusters()
		{
			int hilbertTries = 1000;
			var minClusterSize = 100;
			var maxClusterSize = 100;
			var dimensions = 50;
			var clusterCount = 50;
			var acceptableClusterCount = 75;
			var bitsPerDimension = 10;
			var outlierSize = 5;
			var noiseSkipBy = 10;

			OptimalIndexTestCase(
				hilbertTries, minClusterSize, maxClusterSize, dimensions,
				clusterCount, acceptableClusterCount,
				bitsPerDimension, outlierSize, noiseSkipBy
			);
		}

		[Test]
		public void For_75Dim_10000Pts_100Clusters()
		{
			int hilbertTries = 1000;
			var minClusterSize = 100;
			var maxClusterSize = 100;
			var dimensions = 75;
			var clusterCount = 100;
			var acceptableClusterCount = 150;
			var bitsPerDimension = 10;
			var outlierSize = 5;
			var noiseSkipBy = 10;

			OptimalIndexTestCase(
				hilbertTries, minClusterSize, maxClusterSize, dimensions,
				clusterCount, acceptableClusterCount,
				bitsPerDimension, outlierSize, noiseSkipBy
			);
		}

		[Test]
		public void For_200Dim_40000Pts_200Clusters()
		{
			int hilbertTries = 1000;
			var minClusterSize = 175;
			var maxClusterSize = 225;
			var dimensions = 200;
			var clusterCount = 200;
			var acceptableClusterCount = 350;
			var bitsPerDimension = 10;
			var outlierSize = 5;
			var noiseSkipBy = 10;

			OptimalIndexTestCase(
				hilbertTries, minClusterSize, maxClusterSize, dimensions,
				clusterCount, acceptableClusterCount,
				bitsPerDimension, outlierSize, noiseSkipBy
			);
		}


		private void OptimalIndexTestCase(
			int hilbertTries, int minClusterSize, int maxClusterSize, int dimensions, int clusterCount, int acceptableClusterCount,
			int bitsPerDimension, int outlierSize, int noiseSkipBy)
		{
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = minClusterSize,
				MaxClusterSize = maxClusterSize
			};
			var clusters = data.MakeClusters();
			var points = clusters.Points().Select(p => HilbertPoint.CastOrConvert(p, bitsPerDimension, true)).ToList();
			var results = OptimalIndex.Search(
				points,
				outlierSize, 
				noiseSkipBy,
				hilbertTries, // maxTrials
				4 // maxIterationsWithoutImprovement
			);
			var message = $"Estimated cluster count = {results.EstimatedClusterCount}, actual = {clusterCount}, acceptable = {acceptableClusterCount}";
			Console.WriteLine(message);
			Assert.LessOrEqual(results.EstimatedClusterCount, acceptableClusterCount, $"HilbertIndex fragmented by more than 50%: {message}");
		}

		/// <summary>
		/// Explore the execution time dependence of HilbertIndex creation on the number of points in the index.
		/// </summary>
		[Test]
		public void IndexCreationPerformanceVarying_N()
		{
			/*
				Hilbert Index Creation
				N,K,D,B,Seconds
				1000,100,100,10,0.01255
				2000,100,100,10,0.0214
				5000,100,100,10,0.04925
				10000,100,100,10,0.09995
				20000,100,100,10,0.20025
				50000,100,100,10,0.50545
				100000,100,100,10,1.0198
				200000,100,100,10,2.0944

				Shows close to linear dependence on number of points, as expected.
			 */
			int K = 100, D = 100, B = 10, repeats = 20;
			var N = new[] { 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000 };
			IndexCreationPerformanceVariation(N, new[]{ K }, new[]{ D }, new[]{ B }, repeats);
		}

		/// <summary>
		/// Explore the execution time dependence of HilbertIndex creation on the number of dimensions of each indexed point.
		/// </summary>
		[Test]
		public void IndexCreationPerformanceVarying_D()
		{
			/*
				Hilbert Index Creation
				N,K,D,B,Seconds
				40000,100,10,10,0.12225
				40000,100,20,10,0.1433
				40000,100,30,10,0.1786
				40000,100,40,10,0.2107
				40000,100,50,10,0.23885
				40000,100,100,10,0.40645
				40000,100,200,10,0.72685
				40000,100,400,10,1.3605

				Shows close to linear dependence on number of dimensions, as expected.
			 */
			int N = 40000, K = 100, B = 10, repeats = 20;
			var D = new[] { 10, 20, 30, 40, 50, 100, 200, 400 };
			IndexCreationPerformanceVariation(new int[] { N }, new[] { K }, D, new[] { B }, repeats);
		}

		/// <summary>
		/// Explore the execution time dependence of HilbertIndex creation on the number of bits per dimension.
		/// </summary>
		[Test]
		public void IndexCreationPerformanceVarying_B()
		{
			//TODO: Fix Arithmetic exception for 30 bits per dimension.
			/*
				Hilbert Index Creation
				N,K,D,B,Seconds
				30000,100,50,10,0.1784
				30000,100,50,12,0.19335
				30000,100,50,14,0.2124
				30000,100,50,16,0.22855
				30000,100,50,18,0.2473
				30000,100,50,20,0.2733
				30000,100,50,22,0.2813
				30000,100,50,24,0.29865
				30000,100,50,26,0.3205
				30000,100,50,28,0.33565
				30000,100,50,29,0.3445

				Shows close to linear dependence on bits per dimension, as expected.
			 */
			int N = 30000, K = 100, D = 50, repeats = 20;
			var B = new[] { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 29 };
			IndexCreationPerformanceVariation(new int[] { N }, new[] { K }, new[] { D }, B, repeats);
		}

		public void IndexCreationPerformanceVariation(int[] N, int[] K, int[] D, int[] B, int repeats)
		{
			var report = "Hilbert Index Creation\nN,K,D,B,Seconds\n";
			foreach (var n in N)
				foreach (var k in K)
					foreach (var d in D)
						foreach (var b in B)
						{
							var repeatsToUse = n >= 100000 ? repeats / 2 : repeats; // Fewer iterations for longer tests to save time.
							var seconds = SingleIndexCreationPerformanceCase(n, k, d, b, repeatsToUse);
							var record = $"{n},{k},{d},{b},{seconds}\n";
							report += record;
							Console.WriteLine(report);
						}
			Console.WriteLine(report);
		}

		/// <summary>
		/// For the same test data, create a single HilbertIndex many times and average the execution time across all indices.
		/// 
		/// The goal is to identify how the time depends on number of points N, number of dimensions D, and bits per coordinate B.
		/// (It should be insensitive to cluster count K.)
		/// </summary>
		/// <param name="N">Number of points to index.</param>
		/// <param name="K">Number of clusters of points to create.</param>
		/// <param name="D">Number dimensions.</param>
		/// <param name="B">Number bits.</param>
		/// <param name="repeats">Number of times to repeat.</param>
		/// <returns>Average number of seconds to create the index, averaged over several tries.
		/// The time excludes the time to create the test data.
		/// </returns>
		private double SingleIndexCreationPerformanceCase(int N, int K, int D, int B, int repeats)
		{
			var data = new GaussianClustering
			{
				ClusterCount = K,
				Dimensions = D,
				MaxCoordinate = (1 << B) - 1,
				MinClusterSize = N/K,
				MaxClusterSize = N/K
			};
			var clusters = data.MakeClusters();
			var timer = new Stopwatch();
			var totalTimeMilliseconds = 0L;
			for (var i = 0; i < repeats; i++)
			{
				timer.Reset();
				timer.Start();
				var hIndex = new HilbertIndex(clusters, B);
				Assert.AreEqual(N, hIndex.Count, "Index has wrong number of points"); 
				timer.Stop();
				totalTimeMilliseconds += timer.ElapsedMilliseconds;
			}
			return (double)totalTimeMilliseconds / (1000.0 * repeats);
		}

		/// <summary>
		/// Reveals how the use of a Hilbert index clearly reveals the proper distance between points that 
		/// belong together in a cluster, while randomly chosen pairs of points do not.
		/// </summary>
		[Test]
		public void DistanceDistribution()
		{
			/*
				Percentile,By Index,By Random
				-----------------------------
				0%,111.35,146.55
				1%,142.06,255.96
				2%,147.21,2163.43
				3%,151.2,2214.15
				4%,154.06,2245.2
				5%,156.24,2271.37
				6%,158.38,2292.29
				7%,160.42,2313.55
				8%,162.29,2327.14
				9%,164.07,2345.25
				10%,165.41,2359.95
				11%,166.72,2372.83
				12%,167.99,2386.15
				13%,169.29,2398.47
				14%,170.43,2410.01
				15%,171.53,2422.34
				16%,172.48,2432.43
				17%,173.58,2443.08
				18%,174.73,2454.27
				19%,175.56,2463.71
				20%,176.35,2472.97
				21%,177.35,2483.24
				22%,178.3,2491.9
				23%,179.1,2501.44
				24%,179.82,2510.26
				25%,180.64,2517.73
				26%,181.55,2524.97
				27%,182.33,2531.58
				28%,182.98,2538.08
				29%,183.67,2543.83
				30%,184.33,2550.93
				31%,185.09,2556.59
				32%,185.7,2563.37
				33%,186.41,2570.29
				34%,187.09,2577.29
				35%,187.7,2583.56
				36%,188.43,2589.95
				37%,189.07,2596.13
				38%,189.71,2602.24
				39%,190.46,2608.28
				40%,191.08,2615.25
				41%,191.79,2620.81
				42%,192.46,2626.02
				43%,193.09,2632.7
				44%,193.71,2638.18
				45%,194.31,2643.35
				46%,194.98,2648.69
				47%,195.65,2655.47
				48%,196.3,2660.26
				49%,196.96,2666.37
				50%,197.66,2670.94
				51%,198.34,2677.09
				52%,199.07,2681.9
				53%,199.72,2687.11
				54%,200.3,2692.42
				55%,201.06,2697.92
				56%,201.71,2703.76
				57%,202.4,2710.17
				58%,203.16,2715.06
				59%,203.82,2720.25
				60%,204.51,2725.99
				61%,205.32,2731.6
				62%,206.08,2736.59
				63%,206.79,2741.72
				64%,207.58,2746.59
				65%,208.29,2754.03
				66%,209.07,2760.81
				67%,209.8,2766.65
				68%,210.68,2771.98
				69%,211.71,2778.27
				70%,212.38,2784.23
				71%,213.19,2790.71
				72%,213.92,2796.42
				73%,214.82,2802.84
				74%,215.68,2809.36
				75%,216.54,2814.55
				76%,217.48,2821.32
				77%,218.43,2827.56
				78%,219.35,2833.35
				79%,220.28,2840.72
				80%,221.33,2848.87
				81%,222.31,2856.89
				82%,223.42,2864
				83%,224.46,2872.51
				84%,225.83,2881.09
				85%,227.06,2891.57
				86%,228.27,2900.46
				87%,229.63,2910.46
				88%,231.55,2919.5
				89%,233.59,2933.76
				90%,235.6,2944.88
				91%,237.25,2959.45
				92%,239.83,2976.08
				93%,241.88,2990.4
				94%,244.97,3010.08
				95%,248.23,3029.15
				96%,252.34,3052.37
				97%,260.68,3074.84
				98%,282.76,3112.43      *** Note the jump from 282 to 2550, which shows that the characteristic distance is about 282.
				99%,2550.87,3170.93
				100%,3114.89,3412.57
			 */ 
			var data = new GaussianClustering
			{
				ClusterCount = 100,
				Dimensions = 50,
				MaxCoordinate = 1000,
				MinClusterSize = 50,
				MaxClusterSize = 150
			};
			var clusters = data.MakeClusters();
			var bitsPerDimension = 10;
			var points = clusters.Points().Select(p => HilbertPoint.CastOrConvert(p, bitsPerDimension, true)).ToList();
			var results = OptimalIndex.Search(
				points,
				5,     // outlierSize
				10,    // noiseSkipBy
				1000,  // maxTrials
				4      // maxIterationsWithoutImprovement
			);
			var pointsFromIndex = results.Index.SortedPoints;
			var distancesRandom = new List<long>();
			var distancesHilbert = new List<long>();
			var n = pointsFromIndex.Count;
			var rng = new FastRandom();
			for (var i = 0; i < n - 1; i++)
			{
				var p1 = pointsFromIndex[i];
				var p2 = pointsFromIndex[i + 1];
				distancesHilbert.Add(p1.Measure(p2));

				var p3 = pointsFromIndex[rng.Next(n)];
				var p4 = pointsFromIndex[rng.Next(n)];
				distancesRandom.Add(p3.Measure(p4));
			}
			distancesHilbert.Sort();
			distancesRandom.Sort();
			Console.WriteLine("Percentile,By Index,By Random");
			for (var percentile = 0; percentile <= 100; percentile++)
			{
				var i = Math.Min(n - 2, (n - 1) * percentile / 100);
				var distHilbert = Math.Round(Math.Sqrt(distancesHilbert[i]), 2);
				var distRandom = Math.Round(Math.Sqrt(distancesRandom[i]), 2);
				Console.Write($"{percentile}%,{distHilbert},{distRandom}");
			}
		}
	}
}
