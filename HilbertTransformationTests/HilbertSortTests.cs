using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests
{
    /// <summary>
    /// Test the sorting of points using the HilbertSort class.
    /// </summary>
    [TestFixture]
    public class HilbertSortTests
    {

        /// <summary>
        /// Verify that two ways of sorting points yield the same ordering for wide clusters.
        /// </summary>
        [Test]
        public void InPlaceSort_WideClusters()
        {
            var points = TestData(20000, 50, 20, 1000000, 1000, 5000, out int bitsPerDimension);
            PointBalancer balancer = new PointBalancer(points);
            var unoptimizedSort = HilbertSort.BalancedSort(points.ToList(), ref balancer).ToArray();
            HilbertSort.SmallBalancedSort(points, ref balancer);
            CollectionAssert.AreEqual(unoptimizedSort, points, "Not in same order");
        }

        /// <summary>
        /// Verify that two ways of sorting points yield the same ordering for medium width clusters.
        /// </summary>
        [Test]
        public void InPlaceSort_MediumClusters()
        {
            var points = TestData(20000, 50, 20, 1000000, 100, 500, out int bitsPerDimension);
            PointBalancer balancer = new PointBalancer(points);
            var unoptimizedSort = HilbertSort.BalancedSort(points.ToList(), ref balancer).ToArray();
            HilbertSort.SmallBalancedSort(points, ref balancer);
            CollectionAssert.AreEqual(unoptimizedSort, points, "Not in same order");
        }

        /// <summary>
        /// Verify that two ways of sorting points yield the same ordering for narrower clusters.
        /// </summary>
        [Test]
        public void InPlaceSort_NarrowClusters()
        {
            var points = TestData(20000, 50, 20, 1000000, 10, 30, out int bitsPerDimension);
            PointBalancer balancer = new PointBalancer(points);
            var unoptimizedSort = HilbertSort.BalancedSort(points.ToList(), ref balancer).ToArray();
            HilbertSort.SmallBalancedSort(points, ref balancer);
            CollectionAssert.AreEqual(unoptimizedSort, points, "Not in same order");
        }

        /// <summary>
        /// Explore the relative cost as the number of clusters and standard deviation of the clusters vary.
        /// 
        /// A unit of cost is one bit of Hilbert curve per one point. The sort proceeds from a single bit per dimension to the maximum required bits per dimension,
        /// dropping points at each level of recursion as they are fully sorted. In aggregate, we sometimes require fewer than the maximum number of bits on average.
        /// The relative cost is the ratio between the average bits needed to the maximum required. A relative cost of one means they are identical, 0.5 means the
        /// new sort performs half as much Hilbert transformation, while 1.5 means fifty percent more. The range in this test goes from 0.54 to 1.49.
        /// </summary>
        /// <remarks>
        /// 
        /// Example run:
        /// 
        /// Clusters,Standard Deviation, Relative Cost
        /// 10,20,1.49000449124208
        /// 10,100,1.2842369123797
        /// 10,200,1.12158052471259
        /// 10,1000,0.867599536920522
        /// 10,2000,0.644931012313931
        /// 20,20,1.25127163815422
        /// 20,100,1.15538908653605
        /// 20,200,1.13116207797428
        /// 20,1000,0.797548948161104
        /// 20,2000,0.676821523228484
        /// 50,20,1.34142595606184
        /// 50,100,1.03046767887066
        /// 50,200,0.970282591174693
        /// 50,1000,0.704972975703389
        /// 50,2000,0.588519365511872
        /// 100,20,1.21454310220902
        /// 100,100,1.00827464788732
        /// 100,200,0.9004200420042
        /// 100,1000,0.66023677581864
        /// 100,2000,0.54110146764835
        /// 
        /// </remarks>
        [Test]
        public void InPlaceSortRelativeCost()
        {
            var clusters = new[] { 10, 20, 50, 100 };
            var stdDeviations = new[] { 20, 100, 200, 1000, 2000 };
            var dimensions = 50;
            var numPoints = 20000;
            var report = "Clusters,Standard Deviation,Relative Cost\n";
            foreach (var k in clusters)
                foreach (var sd in stdDeviations)
                {
                    var points = TestData(numPoints, dimensions, k, 1000000, sd, sd, out int bitsPerDimension);
                    PointBalancer balancer = new PointBalancer(points);
                    HilbertSort.SmallBalancedSort(points, ref balancer);
                    var cost = HilbertSort.RelativeSortCost;
                    report += $"{k},{sd},{cost}\n";
                }
            Console.WriteLine($"\n\nFinal report:\n\n{report}");
        }

        /// <summary>
        /// Compare the performance of two ways of sorting points: using the HilbertIndex (old way) and using the in-place HilbertSort.SmallBalancedSort (new way).
        /// </summary>
        [Test]
        public void CompareSpeedOfSorting_SmallBalancedSort_vs_HilbertIndex()
        {
            var points = TestData(20000, 50, 20, 1000000, 100, 500, out int bitsPerDimension);

            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();
            var timer3 = new Stopwatch();

            // 1. HilbertIndex
            timer1.Start();
            var hIndex = new HilbertIndex(points.Select(p => new HilbertPoint(p.Coordinates, bitsPerDimension)));
            var sortedPointsFromIndex = hIndex.SortedPoints;
            timer1.Stop();
            var hilbertIndexTime = timer1.ElapsedMilliseconds;

            // 2. HilbertSort.SmallBalancedSort
            timer2.Start();
            timer3.Start();
            PointBalancer balancer = new PointBalancer(points);
            timer3.Stop();
            HilbertSort.SmallBalancedSort(points, ref balancer);
            timer2.Stop();
            var inplaceSortTime = timer2.ElapsedMilliseconds;
            var balancerTime = timer3.ElapsedMilliseconds;

            var message = $"HilbertIndex required {hilbertIndexTime/1000.0} sec.  In-place Sort required {inplaceSortTime/1000.0} sec, of which {balancerTime/1000.0} sec is Balancer ctor.  Relative Cost = {HilbertSort.RelativeSortCost}";
            Console.WriteLine(message);
            Assert.Greater(hilbertIndexTime, inplaceSortTime, message);
        }

        /// <summary>
        /// Compare the performance of two ways of sorting points: using the HilbertIndex (old way) 
        /// and using the full-resolution, balanced HilbertSort.Sort (new way without memory savings).
        /// </summary>
        [Test]
        public void CompareSpeedOfSorting_Balanced_vs_HilbertIndex()
        {
            var points = TestData(20000, 50, 20, 1000000, 100, 500, out int bitsPerDimension);

            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();
            var timer3 = new Stopwatch();

            // 1. HilbertIndex
            timer1.Start();
            var hIndex = new HilbertIndex(points.Select(p => new HilbertPoint(p.Coordinates, bitsPerDimension)));
            var sortedPointsFromIndex = hIndex.SortedPoints;
            timer1.Stop();
            var hilbertIndexTime = timer1.ElapsedMilliseconds;

            // 2. HilbertSort.BalancedSort
            timer2.Start();
            timer3.Start();
            PointBalancer balancer = new PointBalancer(points);
            timer3.Stop();
            HilbertSort.BalancedSort(points.ToList(), ref balancer);
            timer2.Stop();
            var balancedSortTime = timer2.ElapsedMilliseconds;
            var balancerTime = timer3.ElapsedMilliseconds;

            var message = $"HilbertIndex required {hilbertIndexTime / 1000.0} sec.  Balanced Sort required {balancedSortTime / 1000.0} sec, of which {balancerTime / 1000.0} sec is Balancer ctor.  Relative Cost = {HilbertSort.RelativeSortCost}";
            Console.WriteLine(message);
            Assert.Greater(hilbertIndexTime, balancedSortTime, message);
        }

        [Test]
        public void CompareSpeedOfSorting_Unbalanced_vs_HilbertIndex()
        {
            var points = TestData(20000, 50, 20, 1000000, 100, 500, out int bitsPerDimension);

            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();

            // 1. HilbertIndex
            timer1.Start();
            var hIndex = new HilbertIndex(points.Select(p => new HilbertPoint(p.Coordinates, bitsPerDimension)));
            var sortedPointsFromIndex = hIndex.SortedPoints;
            timer1.Stop();
            var hilbertIndexTime = timer1.ElapsedMilliseconds;

            // 2. HilbertSort.Sort
            timer2.Start();

            HilbertSort.Sort(points.ToList(), bitsPerDimension);
            timer2.Stop();
            var unbalancedSortTime = timer2.ElapsedMilliseconds;

            var message = $"HilbertIndex required {hilbertIndexTime / 1000.0} sec.  Unbalanced Sort required {unbalancedSortTime / 1000.0} sec.";
            Console.WriteLine(message);
            Assert.Greater(hilbertIndexTime, unbalancedSortTime, message);
        }

        UnsignedPoint[] TestData(int numPoints, int dimensions, int clusterCount, int maxCoordinate, int minStdDeviation, int maxStdDeviation, out int bitsPerDimension)
        {
            var avgClusterSize = numPoints / clusterCount;
            var data = new GaussianClustering
            {
                ClusterCount = clusterCount,
                Dimensions = dimensions,
                MaxCoordinate = maxCoordinate,
                MinClusterSize = avgClusterSize - 100,
                MaxClusterSize = avgClusterSize + 100,
                MaxDistanceStdDev = maxStdDeviation,
                MinDistanceStdDev = minStdDeviation
            };
            var clusters = data.MakeClusters();
            var points = clusters.Points().ToArray();
            bitsPerDimension = (maxCoordinate + 1).SmallestPowerOfTwo();
            return points;
        }
    }
}
