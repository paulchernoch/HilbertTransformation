using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using HilbertTransformationTests.Data;
using HilbertTransformation;
using Clustering;

namespace HilbertTransformationTests
{
    [TestFixture]
    /// <summary>
    /// Test whether the test data generator does a good job.
    /// </summary>
    public class GaussianClusteringTests
    {
        /// <summary>
        /// Test if the average distance of a point from the center of a randomnly generated spherical cluster
        /// conforming to a Gaussian distribution is close to the expected value.
        ///    R = σ√D
        /// where sigma is the standard deviation and D the number of dimensions.
        /// </summary>
        [Test]
        public void VerifyGaussianRadius()
        {
            Logger.SetupForTests();
            int maxCoordinate = 10000;
            var N = new[] { 100, 200, 500, 1000, 2000 };
            var D = new[] { 20, 50, 100, 200, 500, 1000, 2000 };
            var SIGMAS = new[] { 100, 200, 500, 1000, 2000 };
            var failures = "";
            var failureCount = 0;
            var maxPercentile = 0.0;
            var minPercentile = 100.0;
            var withinFivePercentCount = 0;
            var totalCount = 0;
            foreach( var n in N)
                foreach(var d in D)
                    foreach(var sigma in SIGMAS)
                    {
                        var expectedRadius = sigma * Math.Sqrt(d);
                        var percentile = GaussianRadiusPercentile(n, d, maxCoordinate, sigma, expectedRadius);
                        maxPercentile = Math.Max(maxPercentile, percentile);
                        minPercentile = Math.Min(minPercentile, percentile);
                        var success = percentile >= 35 && percentile <= 75;
                        if (!success)
                        {
                            failureCount++;
                            var errorMessage = $"Wrong radius for N = {n}, D = {d}, Sigma = {sigma}. Expected R = {expectedRadius}. Percentile = {percentile}";
                            failures += errorMessage + "\n";
                            Logger.Error(errorMessage);
                        }
                        else
                        {
                            var message = $"Correct radius for N = {n}, D = {d}, Sigma = {sigma}. Expected R = {expectedRadius}. Percentile = {percentile}";
                            Logger.Info(message);
                        }
                        totalCount++;
                        if (percentile >= 45 && percentile <= 55)
                            withinFivePercentCount++;
                    }
            Logger.Info($"Percentiles ranged from {minPercentile} % to {maxPercentile} %");
            Logger.Info($"Within five percent: {withinFivePercentCount} of {totalCount} total tests");
            if (failureCount > 0)
                Logger.Error($"{failureCount} failures");
            else
                Logger.Info("NO failures!");
            Assert.AreEqual(0, failureCount, failures);
        }

        /// <summary>
        /// Generate a sperical cluster of points conforming to a Gaussian distribution,
        /// get the distances from each point to the centroid, and compute the deciles
        /// for distance.
        /// </summary>
        /// <param name="n">Number of points.</param>
        /// <param name="dimensions">Dimensions per point.</param>
        /// <param name="maxCoordinate">Largest value permitted for a coordinate.</param>
        /// <param name="sigma">Standard deviation used in the Guassian.</param>
        /// <returns>An array of eleven values.
        /// The first entry is the minimum distance from the cluster center to any point.
        /// The last entry is the maximum value.
        /// At index five is the mean.</returns>
        static double[] GaussianRadiusDeciles(int n, int dimensions, int maxCoordinate, int sigma)
        {
            var distances = GaussianRadiusDistances(n, dimensions, maxCoordinate, sigma);
            return Enumerable.Range(0, 11).Select(decile => distances[((n - 1) * decile) / 10]).ToArray();
        }

        /// <summary>
        /// Compute the percentile of distances at which the given distance falls.
        /// The distances are from points in a cluster to the center of the cluster.
        /// </summary>
        /// <param name="n">Numbner of points.</param>
        /// <param name="dimensions">Number of dimensions.</param>
        /// <param name="maxCoordinate">Maximum coordinate value.</param>
        /// <param name="sigma">Standard deviation of coordinate spread.</param>
        /// <param name="expectedRadius">Expected average distance from center to points in cluster.</param>
        /// <returns> A percentile value, from zero to one hundred.</returns>
        static double GaussianRadiusPercentile(int n, int dimensions, int maxCoordinate, int sigma, double expectedRadius)
        {
            var distances = GaussianRadiusDistances(n, dimensions, maxCoordinate, sigma);
            var position = distances.BinarySearch(expectedRadius);
            if (position < 0) position = ~position;
            return 100.0 * position / n;
        }

        static List<double> GaussianRadiusDistances(int n, int dimensions, int maxCoordinate, int sigma)
        {
            var center = Enumerable.Range(0, dimensions).Select(i => maxCoordinate / 2).ToArray();
            var deviations = Enumerable.Range(0, dimensions).Select(i => (double)sigma).ToArray();
            var affectedIndices = Enumerable.Range(0, dimensions).ToArray();
            var generator = new EllipsoidalGenerator(center, deviations, affectedIndices);
            var tempPoint = new int[dimensions];
            var centerPoint = new UnsignedPoint(center);
            var points = Enumerable.Range(0, n).Select(i => new UnsignedPoint(generator.Generate(tempPoint))).ToList();
            var distances = points.Select(p => centerPoint.Distance(p)).OrderBy(dist => dist).ToList();
            return distances;
        }
    }
}
