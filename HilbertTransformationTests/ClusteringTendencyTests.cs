using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System.Linq;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class ClusteringTendencyTests
    {
        /// <summary>
        /// Uniformly random points should exhibit no clustering tendency.
        /// </summary>
        [Test]
        public void UnclusteredPoints()
        {
            var points = TestDataHelper.UniformRandomPoints(20000, 100, 1000000);
            var ct = new ClusteringTendency(points, 5);
            Assert.AreEqual(ClusteringTendency.ClusteringQuality.Unclustered, ct.HowClustered, $"Data should be unclustered, but was: {ct}");
        }

        /// <summary>
        /// Test on data where all points are in clusters and no cluster has the majority of points.
        /// </summary>
        [Test]
        public void AllClustered()
        {
            // Fifty clusters all the same size.
            var clusteredPoints = TestData(Enumerable.Range(1, 50).Select(i => 100).ToArray(), 100, 1000000);
            var points = clusteredPoints.Points().ToList();
            var ct = new ClusteringTendency(points, 5);
            Assert.AreEqual(ClusteringTendency.ClusteringQuality.HighlyClustered, ct.HowClustered, $"Data should be HighlyClustered, but was: {ct}");
        }

        /// <summary>
        /// Test on data where all points are in one big cluster or are outliers.
        /// </summary>
        [Test]
        public void SingleOrMajorityCluster()
        {
            // Many clusters with one point and one with a whole lot.
            var clusteredPoints = TestData(Enumerable.Range(1, 1000).Select(i => i == 1 ? 10000 : 1).ToArray(), 100, 1000000);
            var points = clusteredPoints.Points().ToList();
            var ct = new ClusteringTendency(points, 5);
            var acceptable = ct.HowClustered == ClusteringTendency.ClusteringQuality.SinglyClustered
                          || ct.HowClustered == ClusteringTendency.ClusteringQuality.MajorityClustered
                          || ct.HowClustered == ClusteringTendency.ClusteringQuality.HighlyClustered;
            Assert.IsTrue(acceptable, $"Data was: {ct}");
        }

        /// <summary>
        /// Test on data where over two-thirds of points are outliers, but there are some large clusters.
        /// </summary>
        [Test]
        public void WeaklyClustered()
        {
            // Mostly outliers with a few large clusters.
            var clusteredPoints = TestData(Enumerable.Range(1, 3000).Select(i => i <= 5 ? 100 : 1).ToArray(), 100, 1000000);
            var points = clusteredPoints.Points().ToList();
            var ct = new ClusteringTendency(points, 5);
            Assert.AreEqual(ClusteringTendency.ClusteringQuality.WeaklyClustered, ct.HowClustered, $"Data should be WeaklyClustered, but was: {ct}");
        }

        private Classification<UnsignedPoint,string> TestData(int[] clusterSizes, int dimensions, int maxCoordinate)
        {
            var clusterCount = clusterSizes.Length;
            var minClusterSize = clusterSizes.Min();
            var maxClusterSize = clusterSizes.Max();
            var data = new GaussianClustering
            {
                ClusterCount = clusterCount,
                Dimensions = dimensions,
                MaxCoordinate = maxCoordinate,
                MinClusterSize = minClusterSize,
                MaxClusterSize = maxClusterSize,
                ClusterSizes = clusterSizes
            };
            return data.MakeClusters();
        }
    }
}
