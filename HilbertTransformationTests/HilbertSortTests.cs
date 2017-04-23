using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            var unoptimizedSort = HilbertSort.Sort(points.ToList(), ref balancer).ToArray();
            HilbertSort.Sort(points, ref balancer);
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
            var unoptimizedSort = HilbertSort.Sort(points.ToList(), ref balancer).ToArray();
            HilbertSort.Sort(points, ref balancer);
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
            var unoptimizedSort = HilbertSort.Sort(points.ToList(), ref balancer).ToArray();
            HilbertSort.Sort(points, ref balancer);
            CollectionAssert.AreEqual(unoptimizedSort, points, "Not in same order");
        }

        /// <summary>
        /// Explore the relative cost as the number of clusters and standard deviation of the clusters vary.
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
                    HilbertSort.Sort(points, ref balancer);
                    var cost = HilbertSort.RelativeSortCost;
                    report += $"{k},{sd},{cost}\n";
                }
            Console.WriteLine($"\n\nFinal report:\n\n{report}");
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
