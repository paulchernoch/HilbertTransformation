using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HilbertTransformation;
using HilbertTransformation.Random;
using static System.Math;

namespace Clustering
{
    /// <summary>
    /// Find within the given budget that Permutation and resulting ordering of points which appears to divide the points into 
    /// the smallest number of clusters (excluding outliers).
    /// The points are transformed into HilbertPoints using that permutation and then sorted in Hilbert curve order.
    /// 
    /// Many Permutations are randomly generated and tested on the same set of points. Each permutation will cause the 
    /// points to be sorted in a different order. Some of these curves are better than others at visiting
    /// a cluster of points, moving from point to point within the cluster, then advancing to the next cluster
    /// without doubling back later to a cluster already visited.
    /// 
    /// If the curve revisits a cluster multiple times after visiting other clusters, then that index is fragmented.
    /// The more fragmented an index is, the poorer the performance when we do an exhaustive construction 
    /// of the clusters. Whatever measure is used of quality should correlate well with the number of
    /// stripes found in the curve. For example, if there are ten true clusters but the curve finds twenty
    /// clusters, our fragmentation factor is two.
    /// 
    /// The metric used to evaluate the quality of the many indices to be tested may be supplied by the caller,
    /// as well as the strategy for randomizing the permutations used.
    /// </summary>
    /// <remarks>NOTE: This class is a rewrite of OptimalIndex which uses less memory than that class.
    /// We no longer convert UnsignedPoints into HilbertPoints at all, just sort by the Hilbert position (BigInteger), 
    /// which saves memory.</remarks>
    public class OptimalPermutation
    {
        /// <summary>
        /// Provides the results of the search.
        /// </summary>
        public class PermutationFound : IComparable<PermutationFound>
        {
            public Permutation<uint> PermutationUsed { get; private set; }

            /// <summary>
            /// The original points sorted according to a Hilbert curve that was generated using PermutationUsed.
            /// </summary>
			public List<UnsignedPoint> SortedPoints { get; private set; }

            /// <summary>
            /// Obtains the Ids of the points sorted according to the Hilbert curve order.
            /// </summary>
            public IEnumerable<int> SortedPointIndices
            {
                get { return SortedPoints.Select(p => p.UniqueId); }
            }

            public int EstimatedClusterCount { get; private set; }

            /// <summary>
            /// The maximum distance between points (other than outliers) that should be clustered together.
            /// </summary>
            public long MergeSquareDistance { get; set; }

            public PermutationFound(Permutation<uint> permutation, List<UnsignedPoint> index, int estimatedClusterCount, long mergeSquareDistance)
            {
                PermutationUsed = permutation;
                SortedPoints = index;
                EstimatedClusterCount = estimatedClusterCount;
                MergeSquareDistance = mergeSquareDistance;
            }

            /// <summary>
            /// Return true if this result is better than the other result.
            /// </summary>
            /// <param name="other">Other result for comparison.</param>
            /// <returns>True if an improvement was found.
            /// False if no improvement was found.
            /// </returns>
            public bool IsBetterThan(PermutationFound other) => CompareTo(other) < 0;

            public int CompareTo(PermutationFound other) => EstimatedClusterCount.CompareTo(other.EstimatedClusterCount);

            public override string ToString() => $"[PermutationFound: K={EstimatedClusterCount} clusters,  L={MergeSquareDistance}]";
            
        }

        #region Attributes: Metric, PermutationStrategy, ParallelTrials, MaxIterations, MaxIterationsWithoutImprovement

        public int BitsPerDimension { get; set; }

        /// <summary>
        /// Scores how well a given ordering of points measures up. The lower the score, the better. 
        /// A low score is assumed to mean less fragmentation of the index.
        /// Also derives the MergeSquareDistance.
        /// 
        /// NOTE: This delegate must be threadsafe!
        /// </summary>
        Func<IReadOnlyList<UnsignedPoint>, Tuple<int, long>> Metric { get; set; }

        /// <summary>
        /// Strategy to use for choosing a new permutation, given the previous best permutation,
        /// the number of dimensions for each point and the iteration number. 
        /// As the iteration number increases, it is pribably better to permute
        /// fewer coordinates and home in on a solution.
        /// 
        /// The second parameter is the number of dimensions per point.
        /// The third parameter is the iteration number.
        /// </summary>
        Func<Permutation<uint>, int, int, Permutation<uint>> PermutationStrategy { get; set; }

        /// <summary>
        /// Number of independent trials run in parallel using different permutations derived from the same
        /// starting permutation. It is not profitable for this to exceed the number of processors.
        /// </summary>
        public int ParallelTrials { get; set; } = 4;

        /// <summary>
        /// Maximum number of iterations to perform before stopping.
        /// 
        /// The most total trials that will be done is ParallelTrials * MaxIterations.
        /// </summary>
        public int MaxIterations { get; set; } = 10;

        /// <summary>
        /// If several iterations are performed with no improvement (i.e. reduction) in score,
        /// searching will halt.
        /// </summary>
        public int MaxIterationsWithoutImprovement { get; set; } = 3;

        /// <summary>
        /// If true, make decisions based on optimizing a sample of the points, not all the points.
        /// If false, optimize based on indices composed of all the points.
        /// 
        /// NOTE: The sample size will be determined after creating a first index with all the points.
        ///       It will be chosen such that:
        ///           S * (N / K) ≥ 100
        ///       where:
        ///          S is the sample fraction (from zero to one)
        ///          N is the total number of points (unsampled)
        ///          K is the first estimate of the cluster count.
        ///       Assuming that some clusters are half the size of the mean and others are twice the mean,
        ///       this means that no cluster will end up with fewer than 50 points, which should be large enough
        ///       to resolve the key features of the data. Even if some clusters are smaller than half the mean, 
        ///       the estimaetd K is usually 1.5x to 3x larger than the true K. 
        /// </summary>
        public bool UseSample { get; set; } = false;

        private int LowestCountSeen { get; set; } = int.MaxValue;

        #endregion

        /// <summary>
        /// A simple strategy that scrambles all the coordinates the first iteration,
        /// then half the coordinates the next iteration, 
        /// then a quarter the dimensions the next iteration, etc until five dimensions are reached.
        /// </summary>
        public static Func<Permutation<uint>, int, int, Permutation<uint>> ScrambleHalfStrategy =
            (previousPermutation, dimensions, iteration) =>
            {
                // Assume that iteration is zero-based.
                var dimensionsToScramble = Max(Min(dimensions, 5), dimensions / (1 << iteration));
                return previousPermutation.Scramble(dimensionsToScramble);
            };

        /// <summary>
        /// A simple strategy that scrambles all the coordinates the first iteration,
        /// then seventy percent of the coordinates the next iteration, 
        /// then half the dimensions the next iteration, etc until five dimensions are reached.
        /// </summary>
        public static Func<Permutation<uint>, int, int, Permutation<uint>> ScrambleSeventyStrategy =
            (previousPermutation, dimensions, iteration) =>
            {
                // Assume that iteration is zero-based, so that the first iteration is 100% of the points.
                var dimensionsToScramble = (int) Round(Max(Min(dimensions, 5), dimensions * Pow(0.7, iteration)));
                return previousPermutation.Scramble(dimensionsToScramble);
            };

        #region Constructors

        /// <summary>
        /// Create an optimizer which finds the curve that minimizes the number of clusters found using means
        /// supplied by the caller.
        /// </summary>
        /// <param name="metric">Evaluates the quality of the HilbertIndex derived using a given permutation.</param>
        /// <param name="strategy">Strategy to employ that decides how many dimensions to scramble during each iteration.</param>
        /// <param name="bitsPerDimension">Bits per dimension, which MUST BE POSITIVE. It must be stated, it cannot be derived here.</param>
        public OptimalPermutation(Func<IReadOnlyList<UnsignedPoint>, Tuple<int, long>> metric, Func<Permutation<uint>, int, int, Permutation<uint>> strategy, int bitsPerDimension)
        {
            Metric = metric;
            PermutationStrategy = strategy;
            BitsPerDimension = bitsPerDimension;
        }

        /// <summary>
        /// Create an optimizer which finds the curve that minimizes the number of clusters found using a ClusterCounter.
        /// </summary>
        /// <param name="outlierSize">OutlierSize to use with the ClusterCounter.</param>
        /// <param name="noiseSkipBy">NoiseSkipBy to use with the ClusterCounter.</param>
        /// <param name="reducedNoiseSkipBy">ReducedNoiseSkipBy to use with the ClusterCounter.</param>
        /// <param name="strategy">Strategy to employ that decides how many dimensions to scramble during each iteration.</param>
        /// <param name="bitsPerDimension">Bits per dimension which MUST BE STATED, not given as -1.</param>
        public OptimalPermutation(int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy, Func<Permutation<uint>, int, int, Permutation<uint>> strategy, int bitsPerDimension)
        {
            var maxOutliers = outlierSize;
            var skip = noiseSkipBy;
            Metric = (IReadOnlyList<UnsignedPoint> sortedPoints) =>
            {
                var counter = new ClusterCounter
                {
                    OutlierSize = maxOutliers,
                    NoiseSkipBy = skip,
                    ReducedNoiseSkipBy = reducedNoiseSkipBy,
                    LowestCountSeen = LowestCountSeen
                };
                var counts = counter.Count(sortedPoints);
                return new Tuple<int, long>(counts.CountExcludingOutliers, counts.MaximumSquareDistance);
            };
            PermutationStrategy = strategy;
            BitsPerDimension = bitsPerDimension;
        }

        #endregion

        /// <summary>
        /// Search many different permutations of the dimensions, and
        /// keep the one yielding the best Metric, which is likely the one that estimates the lowest value 
        /// for the number of clusters.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="startingPermutation">Starting permutation.</param>
        /// <returns>The best index found and the permutation that generated it.</returns>
        public PermutationFound Search(IReadOnlyList<UnsignedPoint> points, Permutation<uint> startingPermutation = null) => SearchMany(points, 1, startingPermutation).First();

        /// <summary> Avoid running out of memory when evaluating multiple indices in parallel by reducing the parallelism. </summary>
        /// <returns>The max degrees of parallelism.</returns>
        /// <param name="points">Points.</param>
        private static int EstimateMaxDegreesOfParallelism(IReadOnlyList<UnsignedPoint> points)
        {
            var n = points.Count();
            var d = points[0].Dimensions;
            // OptimalPermutation uses less memory than OptimalIndex, so we can increase the limit from 24,000,000 to 50,000,000
            //   If largest value used for BitsPerDimension (32) then approximate memory overhead (in multiples of the data size for the UnsignedPoints):
            //      Processors  OptimalIndex  OptimalPermutation
            //      ----------  ------------  ------------------
            //          1           3                 2
            //          2           5                 3
            //          3           7                 4
            //          4           9                 5  
            var limit = 50000000;
            var maxDegrees = limit / (n * d);
            maxDegrees = Min(4, Max(1, maxDegrees));
            return maxDegrees;
        }

        /// <summary>
        /// Approximate number of bytes of storage required for the point and its Hilbert transform, assuming a 64-bit application.
        /// The actual number will be higher than this, but not by much.
        /// </summary>
        /// <param name="numPoints">Number of points.</param>
        /// <param name="dimensions">Number of dimensions for each point.</param>
        /// <param name="bitsPerDimension">Bits used to encode each dimension.</param>
        /// <returns>Approximate number of bytes of storage required.</returns>
        private static long ProblemSize(int numPoints, int dimensions, int bitsPerDimension)
        {
            return ((long)numPoints * dimensions * (bitsPerDimension + 32) / 8L) + (64 * 2 * numPoints);
        }

        /// <summary>
        /// Search many Hilbert orderings of the points, each based on a different permutation of the dimensions, and
        /// keep the ones yielding the best Metrics, likely those that estimate the lowest values
        /// for the number of clusters.
        /// </summary>
        /// <param name="points">Points to index.</param>
        /// <param name="indexCount">Number of the best indices to return. 
        /// For example, if this is 10, then the 10 indices with the lowest scores will be kept.</param>
        /// <param name="startingPermutation">Starting permutation.</param>
        /// <returns>The best indices found and the permutations that generated them.
        /// THe first item in the returned list is the best of the best, and the last is the worst of the best.</returns>
        public IList<PermutationFound> SearchMany(IReadOnlyList<UnsignedPoint> points, int indexCount, Permutation<uint> startingPermutation = null)
        {
            if (points.Count() < 10)
                throw new ArgumentException("List has too few elements", nameof(points));
            var queue = new BinaryHeap<PermutationFound>(BinaryHeapType.MaxHeap, indexCount);
            int dimensions = points[0].Dimensions;

            if (startingPermutation == null)
                startingPermutation = new Permutation<uint>(dimensions);
            List<UnsignedPoint> firstCurve;
            if (ProblemSize(points.Count, dimensions, BitsPerDimension) < 1500000000L)
                firstCurve = HilbertSort.Sort(points, BitsPerDimension, startingPermutation);
            else
            {
                // Used for larger problems.
                firstCurve = SmallBucketSort<UnsignedPoint>.Sort(points, point => point.Coordinates.HilbertIndex(BitsPerDimension));
            }

            // Measure our first index, then loop through random permutations 
            // looking for a better one, always accumulating the best in results.
            var metricResults = Metric(firstCurve);
            var bestResults = new PermutationFound(startingPermutation, firstCurve, metricResults.Item1, metricResults.Item2);
        
            LowestCountSeen = Min(LowestCountSeen, bestResults.EstimatedClusterCount);
            Logger.Info($"Cluster count Starts at: {bestResults}");
            var startingCount = bestResults.EstimatedClusterCount;
            if (MaxIterations <= 1)
                return new List<PermutationFound> { bestResults };
            queue.AddRemove(bestResults);

            // Decide if we are to sample points or use them all
            var sampledPoints = points;
            var sampleSize = points.Count();
            if (UseSample)
            {
                sampleSize = SampleSize(points, bestResults.EstimatedClusterCount);
                sampledPoints = Sample(points, sampleSize);
                Logger.Info($"    Sample is {sampleSize} of {points.Count} points");
            }
            var rejectedSampleSizes = new HashSet<int>();

            var iterationsWithoutImprovement = 0;
            var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = EstimateMaxDegreesOfParallelism(sampledPoints) };

            List<Permutation<uint>> allPermutations = null;

            // If the number of dimensions is small, we might waste time trying the same randomly chosen permutations mutiple times.
            // Instead, we will try all or many of them in order.
            if (dimensions <= 7)
                allPermutations = Permutation<uint>.AllPermutations(dimensions).ToList();

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                var improvedCount = 0;
                var startFromPermutation = bestResults.PermutationUsed;
                Parallel.For(0, ParallelTrials, parallelOpts,
                    i =>
                    {
                        Permutation<uint> permutationToTry;
                        // This locking is needed because we use a static random number generator to create a new permutation.
                        // It is more expensive to make the random number generator threadsafe than to make this loop threadsafe.
                        if (dimensions > 7)
                            lock (startFromPermutation)
                            {
                                permutationToTry = PermutationStrategy(startFromPermutation, dimensions, iteration);
                            }
                        else
                        {
                            lock (allPermutations)
                            {
                                if (!allPermutations.Any())
                                    return;
                                permutationToTry = allPermutations.Last();
                                allPermutations.RemoveAt(allPermutations.Count - 1);
                            }
                        }
                        IReadOnlyList<UnsignedPoint> sampledPointsToUse;
                        lock (points)
                        {
                            sampledPointsToUse = sampledPoints;
                        }
                        var curveToTry = HilbertSort.Sort(sampledPointsToUse, BitsPerDimension, permutationToTry);
                        metricResults = Metric(curveToTry);
                        var resultsToTry = new PermutationFound(permutationToTry, curveToTry, metricResults.Item1, metricResults.Item2);
                        lock (queue)
                        {
                            if (resultsToTry.EstimatedClusterCount < startingCount / 4
                            && UseSample && sampleSize != points.Count())
                            {
                                // If the cluster count has improved too much and we are sampled,
                                // reject it and increase the sample size.
                                // Why? If the clusters are irregular, sampling can break
                                // them into so many small pieces that most points end up in outliers.
                                // This leads to a false low count.
                                if (!rejectedSampleSizes.Contains(curveToTry.Count))
                                {
                                    sampleSize = Math.Min(points.Count(), 3 * curveToTry.Count / 2);
                                    Logger.Info($"Increasing sample size to {sampleSize} because estimated K = {resultsToTry.EstimatedClusterCount} (not trusted)");
                                    var newSampledPoints = Sample(points, sampleSize);
                                    lock (points)
                                    {
                                        sampledPoints = newSampledPoints;
                                    }
                                    rejectedSampleSizes.Add(curveToTry.Count);
                                }
                            }
                            else
                            {
                                queue.AddRemove(resultsToTry);
                                var improved = resultsToTry.IsBetterThan(bestResults);
                                if (improved)
                                {
                                    bestResults = resultsToTry;
                                    Interlocked.Add(ref improvedCount, 1);
                                    LowestCountSeen = Math.Min(LowestCountSeen, bestResults.EstimatedClusterCount);
                                    Logger.Info($"Cluster count Improved to: {bestResults}");
                                }
                            }
                        }

                    });
                if (improvedCount > 0)
                    iterationsWithoutImprovement = 0;
                else
                    iterationsWithoutImprovement++;
                if (iterationsWithoutImprovement >= MaxIterationsWithoutImprovement)
                    break;
                if (bestResults.EstimatedClusterCount <= 2)
                    break; // No point in continuing!
            }
            var indicesFound = queue.RemoveAll().Reverse().ToList();
            if (sampledPoints.Count < points.Count)
            {
                // Results are based on Sampled set of points. Now we need to recreate these indices using the 
                // full set of points.
                //TODO: "Unsample" the indices.
                var unsampledIndices = indicesFound.Select(i => Unsample(points, i)).ToList();
                Logger.Info($"Final, unsampled Cluster count: {unsampledIndices[0]}");
                return unsampledIndices;
            }
            else
                return indicesFound;
        }


        /// <summary>
        /// Using default values for many parameters, search many Hilbert orderings of the points, each based on a different permutation of the dimensions, and
        /// keep the one yielding the best Metric, which is the one that estimates the lowest value for the number of clusters.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="bitsPerDimension">Number of bits needed to represent maximum coordinate value, or -1 if it should be derived.</param>
        /// <param name="outlierSize">OutlierSize that discriminates between clusters worth counting and those that are not.</param>
        /// <param name="noiseSkipBy">NoiseSkipBy value to help smooth out calculations in the presence of noisy data.</param>
        /// <param name="reducedNoiseSkipBy">If few clusters, reduce NoiseSkipBy to this.</param>
        /// <param name="maxTrials">Max trials to attempt. This equals MaxIterations * ParallelTrials (apart from rounding).</param>
        /// <param name="maxIterationsWithoutImprovement">Max iterations without improvement.
        /// Stops searching early if no improvement is detected.</param>
        /// <param name="useSample">If true, use a random sample of points in each HilbertIndex tested, to save time.
        /// May yield a poorer result, but faster.</param>
        public static PermutationFound Search(IReadOnlyList<UnsignedPoint> points, int bitsPerDimension, int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy, int maxTrials, int maxIterationsWithoutImprovement = 3, bool useSample = false, bool shouldCompact = false)
        {
            var parallel = 4;
            if (bitsPerDimension <= 0)
                bitsPerDimension = HilbertSort.FindBitsPerDimension(points);
            var maxIterations = maxTrials == 1 ? 1 : (maxTrials + (parallel / 2)) / parallel;
            var optimizer = new OptimalPermutation(outlierSize, noiseSkipBy, reducedNoiseSkipBy, ScrambleHalfStrategy, bitsPerDimension)
            {
                MaxIterations = maxIterations,
                MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement,
                ParallelTrials = parallel,
                UseSample = useSample
            };
            return optimizer.Search(points);
        }

        public static IList<PermutationFound> SearchMany(IReadOnlyList<UnsignedPoint> points, int bitsPerDimension, int indexCount, int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy, int maxTrials, int maxIterationsWithoutImprovement = 3, bool useSample = false, bool shouldCompact = false)
        {
            var parallel = 4;
            if (bitsPerDimension <= 0)
                bitsPerDimension = HilbertSort.FindBitsPerDimension(points);
            var optimizer = new OptimalPermutation(outlierSize, noiseSkipBy, reducedNoiseSkipBy, ScrambleHalfStrategy, bitsPerDimension)
            {
                MaxIterations = (maxTrials + (parallel / 2)) / parallel,
                MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement,
                ParallelTrials = parallel,
                UseSample = useSample,
            };
            return optimizer.SearchMany(points, indexCount);
        }

        /// <summary>
        /// Compute a fair sample size for sample to be drawn from allPoints such that the 
        /// expected average cluster size is large enough to resolve all clusters
        /// even after they are shrunk by sampling.
        /// 
        /// If the number of points N is larger than 2000, the sample size will never be less than 2000.
        /// If N is less than 2000, no sampling will occur.
        /// </summary>
        /// <param name="allPoints">Points to be sampled.</param>
        /// <param name="estimatedClusterCount">Estimated cluster count.</param>
        /// <returns>Sample size to use.</returns>
        private int SampleSize(IReadOnlyList<UnsignedPoint> allPoints, int estimatedClusterCount)
        {
            var n = (double)allPoints.Count;
            var k = (double)estimatedClusterCount;
            var min = 100.0;
            var s = Min(1.0, min / (n / k));
            var nSample = (int)Min(n, Max(2000.0, s * n));
            return nSample;
        }

        /// <summary>
        /// Sample allPoints such that the expected average cluster size is large enough to resolve all clusters
        /// even after they are shrunk by sampling.
        /// 
        /// If the number of points N is larger than 2000, the sample size will never be less than 2000.
        /// If N is less than 2000, no sampling will occur.
        /// </summary>
        /// <param name="allPoints">Points to be sampled.</param>
        /// <param name="sampleSize">Number of points to randomly sample from allPoints.
        /// If this number equals N, the unsampled size of allPoints, allPoints is returned unchanged.
        /// </param>
        private IReadOnlyList<UnsignedPoint> Sample(IReadOnlyList<UnsignedPoint> allPoints, int sampleSize)
        {
            if (sampleSize >= allPoints.Count)
                return allPoints;
            var randomSample = allPoints.TakeRandom(sampleSize, allPoints.Count);
            return randomSample.ToList();
        }

        /// <summary> Create and measure a new curve using all points, not just a sample, but use the same permutation.</summary>
        /// <param name="sampled">Results from evaluating a sample of points.</param>
        private PermutationFound Unsample(IReadOnlyList<UnsignedPoint> allPoints, PermutationFound sampled)
        {
            var curveToTry = HilbertSort.Sort(allPoints, BitsPerDimension, sampled.PermutationUsed);
            var metricResults = Metric(curveToTry);
            var resultsToTry = new PermutationFound(sampled.PermutationUsed, curveToTry, metricResults.Item1, metricResults.Item2);
            return resultsToTry;
        }

    }
}
