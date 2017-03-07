using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HilbertTransformationTests.Data;
using Clustering;
using HilbertTransformation;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class PolyChromaticClosestPointTests
	{
		[SetUp]
		public void SetUp()
		{
			var c = new ConsoleTraceListener(true);
			Trace.Listeners.Add(c);
		}

		[Test]
		public void PairsAmongFiftySmallClustersByCentroids()
		{
			FindPairByCentroidsTestCase(100, 100, 50);
		}

		[Test]
		public void PairsAmongFiftySmallClusters()
		{
			GaussianPolyChromaticPairTestCase(100, 100, 50);
		}

		[Test]
		public void PairsAmongFiftySmallClustersShortestPath()
		{
			GaussianPolyChromaticPairTestCase(100, 100, 50, 40);
		}

		[Test]
		public void PairsAmongTwoHundredLargeClusters()
		{
			GaussianPolyChromaticPairTestCase(1000, 100, 200);
		}

		[Test]
		public void ClosestOfFiftyClustersWithFourCurves()
		{
			ClosestClusterTest(100, 100, 50, 1000, 4);
		}

		[Test]
		public void ClosestOfFiftyClustersWithTenCurves()
		{
			ClosestClusterTest(100, 100, 50, 1000, 10);
		}

		[Test]
		public void ClosestOfFiftyClustersWithTwentyCurves()
		{
			ClosestClusterTest(100, 100, 50, 1000, 20);
		}

		[Test]
		public void ClosestOfFiftyClustersWithFiftyCurves()
		{
			ClosestClusterTest(100, 100, 50, 1000, 50);
		}

		[Test]
		public void ClosestOfFiftyClusters()
		{
			int hilbertTries = 1000;
			var correctColorCount = 0;
			var correctCrosscheckCount = 0;
			var correctDistanceCount = 0;
			var nPoints = 100;
			var dimensions = 100;
			var clusterCount = 50;
			var data = new GaussianClustering
			{
				ClusterCount = clusterCount,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = nPoints,
				MaxClusterSize = nPoints
			};

			var closestExact = new PolyChromaticClosestPoint<string>.ClosestPair();
			var closestApproximate = new PolyChromaticClosestPoint<string>.ClosestPair();
			var bitsPerDimension = (1 + data.MaxCoordinate).SmallestPowerOfTwo();

			var clusters = data.MakeClusters();
			Assert.AreEqual(clusterCount, clusters.NumPartitions, "Test data are grouped into fewer clusters than requested.");
				 
			PolyChromaticClosestPoint<string> pccp;
			if (hilbertTries <= 1)
				pccp = new PolyChromaticClosestPoint<string>(clusters);
			else {
				var reducedNoiseSkipBy = 1;
				var results = OptimalIndex.Search(
					clusters.Points().Select(up => HilbertPoint.CastOrConvert(up, bitsPerDimension, true)).ToList(),
					5 /*outlier size */, 10 /* NoiseSkipBy */, reducedNoiseSkipBy, hilbertTries
				);
				pccp = new PolyChromaticClosestPoint<string>(clusters, results.Index);
			}
			foreach (var color in pccp.Clusters.ClassLabels())
			{
				var exact = pccp.FindClusterExhaustively(color);
				var approximate = pccp.FindClusterApproximately(color);
				var crosscheck = pccp.FindClusterIteratively(color);

				if (exact.SquareDistance >= approximate.SquareDistance)
					correctDistanceCount++;

				if (exact.Color2.Equals(approximate.Color2))
					correctColorCount++;

				if (exact.Color2.Equals(crosscheck.Color2))
					correctCrosscheckCount++;

				if (exact.SquareDistance < closestExact.SquareDistance)
					closestExact = exact;

				if (approximate.SquareDistance < closestApproximate.SquareDistance)
					closestApproximate = approximate;

				var ratio = approximate.SquareDistance / (double)exact.SquareDistance;
				Console.WriteLine(string.Format("Exact {0} vs Approx. {1} vs Cross {2}. Over by {3:N3}%", exact, approximate, crosscheck, (ratio - 1.0) * 100.0));
			}

			if (closestExact.SquareDistance >= closestApproximate.SquareDistance)
				Console.WriteLine("DID FIND the closest pair of points overall. Exact {0}. Approx {1}", closestExact, closestApproximate);
			else
				Console.WriteLine("DID NOT FIND the closest pair of points overall. Exact {0}. Approx {1}", closestExact, closestApproximate);

			Assert.IsTrue(correctColorCount == clusterCount && correctDistanceCount == clusterCount,
				string.Format("Of {0} clusters, only {1} searches found the closest cluster and {2} found the shortest distance. Crosscheck = {3}",
					clusterCount,
					correctColorCount,
					correctDistanceCount,
					correctCrosscheckCount
				)
			);
		}

		/// <summary>
		/// See how often the approximately closest cluster really is the closest cluster.
		/// </summary>
		[Test]
		public void FindAllClustersApproximately_100P_100D_50C_Test()
		{
			AllColorPairsClosestClusterTest(100, 100, 50, 1000);
		}

		/// <summary>
		/// A test case for PolyChromaticClosestPoint.FindPairApproximately where clusters conform to a Gaussian distribution.
		/// </summary>
		/// <param name="nPoints">Number of points in each cluster.</param>
		/// <param name="dimensions">Number of Dimensions in each point.</param>
		/// <param name="numClusters">Number of clusters to create.</param>
		/// <param name="hilbertsToTry">Number of randomly generated Hilbert curves to try.</param>
		public void GaussianPolyChromaticPairTestCase(int nPoints, int dimensions, int numClusters, int hilbertsToTry = 1)
		{
			var successes = 0;
			var worstRatio = 1.0;
			var color1 = "0";

			var data = new GaussianClustering
			{
				ClusterCount = numClusters,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = nPoints,
				MaxClusterSize = nPoints
			};
			var clusters = data.MakeClusters();
			PolyChromaticClosestPoint<string> pccp;
			if (hilbertsToTry <= 1)
				pccp = new PolyChromaticClosestPoint<string>(clusters);
			else
			{
				var bitsPerDimension = (1 + data.MaxCoordinate).SmallestPowerOfTwo();
				var results = OptimalIndex.Search(
					clusters.Points().Select(up => HilbertPoint.CastOrConvert(up, bitsPerDimension, true)).ToList(), 
					5 /*outlier size */, 10 /* NoiseSkipBy */, 1 /* ReducedNoiseSkipBy */, hilbertsToTry
				);
				pccp = new PolyChromaticClosestPoint<string>(clusters, results.Index);
			}
			for (var iColor2 = 1; iColor2 < numClusters; iColor2++)
			{
				var color2 = iColor2.ToString();

				var exact = pccp.FindPairExhaustively(color1, color2);
				var approximate = pccp.FindPairApproximately(color1, color2);

				var expectedDistance = exact.SquareDistance;
				var actualDistance = approximate.SquareDistance;

				if (actualDistance <= expectedDistance)
					successes++;
				else
					worstRatio = Math.Max(worstRatio, actualDistance / (double)expectedDistance);

				if (exact.SquareDistance >= approximate.SquareDistance)
					Console.WriteLine("FindPairApproximately CORRECT.   Exact {0}. Approx {1}", exact, approximate);
				else
					Console.WriteLine("FindPairApproximately INCORRECT. Exact {0}. Approx {1}. Too high by {2:N3}%",
						exact, approximate, 100.0 * (approximate.SquareDistance / (double)exact.SquareDistance - 1.0));

			}

			Assert.AreEqual(numClusters - 1, successes,
				string.Format("Did not succeed every time. Failed {0} of {1} times. Worst distance ratio is {2:N4}. {3} points of {4} dimensions.",
					numClusters - successes - 1,
					numClusters - 1,
					worstRatio,
					nPoints,
					dimensions
				)
			);
		}

		/// <summary>
		/// A test case for PolyChromaticClosestPoint.FindPairByCentroids where clusters conform to a Gaussian distribution.
		/// </summary>
		/// <param name="nPoints">Number of points in each cluster.</param>
		/// <param name="dimensions">Number of Dimensions in each point.</param>
		/// <param name="numClusters">Number of clusters to create.</param>
		public void FindPairByCentroidsTestCase(int nPoints, int dimensions, int numClusters)
		{
			var successes = 0;
			var worstRatio = 1.0;
			var color1 = "0";

			var data = new GaussianClustering
			{
				ClusterCount = numClusters,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = nPoints,
				MaxClusterSize = nPoints
			};
			var clusters = data.MakeClusters();
			PolyChromaticClosestPoint<string> pccp;

			pccp = new PolyChromaticClosestPoint<string>(clusters);

			for (var iColor2 = 1; iColor2 < numClusters; iColor2++)
			{
				var color2 = iColor2.ToString();

				var exact = pccp.FindPairExhaustively(color1, color2);
				var approximate = pccp.FindPairByCentroids(color1, color2);

				var expectedDistance = exact.SquareDistance;
				var actualDistance = approximate.SquareDistance;

				if (actualDistance <= expectedDistance)
					successes++;
				else
					worstRatio = Math.Max(worstRatio, actualDistance / (double)expectedDistance);

				if (exact.SquareDistance >= approximate.SquareDistance)
					Console.WriteLine("FindPairByCentroids CORRECT.   Exact {0}. Approx {1}", exact, approximate);
				else
					Console.WriteLine("FindPairByCentroids INCORRECT. Exact {0}. Approx {1}. Too high by {2:N3}%",
						exact, approximate, 100.0 * (approximate.SquareDistance / (double)exact.SquareDistance - 1.0));

			}

			Assert.AreEqual(numClusters - 1, successes,
				string.Format("Did not succeed every time. Failed {0} of {1} times. Worst distance ratio is {2:N4}. {3} points of {4} dimensions.",
					numClusters - successes - 1,
					numClusters - 1,
					worstRatio,
					nPoints,
					dimensions
				)
			);
		}

		public void ClosestClusterTest(int nPoints, int dimensions, int numClusters, int numCurvesToTry, int numCurvesToKeep)
		{
			var correctColorCount = 0;
			var correctDistanceCount = 0;
			var data = new GaussianClustering
			{
				ClusterCount = numClusters,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = nPoints,
				MaxClusterSize = nPoints
			};

			var closestExact = new PolyChromaticClosestPoint<string>.ClosestPair();
			var closestApproximate = new PolyChromaticClosestPoint<string>.ClosestPair();

			var clusters = data.MakeClusters();
			var pccps = new List<PolyChromaticClosestPoint<string>>();

			var bitsPerDimension = (1 + data.MaxCoordinate).SmallestPowerOfTwo();

			var bestIndices = OptimalIndex.SearchMany(
				clusters.Points().Select(up => HilbertPoint.CastOrConvert(up, bitsPerDimension, true)).ToList(),
				numCurvesToKeep,
				5 /*outlier size */, 10 /* NoiseSkipBy */, 1 /* ReducedNoiseSkipBy */, numCurvesToTry
			);

			//var pointLists = bestIndices.Select(result => result.Index.SortedPoints).ToList();
			//foreach (var pList in pointLists)
			//	pccps.Add(new PolyChromaticClosestPoint<string>(clusters, pList));

			var indices = bestIndices.Select(result => result.Index).ToList();
			foreach (var index in indices)
				pccps.Add(new PolyChromaticClosestPoint<string>(clusters, index));

			var pccp1 = pccps[0];
			foreach (var color in pccp1.Clusters.ClassLabels())
			{
				var exact = pccp1.FindClusterExhaustively(color);
				var approximate = pccps.Select(pccp => pccp.FindClusterApproximately(color)).OrderBy(cp => cp).First();

				if (exact.SquareDistance >= approximate.SquareDistance)
					correctDistanceCount++;

				if (exact.Color2.Equals(approximate.Color2))
					correctColorCount++;

				if (exact.SquareDistance < closestExact.SquareDistance)
					closestExact = exact;

				if (approximate.SquareDistance < closestApproximate.SquareDistance)
					closestApproximate = approximate;

				var ratio = approximate.SquareDistance / (double)exact.SquareDistance;
				Console.WriteLine(string.Format("Exact {0} vs Approx. {1}. Over by {2:N3}%", exact, approximate, (ratio - 1.0) * 100.0));
			}

			if (closestExact.SquareDistance >= closestApproximate.SquareDistance)
				Console.WriteLine("DID FIND the closest pair of points overall. Exact {0}. Approx {1}", closestExact, closestApproximate);
			else
				Console.WriteLine("DID NOT FIND the closest pair of points overall. Exact {0}. Approx {1}", closestExact, closestApproximate);

			Assert.IsTrue(correctColorCount == numClusters && correctDistanceCount == numClusters,
				string.Format("Of {0} clusters, only {1} searches found the closest cluster and {2} found the shortest distance.",
					numClusters,
					correctColorCount,
					correctDistanceCount
				)
			);
		}

		public void AllColorPairsClosestClusterTest(int nPoints, int dimensions, int numClusters, int numCurvesToTry)
		{
			var rankHistogram = new int[numClusters + 1]; // We will skip the first element so as to have a one-based array.
			var data = new GaussianClustering
			{
				ClusterCount = numClusters,
				Dimensions = dimensions,
				MaxCoordinate = 1000,
				MinClusterSize = nPoints,
				MaxClusterSize = nPoints
			};
			var worstDistanceRatio = 1.0;
			var ratioSum = 0.0;
			var ratioCount = 0;
			var clusters = data.MakeClusters();

			var bitsPerDimension = (1 + data.MaxCoordinate).SmallestPowerOfTwo();
			var results = OptimalIndex
				.Search(
					clusters.Points().Select(up => HilbertPoint.CastOrConvert(up, bitsPerDimension, true)).ToList(), 
					5 /*outlier size */, 10 /* NoiseSkipBy */, 1 /* ReducedNoiseSkipBy */, numCurvesToTry
				);

			var pccp1 = new PolyChromaticClosestPoint<string>(clusters, results.Index);
			var allColorPairs = pccp1.FindAllClustersApproximately();
			foreach (var color1 in clusters.ClassLabels())
			{
				var exact = pccp1.FindClusterExhaustively(color1).Swap(color1);
				var color1Pairs = allColorPairs
					.Where(cp => cp.Color1.Equals(color1) || cp.Color2.Equals(color1))
					.Select(cp => cp.Swap(color1))
					.ToList();
				var approximateColor2Distance = color1Pairs.First(cp => cp.Color2.Equals(exact.Color2)).SquareDistance;
				var approximateRank = color1Pairs.Count(cp => cp.SquareDistance < approximateColor2Distance) + 1;
				rankHistogram[approximateRank]++;
#pragma warning disable RECS0018 // Comparison of floating point numbers with equality operator
				var ratio = exact.SquareDistance == 0.0 ? 0 : approximateColor2Distance / (double)exact.SquareDistance;
#pragma warning restore RECS0018 // Comparison of floating point numbers with equality operator
				ratioSum += ratio;
				ratioCount++;
				worstDistanceRatio = Math.Max(worstDistanceRatio, ratio);
			}
			Debug.WriteLine(string.Format("Worst distance overage   = {0:N3}%", (worstDistanceRatio - 1.0) * 100.0));
			Debug.WriteLine(string.Format("Average distance overage = {0:N3}%", ((ratioSum / ratioCount) - 1.0) * 100.0));
			for (var iRank = 1; iRank <= numClusters; iRank++)
			{
				if (rankHistogram[iRank] > 0 || iRank < 4)
					Debug.WriteLine(string.Format("For {0} Clusters the closest cluster found was Ranked #{1}.", rankHistogram[iRank], iRank));
			}
			// Accept a win, place or show: the true closest cluster shows up as no worse than the 3rd ranked cluster according to the approximate measure.
			Assert.IsTrue(rankHistogram[1] + rankHistogram[2] + rankHistogram[3] == numClusters,
				string.Format("Found the closest cluster for {0} colors", rankHistogram[1])
			);

		}
	}
}

