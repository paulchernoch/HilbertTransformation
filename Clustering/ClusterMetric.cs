using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Clustering
{
    /// <summary>
    /// Implements the B-Cubed Cluster Quality Index metric which scores how well a clustering algorithm works on a scale from zero (worst)
    /// to one (perfect).
    /// 
    /// This is an extrinsic (external) metric that relies on a "Gold standard" - a perfectly clustered solution - for comparison.
    /// Thus this is useful for testing clustering algorithms, but not for implementing them.
    /// 
    /// Many alternate cluster quality index metrics exist in the literature, each of which possesses useful properties. 
    /// This one was chosen because the literature shows that it is useful across many types of problems and suffers the fewest 
    /// known defects in edge cases (so far). 
    /// 
    /// Other extrinsic metrics are: Pantel & Lin, Entropy of a cluster, VI (variation of information), Q0, "class entropy",
    /// MI (Mutual Information), V-measure, Rand, Adjusted Rand, Jaccard, Folkes & Mallows, F (Van Rijsbergen), Purity, Inverse Purity.
    /// 
    /// Examples of intrinsic (internal) metrics that do not require a gold standard but rely on a distance measure are:
    /// Silhouette, Calinski, C-index, DB, Gamma, ARI.
    /// 
    /// See "A comparison of Extrinsic Clustering Evaluation Metrics based on Formal Constraints Technical Report", January 4, 2008.
    /// Authors: Enrique Amigo, Julio Gonzalo, Javier Artiles
    /// </summary>
    /// <typeparam name="TPoint">Type of the points to be clustered.</typeparam>
    /// <typeparam name="TLabel">Type of the label that can be applied to either a category or a cluster.</typeparam>
    public class ClusterMetric<TPoint,TLabel> where TLabel : IEquatable<TLabel>
    {
        #region The Inputs: Points & delegates for getting labels from points and vice versa for both category and cluster classifications

        /// <summary>
        /// All points from all clusters and categories.
        /// </summary>
        private IList<TPoint> Points { get; set; }

        /// <summary>
        /// Delegate that retrieves all the points in the category with the given label.
        /// </summary>
        private Func<TLabel, IEnumerable<TPoint>> PointsInCategory { get; set; }

        /// <summary>
        /// Delegate that retrieves all the points in the cluster with the given label.
        /// </summary>
        private Func<TLabel, IEnumerable<TPoint>> PointsInCluster { get; set; }

        /// <summary>
        /// Delegate that retrieves the label of the category for a given point.
        /// 
        /// In the cited paper, this is the function L(e).
        /// </summary>
        private Func<TPoint, TLabel> Category { get; set; }

        /// <summary>
        /// Delegate that retrieves the label of the cluster for a given point.
        /// 
        /// In the cited paper, this is the function C(e).
        /// </summary>
        private Func<TPoint, TLabel> Cluster { get; set; }

        #endregion

        #region The Outputs: Properties holding Comparison results (BCubed, Precision, Recall, Alpha) and cast to double

        /// <summary>
        /// Average correctness of all points over clusters.
        /// 
        /// This is a proxy for homogeneity: clusters should only contain members that belong together.
        /// Groups should be homogeneous on category.
        /// </summary>
        public double Precision { get; set; }

        /// <summary>
        /// Average correctness of all points over categories.
        /// 
        /// This is a proxy for completeness: all the members that belong together should be in the same group.
        /// Members of a category should not be split among multiple clusters.
        /// </summary>
        public double Recall { get; set; }

        /// <summary>
        /// Weight used when combining Precision and Recall to form BCubed.
        /// </summary>
        public double Alpha { get; set; }

        /// <summary>
        /// The BCubed measure of how similar the categorized and clustered groupings of points match.
        /// 
        /// Uses the cached result of Precision and Recall from the most recent call to Measure, or NaN if Measure has not been called yet.
        /// Uses the current value of Alpha to weight the two values.
        /// </summary>
        public double BCubed
        {
            get
            {
                if (double.IsNaN(Precision) || double.IsNaN(Recall))
                    return double.NaN;
                // Van Rijsbergen's F
                var f = 1.0 / ((Alpha / Precision) + (1.0 - Alpha) / Recall);
                return f;
            }
        }

        /// <summary>
        /// Convert to a double by returning BCubed.
        /// </summary>
        /// <param name="instance">A ClusterMetric.</param>
        /// <returns>BCubed, the weighted combination of Precision and Recall.</returns>
        public static implicit operator double(ClusterMetric<TPoint, TLabel> instance)
        {
            return instance.BCubed;
        }

        #endregion

        #region Methods that measure the correctness of the classification

        /// <summary>
        /// Measures the correctness of the relationship between two points.
        /// If two points are grouped together by category, they should be grouped together by cluster.
        /// If two points are not grouped together by category, they should not be grouped together by cluster.
        /// If both these constraints are satisfied for a pair of points, return 1.
        /// If either of these are not satisfied for a pair of points, return 0.
        /// </summary>
        /// <param name="p1">First point to compare.</param>
        /// <param name="p2">Second point to compare.</param>
        /// <returns>One for a correct relationship, zero for an incorrect one.</returns>
        private double Correctness(TPoint p1, TPoint p2)
        {
            return Category(p1).Equals(Category(p2)) == Cluster(p1).Equals(Cluster(p2)) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Measures the average correctness of a point with respect to all members of a given collection of points,
        /// possibly including itself.
        /// </summary>
        /// <param name="p1">Reference point.</param>
        /// <param name="points">Points from either a category or a cluster.</param>
        /// <returns>An average Correctness value, a number between zero and one, inclusive.</returns>
        private double AverageCorrectness(TPoint p1, IEnumerable<TPoint> points)
        {
            return points.Average(p2 => Correctness(p1, p2));
        }

        /// <summary>
        /// Measure how closely the clustering of the points matches their ideal categorization.
        /// </summary>
        /// <returns>One if the clustering matches the categorization perfectly, or a number between zero and one
        /// if there are deviations from a perfect clustering. The worse the clustering, the lower the number.</returns>
        public double Measure()
        {
			if (Points == null)
			{
				// Special case for the default constructor which composes a "perfect" result.
				Precision = 1.0;
				Recall = 1.0;
			}
			else {
				Precision = 0.0;
				Recall = 0.0;
				var n = Points.Count;
				switch (n)
				{
					case 0:
						Precision = double.NaN;
						Recall = double.NaN;
						break;
					case 1:
						Precision = 1;
						Recall = 1;
						break;
					default:
						Parallel.Invoke(
							() => Precision = Points.Average(p => AverageCorrectness(p, PointsInCluster(Cluster(p)))),
							() => Recall = Points.Average(p => AverageCorrectness(p, PointsInCategory(Category(p))))
						);
						break;
				}
			}
            return BCubed;
        }



        #endregion

        public override string ToString()
        {
            return string.Format("BCubed = {0:N4}, Precision (homogeneity) = {1:N4}, Recall (completeness) = {2:N4}", BCubed, Precision, Recall);
        }

        #region Constructors

        /// <summary>
        /// Construct a metric for a given set of points that have been clustered one way (unsupervised)
        /// but should be ideally categorized another way (gold standard).
        /// 
        /// Note: Category labels will be compared to category labels, and cluster labels will be compared to cluster labels.
        /// Cluster labels and category labels will never be compared to one another. It is assumed that they
        /// are unrelated. Only the groupings inferred by the labels matter.
        /// </summary>
        /// <param name="points">Points that have been clustered.</param>
        /// <param name="pointsInCategory">Delegate that retrieves all the points in the category with a given label.</param>
        /// <param name="pointsInCluster">Delegate that retrieves all the points in the cluster with a given label.</param>
        /// <param name="categoryLabel">Delegate that returns the ideal (gold standard) category label for a point.</param>
        /// <param name="clusterLabel">Delegate that returns the experimental (unsupervised) cluster label for a point.</param>
        /// <param name="alpha">Weighting factor to use when composing Precision and Recall into a single measurement value.
        /// The default is one-half, which weights them ewually. This number must be a value between zero and one.
        /// If higher than a half, it weights the inverse Precision more, otherwise the inverse Recall more.
        /// </param>
        public ClusterMetric(IList<TPoint> points, 
            Func<TLabel, IEnumerable<TPoint>> pointsInCategory, 
            Func<TLabel, IEnumerable<TPoint>> pointsInCluster, 
            Func<TPoint, TLabel> categoryLabel, 
            Func<TPoint, TLabel> clusterLabel, 
            double alpha = 0.5)
        {
            Points = points;
            PointsInCategory = pointsInCategory;
            PointsInCluster = pointsInCluster;
            Category = categoryLabel;
            Cluster = clusterLabel;
            Precision = double.NaN;
            Recall = double.NaN;
            Alpha = alpha;
            if (alpha < 0 || alpha > 1.0)
                throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "value must be between zero and one, inclusively.");
        }

		/// <summary>
		/// Create a "perfect" result which asserts that two Classifications are clustered identically if Measure() is called.
		/// </summary>
		public ClusterMetric()
		{
			Precision = 1;
			Recall = 1;
			Alpha = 0.5;
		}

        #endregion

    }
}
