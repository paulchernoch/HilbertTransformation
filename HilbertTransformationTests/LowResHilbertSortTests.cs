using Clustering;
using HilbertTransformation;
using HilbertTransformation.Random;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class LowResHilbertSortTests
    {
        /// <summary>
        /// Sort test points two ways: by a high resolution (many bits) Hilbert ordering and
        /// by a low resolution (1-bit) Hilbert ordering. If two points are not tied
        /// in the lowres ordering, their relative positions should be in the same order.
        /// 
        /// If this test fails, a major optimization is unworkable. 
        /// </summary>
        [Test]
        public void LowresVersusHires_10000N_50D_20K_2B()
        {
            LowresVersusHiresCase(10000, 50, 20, 2);
        }


        public void LowresVersusHiresCase(int numPoints, int dimensions, int clusterCount, int lowresBits)
        {
            var maxCoordinate = 1000;
            var clusterSizeVariation = 100;
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
            var clusters = data.MakeClusters();
            var points = clusters.Points().ToList();
            PointBalancer balancer = null;

            var hiresSort = HilbertSort.BalancedSort(points, ref balancer);
            var lowresSort = HilbertSort.SortWithTies(points, lowresBits, ref balancer);

            var lowresPositions = new Dictionary<UnsignedPoint, int>();
            var hiresPosition = new Dictionary<UnsignedPoint, int>();

            foreach (var p in hiresSort.Select((p, i) => { hiresPosition[p] = i; return p; })) ;
            foreach (var ties in lowresSort.Select((p, i) => new { Points = p, Position = i }))
            {
                foreach (var point in ties.Points)
                {
                    lowresPositions[point] = ties.Position;
                }
            }

            //      Compare the positions of many pairs of points in the two orderings to see that 
            //      they are either in the same relative order
            //      or tied for position in the lowres ordering.
            var actualNumPoints = points.Count;
            var largestBucket = lowresSort.Select(bucket => bucket.Length).Max();
            var caseDescription = $"N = {actualNumPoints}  D = {dimensions}  K = {clusterCount}  B = {lowresBits}";
            Console.WriteLine(caseDescription);
            Console.WriteLine($"Lowres buckets = {lowresSort.Count}  Largest bucket = {largestBucket}");

            int outOfPlaceCount = 0;
            for (var i = 0; i < actualNumPoints - 1; i++)
            {
                var p1 = points[i];
                for (var j = i + 1; j < actualNumPoints; j++)
                {
                    var p2 = points[j];
                    var lowresPosition1 = lowresPositions[p1];
                    var lowresPosition2 = lowresPositions[p2];
                    var hiresPosition1 = hiresPosition[p1];
                    var hiresPosition2 = hiresPosition[p2];
                    if (lowresPosition1 != lowresPosition2)
                    {
                        if (lowresPosition1 < lowresPosition2 != hiresPosition1 < hiresPosition2)
                        {
                            outOfPlaceCount++;
                        }
                    }
                }
            }
            var msg = $"Out of place count = {outOfPlaceCount}";
            Console.WriteLine(msg);
            Assert.AreEqual(0, outOfPlaceCount, msg);
        }

        [Test]
        public void UniquenessVaryingBits_10000N_50D_20K_1000M()
        {
            ClusteredUniquenessByBits(10000, 50, 20, 200, 1000);
        }

        [Test]
        public void UniquenessVaryingBits_100000N_50D_50K_1000M()
        {
            var n = 100000;
            var smallBucketSize = (int)(2 * Sqrt(n));
            ClusteredUniquenessByBits(n, 50, 50, smallBucketSize, 1000);
        }

        [Test]
        public void UniquenessVaryingBits_40000N_50D_20K_100000M()
        {
            var n = 40000;
            var smallBucketSize = (int)(2 * Sqrt(n));
            ClusteredUniquenessByBits(n, 50, 20, smallBucketSize, 100000);
        }

        [Test]
        public void UniformUniquenessVaryingBits_40000N_50D_100000M()
        {
            var n = 40000;
            var smallBucketSize = (int)(2 * Sqrt(n));
            UniformUniquenessByBits(n, 50, smallBucketSize, 100000);
        }

        /// <summary>
        /// For random clustered data, discover how unique shortened versions of the Hilbert index are.
        /// </summary>
        /// <param name="numPoints">Number of points.</param>
        /// <param name="dimensions">Dimensions per point.</param>
        /// <param name="clusterCount">Number of clusters.</param>
        /// <param name="smallBucketSize">Count of items that constitutes a small bucket.</param>
        /// <param name="maxCoordinate">Highest permitted coordinate value.</param>
        public void ClusteredUniquenessByBits(int numPoints, int dimensions, int clusterCount, int smallBucketSize, int maxCoordinate)
        {
            var clusterSizeVariation = 100;
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
            var clusters = data.MakeClusters();
            var points = clusters.Points().ToList();
            PointBalancer balancer = null;
            var bitsRequired = (maxCoordinate + 1).SmallestPowerOfTwo();
            for (var iBits = 1; iBits <= bitsRequired; iBits++)
            {
                var maxBucketSize = MaxBucketSizePerBits(points, iBits, smallBucketSize, ref balancer, out int pointsInSmallBuckets);
                var pctInSmallBuckets = 100.0 * pointsInSmallBuckets / points.Count;
                Console.WriteLine($"Bits: {iBits}  Max Bucket: {maxBucketSize}  # in Small Buckets: {pointsInSmallBuckets} - {pctInSmallBuckets} %");
            }
        }


        /// <summary>
        /// For uniformly random data, discover how unique shortened versions of the Hilbert index are.
        /// </summary>
        /// <remarks>
        /// The results from this test show that for truly random data, every point (or almost every point)
        /// ends up in its own bucket even if only one bit per dimension is used.
        /// </remarks>
        /// <param name="numPoints">Number of points.</param>
        /// <param name="dimensions">Dimensions per point.</param>
        /// <param name="smallBucketSize">Count of items that constitutes a small bucket.</param>
        /// <param name="maxCoordinate">Highest permitted coordinate value.</param>
        public void UniformUniquenessByBits(int numPoints, int dimensions, int smallBucketSize, int maxCoordinate)
        {
            var points = UniformRandomPoints(numPoints, dimensions, maxCoordinate);
            PointBalancer balancer = null;
            var bitsRequired = (maxCoordinate + 1).SmallestPowerOfTwo();
            var maxBucketSize = new int[bitsRequired];
            for (var iBits = 1; iBits <= bitsRequired; iBits++)
            {
                maxBucketSize[iBits-1] = MaxBucketSizePerBits(points, iBits, smallBucketSize, ref balancer, out int pointsInSmallBuckets);
                var pctInSmallBuckets = 100.0 * pointsInSmallBuckets / points.Count;
                Console.WriteLine($"Bits: {iBits}  Max Bucket: {maxBucketSize[iBits-1]}  # in Small Buckets: {pointsInSmallBuckets} - {pctInSmallBuckets} %");
            }
            Assert.LessOrEqual(maxBucketSize[0], 2, $"Even a one-bit Hilbert curve should be enough to distinguish random points, but maxBucketSize is {maxBucketSize[0]}");
        }

        public int MaxBucketSizePerBits(List<UnsignedPoint> points, int lowresBits, int smallBucketSize, ref PointBalancer balancer, out int pointsInSmallBuckets)
        {
            balancer = balancer ?? new PointBalancer(points);
            var dimensions = points[0].Dimensions;
            var lowresSort = HilbertSort.SortWithTies(points, lowresBits, ref balancer);
            var lowresPositions = new Dictionary<UnsignedPoint, int>();
            foreach (var ties in lowresSort.Select((p, i) => new { Points = p, Position = i }))
            {
                foreach (var point in ties.Points)
                {
                    lowresPositions[point] = ties.Position;
                }
            }

            //      Compare the positions of many pairs of points in the two orderings to see that 
            //      they are either in the same relative order
            //      or tied for position in the lowres ordering.
            var actualNumPoints = points.Count;
            var largestBucket = lowresSort.Select(bucket => bucket.Length).Max();
            pointsInSmallBuckets = lowresSort.Select(bucket => bucket.Length > smallBucketSize ? 0 : bucket.Length).Sum();
            var caseDescription = $"N = {actualNumPoints}  D = {dimensions}  B = {lowresBits}";
            //Console.WriteLine(caseDescription);
            //Console.WriteLine($"Buckets: Count = {lowresSort.Count}  Largest = {largestBucket}  Points in Small = {pointsInSmallBuckets}");
            return largestBucket;
        }

        /// <summary>
        /// Generate random points whose coordinates are uniformly distributed between zero and maxCoordinate.
        /// </summary>
        /// <param name="n">Number of points to generate.</param>
        /// <param name="dimensions">Dimensions for each point.</param>
        /// <param name="maxCoordinate">All coordinate values will range between zero and maxCoordinate (inclusive).</param>
        /// <returns>The random points.</returns>
        private List<UnsignedPoint> UniformRandomPoints(int n, int dimensions, int maxCoordinate)
        {
            var rng = new FastRandom();
            return Enumerable.Range(0, n)
                .Select(i => {
                    return new UnsignedPoint(
                        Enumerable
                            .Range(0, dimensions)
                            .Select(j => (uint) rng.Next(0, maxCoordinate+1))
                            .ToArray()
                    );
                })
                .ToList();
        }

    }
}
