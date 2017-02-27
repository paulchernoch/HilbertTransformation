using System;
using System.Collections.Generic;
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
			var correlator = new KendallTauCorrelation<HilbertPoint, long>(exactMetric, estimatedMetric);
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
			var neighborhoodDistance = count.MaximumSquareDistance * 2 / 5;
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

		/// <summary>
		/// Vary the number of points and the WindowSize.
		/// </summary>
		[Test]
		public void DensityCorrelationVariations()
		{
			/*
			  Sample run: 


       Minimum Correlation per Window Radius

  N       20    40    60    80   100   150   200   300   400   500   600   700
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
  1000  .739  .799  .828  .855  .855  .924  .965  .965  .965     1     1     1
  2000  .663  .834  .885  .916  .936  .936  .947  .957  .957  .957  .957  .968
  4000  .598  .721  .774  .807  .848  .874  .885  .885  .885  .911  .916  .928
  6000   .58  .678  .735  .784   .81  .856  .872  .883  .915  .924  .924  .924
  8000  .559  .647  .709  .763  .787  .839  .865  .882  .913  .949  .957  .959
 10000  .521  .623  .689  .731  .761  .814  .843  .881  .899  .902  .902  .902
 20000   .45  .552  .621   .66  .693  .749  .805  .827  .831  .835  .846  .875
 40000  .451  .554  .617  .648  .671  .724  .764  .801  .821  .842  .858  .867
 80000  .509    .6  .639  .664  .683  .719  .739  .758  .764  .759  .755  .752
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----



       Average Correlation per Window Radius

  N       20    40    60    80   100   150   200   300   400   500   600   700
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
  1000  .843  .946  .956  .962  .966  .977  .985   .99  .991     1     1     1
  2000  .718  .862   .92  .948  .957  .965  .968  .976  .981  .985  .987  .993
  4000  .644  .753  .817  .861  .895  .931  .943  .952  .956  .964  .967   .97
  6000  .597  .702  .762  .808  .839  .893  .926   .95  .956  .959  .961  .962
  8000  .611  .702  .763    .8  .828  .874  .906  .946  .963  .971  .976  .979
 10000  .619  .705  .758  .791  .814  .859  .889  .931  .955  .962  .963  .964
 20000  .604  .684  .728  .758  .781  .822  .851  .888  .912  .932   .95  .965
 40000  .596  .671  .713   .74  .758  .793  .818  .848  .869  .887  .902  .915
 80000   .61  .678  .713  .735   .75  .777  .796  .821  .835  .847  .857  .864
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----



       Maximum Correlation per Window Radius

  N       20    40    60    80   100   150   200   300   400   500   600   700
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
  1000  .909     1     1     1     1     1     1     1     1     1     1     1
  2000  .764  .899  .943  .978  .981     1     1     1     1     1     1     1
  4000  .719  .813  .863  .903  .943  .969  .981  .985  .988  .988  .988  .991
  6000  .621  .742  .788  .838  .877  .924  .956  .993  .998     1     1     1
  8000  .678  .769  .818  .844  .865    .9  .936  .992  .998  .999     1     1
 10000   .71  .781  .809  .824  .841  .887  .925  .966  .993  .999  .999     1
 20000  .705  .762  .802  .821   .84  .871  .884  .911  .933  .956  .974  .991
 40000  .698  .756  .785  .808  .827  .851  .872  .879  .898  .914  .936  .954
 80000  .739  .788   .81  .824   .83  .844  .862  .883  .889   .91  .927  .934
------ ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----

			 */ 
			var varyWindowRadius = new[] { 20, 40, 60, 80, 100, 150, 200, 300, 400, 500, 600, 700 };
			var varyNumPoints = new[] { 1000, 2000, 4000, 6000, 8000, 10000, 20000, 40000, 80000 };
			var dimensions = 100;
			var clusterCount = 20;
			var repeats = 10;
			var results = DensityCorrelationCases(varyWindowRadius, varyNumPoints, dimensions, clusterCount, repeats);
			Console.WriteLine("\n\nDensityCorrelationVaryiations is DONE.\n");
			var minReport = CorrelationReport(varyNumPoints, varyWindowRadius, dimensions, clusterCount, results, StatType.Minimum);
			var meanReport = CorrelationReport(varyNumPoints, varyWindowRadius, dimensions, clusterCount, results, StatType.Mean);
			var maxReport = CorrelationReport(varyNumPoints, varyWindowRadius, dimensions, clusterCount, results, StatType.Maximum);

			Console.WriteLine($"       Minimum Correlation per Window Radius\n\n{minReport}\n");
			Console.WriteLine($"       Average Correlation per Window Radius\n\n{meanReport}\n");
			Console.WriteLine($"       Maximum Correlation per Window Radius\n\n{maxReport}\n");
		}

		public enum StatType { Minimum, Maximum, Mean }

		private string CorrelationReport(int[] varyNumPoints, int[] varyWindowRadius, int dimensions, int clusterCount, Dictionary<string,CorrelationStats> stats, StatType statToReport)
		{
			var report = "  N   ";
			var dashLine = "------";
			foreach (var wr in varyWindowRadius)
			{
				report += P6(wr);
				dashLine += " -----";
			}
			report += $"\n{dashLine}\n";
			foreach (var n in varyNumPoints)
			{
				report += P6(n);
				foreach (var wr in varyWindowRadius)
				{
					var label = MakeLabel(n, wr, dimensions, clusterCount);
					var corr = stats[label];
					double value;
					switch (statToReport)
					{
						case StatType.Minimum: value = corr.Min; break;
						case StatType.Maximum: value = corr.Max; break;
						default: value = corr.Mean; break;
					}
					report += FC(value).PadLeft(6);
				}
				report += "\n";
			}
			report += dashLine + "\n";
			return report;
		}

		private double DensityCorrelationCase(DensityMeter dMeter, int newWindowRadius, int numPointsToCorrelate = 1000)
		{
			dMeter.Distances.WindowRadius = newWindowRadius;
			Func<HilbertPoint, long> exactMetric = p => (long)dMeter.ExactNeighbors(p);
			Func<HilbertPoint, long> estimatedMetric = p => (long)dMeter.EstimatedDensity(p, newWindowRadius);
			var correlator = new KendallTauCorrelation<HilbertPoint, long>(exactMetric, estimatedMetric);
			var correlation = correlator.TauB(dMeter.Index.SortedPoints.Take(numPointsToCorrelate));
			return correlation;
		}

		public class CorrelationStats
		{
			public string Label { get; set; }
			public double Max { get; private set; } = double.MinValue;
			public double Min { get; private set; } = double.MaxValue;
			public double Mean { get; private set; } = 0;
			public int Count { get; private set; } = 0;
			private double Sum { get; set; } = 0;
			public CorrelationStats(string label)
			{
				Label = label;
			}
			public void Add(double correlation)
			{
				Max = Math.Max(Max, correlation);
				Min = Math.Min(Min, correlation);
				Count++;
				Sum += correlation;
				Mean = Sum / Count;
			}
			public override string ToString()
			{
				return $"[CorrelationStats for {Label}: Max={FC(Max)}, Min={FC(Min)}, Mean={FC(Mean)}, Count={Count}]";
			}
		}

		/// <summary>
		/// Format d to three decimal places.
		/// </summary>
		/// <param name="d">Number to format.</param>
		private static String FC(double d)
		{
			return String.Format("{0:.###}", d);
		}

		/// <summary>
		/// Left pad number with spaces until it is 6 characters wide.
		/// </summary>
		private static String P6(int n)
		{
			return n.ToString().PadLeft(6);
		}

		/// <summary>
		/// Left pad number with spaces until it is 4 characters wide.
		/// </summary>
		private static String P4(int n)
		{
			return n.ToString().PadLeft(4);
		}

		private Dictionary<string, CorrelationStats> DensityCorrelationCases(int[] varyWindowRadius, int[] varyNumPoints, int dimensions, int clusterCount, int repeats = 1)
		{
			var stats = new Dictionary<string, CorrelationStats>();
			for (var iRepeat = 0; iRepeat < repeats; iRepeat++)
			{
				foreach (var numPoints in varyNumPoints)
				{
					var bitsPerDimension = 10;
					var clusterSize = numPoints / clusterCount;
					var data = new GaussianClustering
					{
						ClusterCount = clusterCount,
						Dimensions = dimensions,
						MaxCoordinate = (1 << bitsPerDimension) - 1,
						MinClusterSize = clusterSize,
						MaxClusterSize = clusterSize
					};
					var expectedClusters = data.MakeClusters();
					var hIndex = new HilbertIndex(expectedClusters, bitsPerDimension);
					var cc = new ClusterCounter { NoiseSkipBy = 10, OutlierSize = 5, ReducedNoiseSkipBy = 1 };
					var count = cc.Count(hIndex.SortedPoints);
					var neighborhoodDistance = count.MaximumSquareDistance * 2 / 5;
					var dMeter = new DensityMeter(hIndex, neighborhoodDistance, varyWindowRadius[0]);

					// It is more efficient to process windowRadius in descending order, 
					// because the DistanceMemo can reuse more work that way. Once a larger window has been processed,
					// it includes all shorter windows as well.
					foreach (var windowRadius in varyWindowRadius.OrderByDescending(r => r))
					{
						var label = MakeLabel(numPoints, windowRadius, dimensions, clusterCount);
						CorrelationStats corStats;
						if (!stats.TryGetValue(label, out corStats))
						{
							corStats = new CorrelationStats(label);
							stats[label] = corStats;
						}
						corStats.Add(DensityCorrelationCase(dMeter, windowRadius));
						Console.Write(corStats);
					}
				}
			}
			return stats;
		}

		private static string MakeLabel(int numPoints, int windowRadius, int dimensions, int clusterCount)
		{
			return $"N={P6(numPoints)}, R={P4(windowRadius)}, D={dimensions}, K={clusterCount}";
		}
	}
}
