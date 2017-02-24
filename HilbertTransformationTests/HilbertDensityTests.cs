using System;
using System.Linq;
using Clustering;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class HilbertDensityTests
	{
		/// <summary>
		/// Compare the results of EstimateNeighbors to CountNeighbors to see how far off the estimation really is.
		/// </summary>
		[Test]
		public void DensityAccuracy()
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
			var neighborCount = 10;
			var hd = new HilbertDensity(hIndex, neighborCount);
			var differenceHistogram = new int[expectedClusters.NumPoints];
			var comparisonsToMake = 1000;
			foreach (var i in Enumerable.Range(0, comparisonsToMake))
			{
				var estimatedNeighbors = hd.EstimateNeighbors(i, neighborCount);
				var accurateNeighbors = hd.CountNeighbors(i);
				var difference = accurateNeighbors - estimatedNeighbors;
				if (difference > differenceHistogram.Length - 1)
					difference = differenceHistogram.Length - 1;
				differenceHistogram[difference]++;
			}
			for (var i = 0; i < differenceHistogram.Length; i++)
			{
				if (differenceHistogram[i] > 0)
				{
					Console.WriteLine($"{differenceHistogram[i]} points are off by {i}");
				}
			}
			Assert.AreEqual(comparisonsToMake, differenceHistogram[0], "Some estimates are wrong.");
		}
	}
}
