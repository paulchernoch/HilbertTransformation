using System;
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
		/// Classifies random data with 10,000 points, 30 dimensions, 100 clusters.
		/// </summary>
		public void Classify_N10000_30D_100K()
		{
			ClassifyCase(10000, 30, 100);
		}

		private void ClassifyCase(int numPoints, int clusterCount, int dimensions, int clusterSizeVariation = 0, int maxCoordinate = 1000)
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
			// Do an extra outlier pass.
			//var farther = (long)(classifier.MergeSquareDistance * classifier.OutlierDistanceMultiplier * 5);
			var farther = long.MaxValue;
			classifier.MaxNeighborsToCompare = actualClusters.NumLargePartitions(classifier.OutlierSize);
			var extraMerges = classifier.MergeOutliers(farther);

			var comparison = expectedClusters.Compare(actualClusters);

			var message = $"   Comparison of clusters: {comparison}.\n   Clusters expected/actual: {expectedClusters.NumPartitions}/{actualClusters.NumPartitions}.\n   Extra outlier merges = {extraMerges}";
			Console.WriteLine(message);
			Console.WriteLine($"   Large clusters: {actualClusters.NumLargePartitions(classifier.OutlierSize)}");
			Assert.GreaterOrEqual(comparison.BCubed, 0.97, $"Clustering was not good enough. BCubed = {comparison.BCubed}"); 
		}
	}
}
