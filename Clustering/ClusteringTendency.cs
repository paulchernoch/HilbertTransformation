using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static System.Math;

namespace Clustering
{
    /// <summary>
    /// Assess whether a set of points has a clustering tendency or not.
    /// 
    /// If data is uniformly randomly selected, it will have no clustering tendency, so attempting to cluster it will produce
    /// meaningless results. If all data is in small outlying groups or one large central group, it is similarly not clustered.
    /// 
    /// Checking for clustering tendency is an efficient process:
    ///   * linear in N (number of points)
    ///   * linear in D (number of dimensions)
    ///   * constant in B (number of bits per dimension)
    /// </summary>
    public class ClusteringTendency
    {
        public enum ClusteringQuality {
            /// <summary> No clustering tendency, with all points in groups smaller than the outlier size. </summary>
            Unclustered,
            /// <summary>All points that are not in outlying groups are in a single, large cluster. </summary>
            SinglyClustered,
            /// <summary>Of the points that are not outliers, two thirds or more are in a single, large cluster. </summary>
            MajorityClustered,
            /// <summary>Two-thirds or more of the points are outliers. </summary>
            WeaklyClustered,
            /// <summary>Between one- and two-thirds of the points are outliers. </summary>
            ModeratelyClustered,
            /// <summary>Fewer than one-third of the points are outliers.</summary>
            HighlyClustered
        }

        /// <summary>
        /// Size used to determine which groups are outliers and which are clusters.
        /// </summary>
        public int OutlierSize { get; private set; }

        /// <summary>
        /// Estimated number of clusters NOT smaller than the outlier size.
        /// </summary>
        public int LargeClusterCount { get; private set; }

        /// <summary>
        /// Estimated number of points in large clusters.
        /// </summary>
        public int LargeClusterMembership { get; private set; }

        /// <summary>
        /// Estimated number of points in the single largest cluster.
        /// </summary>
        public int LargestClusterMembership { get; private set; }

        /// <summary>
        /// Number of clusters smaller than the outlier size.
        /// </summary>
        public int OutlierCount { get; private set; }

        /// <summary>
        /// Estimated number of points in outliers.
        /// </summary>
        public int OutlierMembership { get; private set; }

        /// <summary>
        /// Percent of all points that are in outlying groups.
        /// </summary>
        public double OutlierPercent {
            get
            {
                return OutlierMembership == 0 ? 0 : (100.0 * OutlierMembership / (OutlierMembership + LargeClusterMembership));
            }
        }

        public double LargeClusterPercent { get { return 100.0 - OutlierPercent;  } }

        /// <summary>
        /// Percent of points in large clusters that are in the largest cluster.
        /// 
        /// If this number exceeds two-thirds, the points are likely MajorityClustered.
        /// NOTE: The basis of the percentage is the number of points in all large clusters, not the full number of points.
        /// </summary>
        public double LargestClusterPercent {
            get {
                return LargeClusterMembership == 0 ? 0 : (100.0 * LargestClusterMembership / LargeClusterMembership);
            }
        }

        /// <summary>
        /// Qualitative assessment of how clustered the points are.
        /// 
        /// There is no point to try clustering the data if it is Unclustered or SinglyClustered.
        /// </summary>
        public ClusteringQuality HowClustered {
            get
            {
                if (LargeClusterCount == 0 || OutlierPercent >= 95.0)
                    return ClusteringQuality.Unclustered;
                if (LargeClusterCount == 1)
                    return ClusteringQuality.SinglyClustered;
                if (LargestClusterPercent > 66.666)
                    return ClusteringQuality.MajorityClustered;
                if (OutlierPercent > 66.666)
                    return ClusteringQuality.WeaklyClustered;
                if (OutlierPercent > 33.333)
                    return ClusteringQuality.ModeratelyClustered;
                else
                    return ClusteringQuality.HighlyClustered;
            }
                
        }


        public ClusteringTendency(IReadOnlyList<UnsignedPoint> points, int outlierSize)
        {
            OutlierSize = outlierSize;
            var tallies = Analyze(points);
        }

        private Dictionary<BigInteger, int> Analyze(IReadOnlyList<UnsignedPoint> points)
        {
            var balancer = new PointBalancer(points);
            var hilbertIndexTallies = new Dictionary<BigInteger, int>();
            LargestClusterMembership = 0;
            LargeClusterCount = 0;
            LargeClusterMembership = 0;
            foreach (var point in points)
            {
                var hIndex = balancer.ToHilbertPosition(point, 1);
                hilbertIndexTallies.TryGetValue(hIndex, out int tally);
                tally++;
                LargestClusterMembership = Max(LargestClusterMembership, tally);
                if (tally == OutlierSize)
                {
                    LargeClusterCount++;
                    LargeClusterMembership += tally;
                }
                else if (tally > OutlierSize)
                    LargeClusterMembership++;
                hilbertIndexTallies[hIndex] = tally;
            }
            OutlierMembership = points.Count - LargeClusterMembership;
            OutlierCount = hilbertIndexTallies.Count - LargeClusterCount;
            return hilbertIndexTallies;
        }

        public override string ToString()
        {
            var largeClusterPhrase = LargeClusterCount == 0 ? "No large clusters." : $"{LargeClusterPercent} % of points in {LargeClusterCount} large clusters.";
            var outlierPhrase = OutlierCount == 0 ? " No outliers." : $" {OutlierPercent} % of points in {OutlierCount} outliers.";
            var majorityPhrase = LargeClusterCount == 0 ? "" : $" Largest contains {LargestClusterPercent} % of clustered points.";
            return $"{HowClustered} : {largeClusterPhrase}{outlierPhrase}{majorityPhrase}";
        }


    }
}
