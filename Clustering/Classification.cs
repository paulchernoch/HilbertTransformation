using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation.Random;

namespace Clustering
{
    /// <summary>
    /// Represent points grouped into class partitions where each point is associated with a class label 
    /// and each labeled class has a set of points.
    /// 
    /// This does not perform the unassisted clustering of points, it merely shows the results of such a clustering.
    /// </summary>
    /// <typeparam name="TPoint">Type of the points to be classified.</typeparam>
    /// <typeparam name="TLabel">Type of the label that can be associated with a set within the classification.</typeparam>
    public class Classification<TPoint, TLabel> :ICloneable where TLabel : IEquatable<TLabel>
    {

        #region Properties (LabelToPoints, PointToLabel, NumPartitions, NumPoints)

        /// <summary>
        /// Associates a class label with the points belonging to that class.
        /// </summary>
        public Dictionary<TLabel, ISet<TPoint>> LabelToPoints { get; set; }

        /// <summary>
        /// Associates a point with the label for its class.
        /// </summary>
        private Dictionary<TPoint, TLabel> PointToLabel { get; set; }

        /// <summary>
        /// Total number of class partitions that points are divided among.
        /// </summary>
        public int NumPartitions { get { return LabelToPoints.Count; } }

        /// <summary>
        /// Counts the number of partitions whose size equals or exceeds the given number.
        /// </summary>
        /// <param name="minSize">Minimum size of partitions to be counted.</param>
        /// <returns>The number of partitions that are large enough.</returns>
        public int NumLargePartitions(int minSize)
        {
            return LabelToPoints.Count(p => p.Value.Count >= minSize);
        }

        public int SmallestLargePartition(int minSize)
        {
            return LabelToPoints.Values
                .Where(partition => partition.Count >= minSize)
                .Select(partition => partition.Count)
                .DefaultIfEmpty()
                .Min()
                ;
        }

        public double NumEffectivePartitions
        {
            get
            {
                return EffectivePartitions(LabelToPoints.Values.Select(partition => partition.Count));
            }
        }

        /// <summary>
        /// Count how many "effective" partitions there are using the second moment of the partition sizes.
        /// 
        /// This is N divided by the second moment of the number of points per partition.
        /// If all partitions have the same number of points, this will equal the actual number of partitions P.
        /// If one partition has all the points, this will equal one.
        /// If one partition is much larger than all the others, the value will be close to one.
        /// Large partitions count for more than small ones.
        /// </summary>
        /// <param name="partitionSizes">Sizes of each partition.</param>
        /// <returns>Number of effective partitions.</returns>
        public static double EffectivePartitions(IEnumerable<int> partitionSizes)
        {
            var N = 0;
            var sumNSquared = 0;
            foreach (var n in partitionSizes)
            {
                N += n;
                sumNSquared += n * n;
            }
            // The second moment is sumNSquared / N.
            // The effective number of partitions is N / secondMoment.
            return N * N / (double)sumNSquared;
        }

        /// <summary>
        /// Total number of points among all partitions.
        /// </summary>
        public int NumPoints { get { return PointToLabel.Count; } }

        #endregion

        #region Constructors

        public Classification()
        {
            LabelToPoints = new Dictionary<TLabel, ISet<TPoint>>();
            PointToLabel = new Dictionary<TPoint, TLabel>();
        }

        public Classification(IEnumerable<TPoint> points, Func<TPoint,TLabel> startingLabel) : this()
        {
            foreach (var point in points)
            {
                Add(point, startingLabel(point));
            }
        }

        /// <summary>
        /// Create a new Classification by randomly selecting a subset of the points in the current Classification.
        /// </summary>
        /// <param name="sampleSize">Number of points to include in the new Classification.</param>
        /// <returns>A new Classification that has the given number of points.</returns>
        public Classification<TPoint, TLabel> Sample(int sampleSize)
        {
            var subset = new Classification<TPoint, TLabel>();
            foreach (var point in Points().TakeRandom(sampleSize, NumPoints))
                subset.Add(point,  GetClassLabel(point));
            return subset;
        }

        #endregion

        #region Modify the Classification (Add, Remove, Merge)

        /// <summary>
        /// Add a point to the classification with the associated label.
        /// 
        /// If the point was already classified, its old classification is removed.
        /// </summary>
        /// <param name="p">Point to add.</param>
        /// <param name="classLabel">Label for classification.</param>
        public void Add(TPoint p, TLabel classLabel)
        {
            Remove(p);
            PointToLabel[p] = classLabel;
            EnsurePartition(classLabel).Add(p);
        }

        private ISet<TPoint> EnsurePartition(TLabel classLabel)
        {
            ISet<TPoint> partition;
            if (LabelToPoints.TryGetValue(classLabel, out partition)) return partition;
            partition = new HashSet<TPoint>();
            LabelToPoints[classLabel] = partition;
            return partition;
        }

        /// <summary>
        /// Remove a point from its class.
        /// </summary>
        /// <param name="p">Point to remove.</param>
        /// <returns>True if the point was removed, false if it was not previously a member of any class.</returns>
        public bool Remove(TPoint p)
        {
            TLabel label;
            if (!PointToLabel.TryGetValue(p, out label)) return false;
            PointToLabel.Remove(p);
            var oldPoints = LabelToPoints[label];
            var didRemove = oldPoints.Remove(p);
            if (oldPoints.Count == 0)
                LabelToPoints.Remove(label);
            return didRemove;
        }

        /// <summary>
        /// Merge all the members of the partitions labeled by any of the labels in sourceLabels
        /// into the partition indicated by targetLabel.
        /// </summary>
        /// <param name="targetLabel">Move members into this labeled partition.</param>
        /// <param name="sourceLabels">Move members out of these labeled partitions.</param>
        /// <returns>True if at least one point was added to the targetLabel's partition.
        /// False if the target partition did not increase in size.</returns>
        public bool Merge(TLabel targetLabel, IEnumerable<TLabel> sourceLabels)
        {
            var targetPartition = EnsurePartition(targetLabel);
            var startingSize = targetPartition.Count;
            foreach (var sourceLabel in sourceLabels.Where(sLabel => !sLabel.Equals(targetLabel)))
            {
                ISet<TPoint> singleSourcePoints;
                if (!LabelToPoints.TryGetValue(sourceLabel, out singleSourcePoints)) continue;

                // Add to LabelToPoints under new targetLabel
                targetPartition.UnionWith(singleSourcePoints);
                // Remove from LabelToPoints under old sourceLabel.
                LabelToPoints.Remove(sourceLabel);

                foreach (var p in singleSourcePoints)
                    PointToLabel[p] = targetLabel;
            }
            return startingSize < targetPartition.Count;
        }

        /// <summary>
        /// Merge all the members of the partition with the sourceLabel
        /// into the partition indicated by targetLabel.
        /// </summary>
        /// <param name="targetLabel">Move members into this labeled partition.</param>
        /// <param name="sourceLabel">Move members out of the partition with this label.</param>
        /// <returns>True if at least one point was added to the targetLabel's partition.
        /// False if the target partition did not increase in size.</returns>
        public bool Merge(TLabel targetLabel, TLabel sourceLabel)
        {
			if (targetLabel.Equals(sourceLabel)) return false;
            return Merge(targetLabel, new[] { sourceLabel });
        }

        /// <summary>
        /// Merge all labels in the set into one cluster, making one of them the target label.
        /// </summary>
        /// <param name="mergeSet">Set of cluster labels to merge into a single cluster.
        /// If this has fewer than two items, no merging will occur.</param>
        /// <returns>True if any items were merged, false otherwise.</returns>
        public bool Merge(ISet<TLabel> mergeSet)
        {
            if (mergeSet.Count < 2) return false;
            var mergeList = mergeSet.ToList();
            // We will make the last label in the list the target label, because it is least expensive to remove the last item.
            var targetIndex = mergeList.Count - 1;
            var targetLabel = mergeList[targetIndex];
            mergeList.RemoveAt(targetIndex);
            return Merge(targetLabel, mergeList);
        }

        /// <summary>
        /// Counts how many distinct classes in the goldStandard are represented by points from the given class in this Classification.
        /// </summary>
        /// <param name="goldStandard">A second Classification to use as a benchmark for comparison.
        /// It is not assumed that the two Classifications use the same labeling scheme.</param>
        /// <param name="classLabel">Identifies a class in this Classification (NOT in the goldStandard).</param>
        /// <returns>One if all points in the class labeled with classLabel from this Classification are in the same class in goldStandard.
        /// Otherwise, it returns the number of distinct classes that the points are drawn from in goldStandard.
        /// A perfect clustering scheme will always return one.</returns>
        public int Homogeneity(Classification<TPoint,TLabel> goldStandard, TLabel classLabel)
        {
            return LabelToPoints[classLabel].Select(goldStandard.GetClassLabel).Distinct().Count();
        }

        #endregion

		#region Enumerating some or all Points

        /// <summary>
        /// Enumerate through all points in all partitions.
        /// </summary>
        /// <returns>Enumerator over all points.</returns>
        public IEnumerable<TPoint> Points()
        {
            return PointToLabel.Keys.AsEnumerable();
        }

        /// <summary>
        /// Enumerate through all points in the partition for the given class label.
        /// </summary>
        /// <returns>Enumerator over all points.</returns>
        public ISet<TPoint> PointsInClass(TLabel label)
        {
            return LabelToPoints[label];
        }

		#endregion

        #region Labels (GetClassLabel, ClassLabels)

        /// <summary>
        /// Get the class label for a point.
        /// </summary>
        /// <param name="p">Point whose label is sought.</param>
        /// <returns>The label. If the point is not present, return default(TLabel), which is likely null.</returns>
        public TLabel GetClassLabel(TPoint p)
        {
            TLabel label;
            return !PointToLabel.TryGetValue(p, out label) ? default(TLabel) : label;
        }

        /// <summary>
        /// Enumerate through all labels for all partitions.
        /// </summary>
        /// <returns>Enumerator over all the labels.</returns>
        public IEnumerable<TLabel> ClassLabels()
        {
            return LabelToPoints.Keys.AsEnumerable();
        }

        /// <summary>
        /// Get a distinct set of all the class labels for any of the given points.
        /// </summary>
        /// <param name="points">Points whose labels are sought.</param>
        /// <returns>Set of labels for the given points.</returns>
        public ISet<TLabel> DistinctClassLabels(IEnumerable<TPoint> points)
        {
            var labels = new HashSet<TLabel>();
            labels.UnionWith(points.Select(GetClassLabel));
            return labels;
        }

        #endregion

		#region Comparing two Classifications

        /// <summary>
        /// Compare two classifications and get a metric that identifies how similar they are.
        /// 
        /// Consider this the gold standard categorization and alternatePartition the unattended clustering.
        /// (It doesn't matter, as the results are symmetrical.)
        /// </summary>
        /// <param name="alternatePartition">The unattended clustering.</param>
        /// <param name="alpha">Determines how to weight the Precision and Recall. 
        /// The default value of 0.5 works well for most situations and evenly balances Precision and Recall.
        /// Must be between 0 and 1. </param>
        /// <returns>The metric. Casting it to a double (or calling BCubed) will retrieve a numerical measure.
        /// If that value is 1.0, the classifications are identical.
        /// </returns>
        public ClusterMetric<TPoint, TLabel> Compare(Classification<TPoint, TLabel> alternatePartition, double alpha = 0.5)
        {
            var category = this;
            var cluster = alternatePartition;
			ClusterMetric<TPoint, TLabel> metric;

			// IsSimilar is fast, but less precise. If it says the Classifications have identical clustering, they do,
			// but if they do not, we need to figure out how bad they match and need to create a real ClusterMetric.
			if (category.IsSimilarTo(cluster))
				metric = new ClusterMetric<TPoint, TLabel>();
			else
                metric = new ClusterMetric<TPoint, TLabel>(
	                Points().ToList(),
	                label => category.LabelToPoints[label],
	                label => cluster.LabelToPoints[label], 
	                point => category.PointToLabel[point], 
	                point => cluster.PointToLabel[point], 
	                alpha
                );

            metric.Measure();
            return metric;
        }

		public IEnumerable<TPoint> MisplacedPoints(Classification<TPoint, TLabel> goldPartition)
		{
			var misplaced = new List<TPoint>();
			foreach (var thisLabel in ClassLabels())
			{
				var correspondingLabel = PointsInClass(thisLabel).Select(goldPartition.GetClassLabel)
					.GroupBy(goldLabel => goldLabel)
					.OrderByDescending(grp => grp.Count())
					.Select(grp => grp.Key).First();
				misplaced.AddRange(PointsInClass(thisLabel).Where(p => !goldPartition.GetClassLabel(p).Equals(correspondingLabel)));
			}
			return misplaced;
		}

		/// <summary>
		/// Test if two Classifications are similar or not.
		/// 
		/// The labels associated with each cluster are ignored by this comparison.
		/// All that matters is that the same points are clustered together in each Classification,
		/// and no points are grouped together in one that are not together in the other.
		/// 
		/// NOTE: This is much faster than comparing using BCubed, but does not give a useful qualitative 
		/// measure of how different two Classifications are.
		/// </summary>
		/// <returns><c>true</c>, if both classifications have identical groupings of points into clusters. 
		/// <c>false</c> otherwise.</returns>
		/// <param name="alternatePartition">Alternate partition to compare.</param>
		public bool IsSimilarTo(Classification<TPoint, TLabel> alternatePartition)
		{
			if (NumPartitions != alternatePartition.NumPartitions)
				return false;
			var identicalCount = IdenticalClusters(alternatePartition);
			return identicalCount == NumPartitions;
		}

		/// <summary>
		/// Compares two classifications to see how many clusters in this partition are identical to corresponding clusters in
		/// the second partition.
		/// 
		/// This measure is not as good as BCubed for getting a true picture of how similar are two Classifications,
		/// but it is much faster. It is best used to decide if two Classifications are exactly the same or not.
		/// 
		/// For example, if there were 10,000 points in 100 clusters, and each cluster had one point missing, 
		/// then no clusters would match perfectly, giving a score of zero, when in fact, that would yield 
		/// a very good BCubed score. However, if this returns a number equal to the total number of clusters in this 
		/// Classification, then the partitions are identical and BCubed would also equal one. 
		/// </summary>
		/// <returns>Count of perfectly matching clusters.</returns>
		/// <param name="alternatePartition">Alternate partition to compare.</param>
		public int IdenticalClusters(Classification<TPoint, TLabel> alternatePartition)
		{
			var identicalCount = 0;
			var clusterHashes = new HashSet<int>();
			// Record hashes for each of "this" Classification's clusters.
			foreach (var pointSet in LabelToPoints.Values)
			{
				var hash = SetHash(pointSet.Select(p => p.GetHashCode()));
				clusterHashes.Add(hash);
			}
			// Attempt to match hashes for the alternatePartition's clusters to the ones just recorded above.
			foreach (var pointSet in alternatePartition.LabelToPoints.Values)
			{
				var hash = SetHash(pointSet.Select(p => p.GetHashCode()));
				if (clusterHashes.Contains(hash))
					identicalCount++;
			}
			return identicalCount;
		}

		/// <summary>
		/// Combine a set of integers into a hash in an order-independent way.
		/// 
		/// If two sets of integers have the same hash, they are likely the same.
		/// </summary>
		/// <param name="ids">Integers to be hashed.</param>
		private static int SetHash(IEnumerable<int> ids)
		{
			var sum = 0;
			var count = 0;
			foreach (var i in ids)
			{
				sum += i;
				count++;
			}
			return sum + count;
		}

		#endregion

        /// <summary>
        /// For each label in this Classification, deduce the corresponding label in the goldPartition.
        /// 
        /// Caveats:
        ///    1. Multiple labels in this Classification could map to the same label in goldPartition.
        ///    2. Some labels in the goldPartition may not have a corresponding label in this Classification.
        ///    3. Only the dominant mapping is sought.
        /// </summary>
        /// <param name="goldPartition">Has the "correct" partitioning.</param>
        /// <returns>A concordance where the key is a label from this Classification and the value is a label in the goldPartition.</returns>
        public Dictionary<TLabel, TLabel> MakeConcordance(Classification<TPoint, TLabel> goldPartition)
        {
            var concordance = new Dictionary<TLabel, TLabel>();
            foreach (var thisLabel in ClassLabels())
            {
                var correspondingLabel = PointsInClass(thisLabel).Select(goldPartition.GetClassLabel)
                    .GroupBy(goldLabel=>goldLabel)
                    .OrderByDescending(grp=>grp.Count())
                    .Select(grp=>grp.Key).First();
                concordance[thisLabel] = correspondingLabel;
            }
            return concordance;
        }



        /// <summary>
        /// Measure how much the given ordering of points is concordant with the clustering.
        /// </summary>
        /// <param name="orderedPoints">Points given in the order being tested.</param>
        /// <returns>A value between zero and one, where one means that all points are grouped into their clusters
        /// with no cluster broken into more than one consecutive range of points.
        /// A value of a half means that on average, every cluster is broken into two distinct segments of orderedPoints
        /// with points from one or more different clusters interposed between them.
        /// A value of C/N (where C is the number of clusters and N is the number of points) means that
        /// no two consecutive points belong to the same cluster.</returns>
        public double MeasureConcord(IEnumerable<TPoint> orderedPoints)
        {
            var count = 0;
            var transitions = 0;
            TLabel previousLabel = default(TLabel);
            foreach (var point in orderedPoints)
            {
                var currentLabel = GetClassLabel(point);
                if (!currentLabel.Equals(previousLabel))
                    transitions++;
                previousLabel = currentLabel;
                count++;
            }
            return NumPartitions / (double) transitions;
        }

		/// <summary>
		/// Clone the Classification, using the same Points and labels (semi-shallow copy).
		/// </summary>
		public object Clone()
		{
			var copy = new Classification<TPoint, TLabel>();
			foreach (var point in Points())
			{
				copy.Add(point, GetClassLabel(point));
			}
			return copy;
		}
	}
}
