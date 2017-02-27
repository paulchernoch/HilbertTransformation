using System;
using System.Linq;
using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class DensityMeterTests
	{
		/// <summary>
		/// Test if the two ways of computing density, exaxt and estimated, are highly-correlated.
		/// If they are, the more efficient estimated computation can be used in clustering.
		/// 
		/// Use Kendall Tau-B correlation for the test.
		/// </summary>
		[Test]
		public void DensityCorrelation()
		{
			var bitsPerDimension = 10;
			var data = new GaussianClustering
			{
				ClusterCount = 50,
				Dimensions = 100,
				MaxCoordinate = (1 << bitsPerDimension) - 1,
				MinClusterSize = 100,
				MaxClusterSize = 500
			};
			var expectedClusters = data.MakeClusters();
			var hIndex = new HilbertIndex(expectedClusters, bitsPerDimension);
			var cc = new ClusterCounter { NoiseSkipBy = 10, OutlierSize = 5, ReducedNoiseSkipBy = 1 };
			var count = cc.Count(hIndex.SortedPoints);
			// Choice of neighborhoodDistance is crucial.
			//   - If it is too large, then a huge number of neighbors will be caught up in the dragnet, and estimating
			//	   that value with a window into the Hilbert curve will yield poor results. Why? If there are 200 neighbors
			//     and your window size is 100 then many points will have their neighbor count saturate near 100 and
			//     no meaningful variation in density will be found. 
			//   - If it is too small, then too few neighbors (or none!) will be found, and we get no meaningful density.
			//   - We know that almost every point has two neighbors within MaximumSquareDistance, so we should
			//     make it smaller than MaximumSquareDistance.
			var neighborhoodDistance = count.MaximumSquareDistance * 2 / 5;
			var numPoints = hIndex.SortedPoints.Count;

			var windowRadius = (int)Math.Sqrt(numPoints / 2);
			var dMeter = new DensityMeter(hIndex, neighborhoodDistance, windowRadius);

			Func<HilbertPoint, long> exactMetric = p => (long)dMeter.ExactNeighbors(p);
			Func<HilbertPoint, long> estimatedMetric = p => (long)dMeter.EstimatedDensity(p, windowRadius);
			var correlator = new KendallTauCorrelation<HilbertPoint,long>(exactMetric, estimatedMetric);
			var correlation = correlator.TauB(hIndex.SortedPoints.Take(1000));

			Console.WriteLine($"Correlation between exact and estimated density is: {correlation}");
			Assert.GreaterOrEqual(correlation, 0.90, $"Correlation {correlation} is not high enough");
		}

		[Test]
		public void DensityCompared()
		{
			var bitsPerDimension = 10;
			var data = new GaussianClustering
			{
				ClusterCount = 50,
				Dimensions = 100,
				MaxCoordinate = (1 << bitsPerDimension) - 1,
				MinClusterSize = 100,
				MaxClusterSize = 500
			};
			var expectedClusters = data.MakeClusters();
			var hIndex = new HilbertIndex(expectedClusters, bitsPerDimension);
			var cc = new ClusterCounter { NoiseSkipBy = 10, OutlierSize = 5, ReducedNoiseSkipBy = 1 };
			var count = cc.Count(hIndex.SortedPoints);
			var neighborhoodDistance = count.MaximumSquareDistance * 2/5;
			var numPoints = hIndex.SortedPoints.Count;
			var windowRadius = (int)Math.Sqrt(numPoints / 2);
			var dMeter = new DensityMeter(hIndex, neighborhoodDistance, windowRadius);

			Console.WriteLine($"Window Radius = {windowRadius}. {hIndex.SortedPoints.Count} points");
			Console.Write("Exact,Estimated");
			for (var i = 0; i < numPoints; i++)
			{
				var p = hIndex.SortedPoints[i];
				var exact = dMeter.ExactNeighbors(p);
				var estimate = dMeter.EstimatedDensity(p, windowRadius);
				Console.Write($"{exact},{estimate}");
			}

		}
	}
}
