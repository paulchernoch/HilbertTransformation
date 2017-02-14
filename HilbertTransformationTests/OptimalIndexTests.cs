using System;
using NUnit.Framework;
using HilbertTransformationTests.Data;
using Clustering;
using HilbertTransformation;
using System.Linq;

namespace HilbertTransformationTests
{
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

		public void OptimalIndexTestCase(
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
	}
}
