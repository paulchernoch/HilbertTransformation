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
    [TestFixture]
    public class GridCoarsenessTests
    {
        /// <summary>
        /// Test coarseness using low standard deviation (from 10 to 30) for 20,000 points with 50 dimensions in 20 clusters.
        /// 
        /// With a low standard deviation, more bits are required to achieve low coarseness than the case with a larger standard deviation.
        /// </summary>
        [Test]
        public void Coarseness_20000N_50D_20K_1000000M_LowSD()
        {
            var n = 20000;
            var dimensions = 50;
            var clusterCount = 20;
            var maxCoordinate = 1000000;
            CoarsenessCase(n, dimensions, clusterCount, maxCoordinate);
        }

        /// <summary>
        /// Test coarseness using moderate standard deviation (from 100 to 500) for 20,000 points with 50 dimensions in 20 clusters.
        /// 
        /// With a moderate standard deviation, fewer bits are required to achieve low coarseness than the case with a lower standard deviation.
        /// </summary>
        [Test]
        public void Coarseness_20000N_50D_20K_1000000M_MediumSD()
        {
            var n = 20000;
            var dimensions = 50;
            var clusterCount = 20;
            var maxCoordinate = 1000000;
            CoarsenessCase(n, dimensions, clusterCount, maxCoordinate, 100, 500);
        }

        /// <summary>
        /// Compare the exact coarseness with an estimate for all numbers of bits.
        /// 
        /// This takes an assemblage of many clusters and finds the most concentrated
        /// cluster according to a single bit Hilbert curve. Then it tries to see
        /// how coarse different grid sizes around just that one cluster. Why?
        /// Because the full set of points is balanced, but it is unlikely that a single
        /// cluster is balanced.
        /// </summary>
        /// <param name="numPoints">Number of points</param>
        /// <param name="dimensions">Number of dimensions</param>
        /// <param name="clusterCount">Number of clusters</param>
        /// <param name="maxCoordinate">Larges value any cooedinate of any dimension can hold</param>
        /// <param name="maxStdDeviation">Maximum standard deviation among coordinate values relative to the center of each Gaussian cluster generated.</param>
        /// <param name="minStdDeviation">Maximum standard deviation among coordinate values relative to the center of each Gaussian cluster generated.</param>
        void CoarsenessCase(int numPoints, int dimensions, int clusterCount, int maxCoordinate, int minStdDeviation = 10, int maxStdDeviation = 30)
        {
            var grid = MakeTestGrid(numPoints, dimensions, clusterCount, maxCoordinate, minStdDeviation, maxStdDeviation);
            var exactCoarseness = grid.Coarseness();
            grid.Clear();
            var estimatedCoarseness = grid.Coarseness(2 * grid.Count);
            for (var i = 1; i < grid.BitsPerDimension; i++)
            {
                var msg = $"{i} Bits: Exact coarseness = {exactCoarseness[i]}  Estimated = {estimatedCoarseness[i]}";

                Console.WriteLine(msg);
                var difference = Math.Abs(exactCoarseness[i] - estimatedCoarseness[i]);
                Assert.IsTrue(difference < 0.015, msg);
            }
        }

        /// <summary>
        /// Shows that when compared to the small dispersion case, when clusters are less compact, FEWER bits are needed to divide them into ten parts.
        /// </summary>
        [Test]
        public void BitsToDivide_20000N_50D_20K_1000000M_MediumSD()
        {
            var n = 20000;
            var dimensions = 50;
            var clusterCount = 20;
            var maxCoordinate = 1000000;
            BitsToDivideCase(0.10, 8, n, dimensions, clusterCount, maxCoordinate, 100, 500);
        }

        /// <summary>
        /// Shows that when compared to the medium dispersion case, when clusters are compact, MORE bits are needed to divide them into ten parts.
        /// </summary>
        [Test]
        public void BitsToDivide_20000N_50D_20K_1000000M_LowSD()
        {
            var n = 20000;
            var dimensions = 50;
            var clusterCount = 20;
            var maxCoordinate = 1000000;
            BitsToDivideCase(0.10, 12, n, dimensions, clusterCount, maxCoordinate);
        }

        void BitsToDivideCase(double fractionWanted, int expectedBits, int numPoints, int dimensions, int clusterCount, int maxCoordinate, int minStdDeviation = 10, int maxStdDeviation = 30)
        {
            var grid = MakeTestGrid(numPoints, dimensions, clusterCount, maxCoordinate, minStdDeviation, maxStdDeviation);
            var sizeWanted = (int) (grid.Count * fractionWanted);
            var actualBits = grid.BitsToDivide(sizeWanted, 2 * grid.Count);
            Assert.GreaterOrEqual(expectedBits, actualBits, $"For fraction {fractionWanted}, Expected no more than {expectedBits} bits, actual was {actualBits}");
        }

        /// <summary>
        /// Compare the exact coarseness with an estimate for all numbers of bits.
        /// 
        /// This takes an assemblage of many clusters and finds the most concentrated
        /// cluster according to a single bit Hilbert curve. 
        /// Then it composes a GridCoarseness for the points in that cluster.
        /// </summary>
        /// <param name="numPoints">Number of points</param>
        /// <param name="dimensions">Number of dimensions</param>
        /// <param name="clusterCount">Number of clusters</param>
        /// <param name="maxCoordinate">Larges value any cooedinate of any dimension can hold</param>
        /// <param name="maxStdDeviation">Maximum standard deviation among coordinate values relative to the center of each Gaussian cluster generated.</param>
        /// <param name="minStdDeviation">Maximum standard deviation among coordinate values relative to the center of each Gaussian cluster generated.</param>
        /// <returns>The GridCoarseness.</returns>
        GridCoarseness MakeTestGrid(int numPoints, int dimensions, int clusterCount, int maxCoordinate, int minStdDeviation = 10, int maxStdDeviation = 30)
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
            var points = clusters.Points().ToList();
            PointBalancer balancer = null;
            var bitsRequired = (maxCoordinate + 1).SmallestPowerOfTwo();

            var lowresSort = HilbertSort.SortWithTies(points, 1, ref balancer);
            var largestBucket = lowresSort.OrderByDescending(bucket => bucket.Length).FirstOrDefault();
            var bucketSize = largestBucket.Length;

            var grid = new GridCoarseness(largestBucket, bitsRequired);
            return grid;
        }
    }
}
