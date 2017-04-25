using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HilbertTransformation;
using HilbertTransformation.Random;

namespace Clustering
{
	/// <summary>
	/// Find an optimal HilbertIndex for a given set of points.
	/// 
	/// Many HilbertIndex objects are created for the same set of points, each with a different permutation
	/// used in performing the Hilbert transformation on the points. Each permutation will cause the 
	/// points to be sorted in a different order. Some of these curves are better than others at visiting
	/// a cluster of points, moving from point to point within the cluster, then advancing to the next cluster
	/// without doubling back later to a cliuster already visited.
	/// 
	/// If the curve revisits a cluster multiple times after visiting other clusters, then that index is fragmented.
	/// The more fragmented an index is, the poorer the performance when we do an exhaustive construction 
	/// of the clusters. Whatever measure is used of quality should correlate well with the number of
	/// stripes found in the index. For example, if there are ten true clusters but the index finds twenty
	/// clusters, our fragmentation factor is two.
	/// 
	/// The metric used to evaluate the quality of the many indices to be tested may be supplied by the caller,
	/// as well as the strategy for randomizing the permutation used.
	/// </summary>
    /// <remarks>NOTE: This approach uses more memory than necessary. We do not need to convert UnsignedPoints into HilbertPoints
    /// at all, just sort by the Hilbert position (BigInteger), which saves memory. See class OptimalOrdering.</remarks>
	public class OptimalIndex
	{
		/// <summary>
		/// Provides the results of the search.
		/// </summary>
		public class IndexFound: IComparable<IndexFound>
		{
			public Permutation<uint> PermutationUsed { get; private set; }

            /// <summary>
            /// The Hilbert index corresponding to the permutation.
            /// 
            /// If this structure is compacted, Index will be set to null.
            /// </summary>
			public HilbertIndex Index { get; private set; }

            private List<int> CompactIndex { get; set; }

            public void Compact()
            {
                CompactIndex = Index.SortedPoints.Select(hp => hp.UniqueId).ToList();
                Index = null;
            }

            /// <summary>
            /// Obtains the Ids of the points sorted according to the Hilbert curve order.
            /// </summary>
            public IEnumerable<int> SortedPointIndices
            {
                get
                {
                    if (CompactIndex != null)
                        return CompactIndex;
                    else
                        return Index.SortedPoints.Select(hp => hp.UniqueId);
                }
            }

			public int EstimatedClusterCount { get; private set; }

			/// <summary>
			/// The maximum distance between points (other than outliers) that should be clustered together.
			/// </summary>
			public long MergeSquareDistance { get; set; }

			public IndexFound(Permutation<uint> permutation, HilbertIndex index, int estimatedClusterCount, long mergeSquareDistance)
			{
				PermutationUsed = permutation;
				Index = index;
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
			public bool IsBetterThan(IndexFound other)
			{
				return CompareTo(other) < 0;
			}

			public int CompareTo(IndexFound other)
			{
				return EstimatedClusterCount.CompareTo(other.EstimatedClusterCount);
			}

			public override string ToString()
			{
				return $"[IndexFound: EstimatedClusterCount={EstimatedClusterCount}]";
			}
		}

		#region Attributes: Metric, PermutationStrategy, ParallelTrials, MaxIterations, MaxIterationsWithoutImprovement

		/// <summary>
		/// Scores how well a given index measures up. The lower the score, the better. 
		/// A low score is assumed to mean less fragmentation of the index.
		/// Also derives the MergeSquareDistance.
		/// 
		/// NOTE: This delegate must be threadsafe!
		/// </summary>
		Func<HilbertIndex, Tuple<int,long>> Metric { get; set; }

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

        /// <summary>
        /// If true, the IndexFound will be compacted by discarding the HilbertIndex and making a simple list of ids in Hilbert sorted order.
        /// This is to save memory.
        /// </summary>
        public bool ShouldCompact { get; set; }

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
			var dimensionsToScramble = Math.Max(Math.Min(dimensions, 5), dimensions / (1 << iteration));
			return previousPermutation.Scramble(dimensionsToScramble);
		};

		#region Constructors

		/// <summary>
		/// Create an optimizer which finds the curve that minimizes the number of clusters found using means
		/// supplied by the caller.
		/// </summary>
		/// <param name="metric">Evaluates the quality of the HilbertIndex derived using a given permutation.</param>
		/// <param name="strategy">Strategy to employ that decides how many dimensions to scramble during each iteration.</param>
		public OptimalIndex(Func<HilbertIndex, Tuple<int,long>> metric, Func<Permutation<uint>, int, int, Permutation<uint>> strategy)
		{
			Metric = metric;
			PermutationStrategy = strategy;
            ShouldCompact = false;
        }

		/// <summary>
		/// Create an optimizer which finds the curve that minimizes the number of clusters found using a ClusterCounter.
		/// </summary>
		/// <param name="outlierSize">OutlierSize to use with the ClusterCounter.</param>
		/// <param name="noiseSkipBy">NoiseSkipBy to use with the ClusterCounter.</param>
		/// <param name="reducedNoiseSkipBy">ReducedNoiseSkipBy to use with the ClusterCounter.</param>
		/// <param name="strategy">Strategy to employ that decides how many dimensions to scramble during each iteration.</param>
		public OptimalIndex(int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy, Func<Permutation<uint>, int, int, Permutation<uint>> strategy)
		{
			var maxOutliers = outlierSize;
			var skip = noiseSkipBy;
			Metric = (HilbertIndex index) =>
			{
				var counter = new ClusterCounter { 
					OutlierSize = maxOutliers, 
					NoiseSkipBy = skip, 
					ReducedNoiseSkipBy = reducedNoiseSkipBy, 
					LowestCountSeen = LowestCountSeen 
				};
				var counts = counter.Count(index.SortedPoints);
				return new Tuple<int, long>(counts.CountExcludingOutliers, counts.MaximumSquareDistance);
			};
			PermutationStrategy = strategy;
            ShouldCompact = false;
        }

		#endregion

		/// <summary>
		/// Search many HilbertIndex objects, each based on a different permutation of the dimensions, and
		/// keep the one yielding the best Metric, which is likely the one that estimates the lowest value 
		/// for the number of clusters.
		/// </summary>
		/// <param name="points">Points to index.</param>
		/// <param name="startingPermutation">Starting permutation.</param>
		/// <returns>The best index found and the permutation that generated it.</returns>
		public IndexFound Search(IList<HilbertPoint> points, Permutation<uint> startingPermutation = null)
		{
			var found = SearchMany(points, 1, startingPermutation);
			return found.First();
		}

		/// <summary>
		/// Avoid running out of memory when evaluating multiple indices in parallel
		/// by reducing the parallelism.
		/// </summary>
		/// <returns>The max degrees of parallelism.</returns>
		/// <param name="points">Points.</param>
		private static int EstimateMaxDegreesOfParallelism(IList<HilbertPoint> points)
		{
			var n = points.Count();
			var d = points[0].Dimensions;
			var limit = 24000000;
			var maxDegrees = limit / (n * d);
			maxDegrees = Math.Min(4, Math.Max(1, maxDegrees));
			return maxDegrees;
		}

		/// <summary>
		/// Search many HilbertIndex objects, each based on a different permutation of the dimensions, and
		/// keep the ones yielding the best Metrics, likely those that estimate the lowest values
		/// for the number of clusters.
		/// </summary>
		/// <param name="points">Points to index.</param>
		/// <param name="indexCount">Number of the best indices to return. 
		/// For example, if this is 10, then the 10 indices with the lowest scores will be kept.</param>
		/// <param name="startingPermutation">Starting permutation.</param>
		/// <returns>The best indices found and the permutations that generated them.
		/// THe first item in the returned list is the best of the best, and the last is the worst of the best.</returns>
		public IList<IndexFound> SearchMany(IList<HilbertPoint> points, int indexCount, Permutation<uint> startingPermutation = null)
		{
			if (points.Count() < 10)
				throw new ArgumentException("List has too few elements", nameof(points));
			var queue = new BinaryHeap<IndexFound>(BinaryHeapType.MaxHeap, indexCount);
			int dimensions = points[0].Dimensions;
			var bitsPerDimension = points[0].BitsPerDimension;
			if (startingPermutation == null)
				startingPermutation = new Permutation<uint>(dimensions);
			var firstIndex = new HilbertIndex(points, startingPermutation);
			// Measure our first index, then loop through random permutations 
			// looking for a better one, always accumulating the best in results.
			var metricResults = Metric(firstIndex);
			var bestResults = new IndexFound(startingPermutation, firstIndex, metricResults.Item1, metricResults.Item2);
            if (ShouldCompact)
                bestResults.Compact();
			LowestCountSeen = Math.Min(LowestCountSeen, bestResults.EstimatedClusterCount);
		    Logger.Info($"Cluster count Starts at: {bestResults}");
            var startingCount = bestResults.EstimatedClusterCount;
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
						lock(allPermutations)
						{
							if (!allPermutations.Any())
								return;
							permutationToTry = allPermutations.Last();
							allPermutations.RemoveAt(allPermutations.Count - 1);
						}
					}
                    IList<HilbertPoint> sampledPointsToUse;
                    lock(points)
                    {
                        sampledPointsToUse = sampledPoints;
                    }
					var indexToTry = new HilbertIndex(sampledPointsToUse, permutationToTry);
					metricResults = Metric(indexToTry);
					var resultsToTry = new IndexFound(permutationToTry, indexToTry, metricResults.Item1, metricResults.Item2);
                    if (ShouldCompact)
                        resultsToTry.Compact();
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
                            if (!rejectedSampleSizes.Contains(indexToTry.Count)) { 
                                sampleSize = Math.Min(points.Count(), 3 * indexToTry.Count / 2);
                                Logger.Info($"Increasing sample size to {sampleSize} because estimated K = {resultsToTry.EstimatedClusterCount} (not trusted)");
                                var newSampledPoints = Sample(points, sampleSize);
                                lock (points)
                                {
                                    sampledPoints = newSampledPoints;
                                }
                                rejectedSampleSizes.Add(indexToTry.Count);
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
		/// Using default values for many parameters, search many HilbertIndex objects, each based on a different permutation of the dimensions, and
		/// keep the one yielding the best Metric, which is the one that estimates the lowest value 
		/// for the number of clusters.
		/// </summary>
		/// <param name="points">Points to index.</param>
		/// <param name="outlierSize">OutlierSize that discriminates between clusters worth counting and those that are not.</param>
		/// <param name="noiseSkipBy">NoiseSkipBy value to help smooth out calculations in the presence of noisy data.</param>
		/// <param name="reducedNoiseSkipBy">If few clusters, reduce NoiseSkipBy to this.</param>
		/// <param name="maxTrials">Max trials to attempt. This equals MaxIterations * ParallelTrials (apart from rounding).</param>
		/// <param name="maxIterationsWithoutImprovement">Max iterations without improvement.
		/// Stops searching early if no improvement is detected.</param>
		/// <param name="useSample">If true, use a random sample of points in each HilbertIndex tested, to save time.
		/// May yield a poorer result, but faster.</param>
		public static IndexFound Search(IList<HilbertPoint> points, int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy, int maxTrials, int maxIterationsWithoutImprovement = 3, bool useSample = false, bool shouldCompact = false)
		{
			var parallel = 4;
			var optimizer = new OptimalIndex(outlierSize, noiseSkipBy, reducedNoiseSkipBy, ScrambleHalfStrategy)
			{
				MaxIterations = (maxTrials + (parallel / 2)) / parallel,
				MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement,
				ParallelTrials = parallel,
				UseSample = useSample,
                ShouldCompact = shouldCompact
			};
			return optimizer.Search(points);
		}

		public static IList<IndexFound> SearchMany(IList<HilbertPoint> points, int indexCount, int outlierSize, int noiseSkipBy, int reducedNoiseSkipBy,  int maxTrials, int maxIterationsWithoutImprovement = 3, bool useSample = false, bool shouldCompact = false)
		{
			var parallel = 4;
			var optimizer = new OptimalIndex(outlierSize, noiseSkipBy, reducedNoiseSkipBy, ScrambleHalfStrategy)
			{
				MaxIterations = (maxTrials + (parallel / 2)) / parallel,
				MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement,
				ParallelTrials = parallel,
				UseSample = useSample,
                ShouldCompact = shouldCompact
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
		private int SampleSize(IList<HilbertPoint> allPoints, int estimatedClusterCount)
		{
			var n = (double)allPoints.Count;
			var k = (double)estimatedClusterCount;
			var min = 100.0;
			var s = Math.Min(1.0, min / (n / k));
			var nSample = (int)Math.Min(n, Math.Max(2000.0, s * n));
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
		private IList<HilbertPoint> Sample(IList<HilbertPoint> allPoints, int sampleSize)
		{
			if (sampleSize >= allPoints.Count)
				return allPoints;
			var randomSample = allPoints.TakeRandom(sampleSize, allPoints.Count);
			return randomSample.ToList();
		}

		/// <summary>
		/// Create and measure a new HilbertIndex using all the points, not just a sample of them,
		/// but use the same permutation.
		/// </summary>
		/// <param name="sampled">Sampled.</param>
		private IndexFound Unsample(IList<HilbertPoint> allPoints, IndexFound sampled)
		{
			var indexToTry = new HilbertIndex(allPoints, sampled.PermutationUsed);
			var metricResults = Metric(indexToTry);
			var resultsToTry = new IndexFound(sampled.PermutationUsed, indexToTry, metricResults.Item1, metricResults.Item2);
            if (ShouldCompact)
                resultsToTry.Compact();
			return resultsToTry;
		}

	}
}
