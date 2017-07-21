using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;
using static System.Double;
using HilbertTransformation.Random;

namespace HilbertTransformationTests.Data
{
    /// <summary>
    /// Yields an approximate probability density function (PDF) and cumulative density function (CDF)
    /// for a Zipf distribution.
    /// 
    /// The distribution is discrete.
    /// 
    ///    P(r) = C / r^α
    ///    
    /// where:
    ///    P(r) is the discrete probability density for rank r 
    ///    r is the rank from one to N
    ///    C is the normalization constant (and the value that is approximated)
    ///    α is alpha, the Zipf exponent, which must be positive and is usually in the range 0.4 ≤ Alpha ≤ 1
    /// 
    /// See formulas 6 and 16 in "Approximation of the truncated Zeta distribution and Zipf's law" by Maurizio Naldi.
    /// https://arxiv.org/abs/1511.01480
    /// </summary>
    /// <remarks>
    /// Note: The computations may be numerically unstable for values of Alpha near one (but not not exactly one).
    /// How near? Unsure, but if |1 - Alpha| ≥ 0.0001, it should be okay.
    /// </remarks>
    public class ZipfDistribution
    {

        public enum InterpolationMethod {  Linear, Quadratic, Hyperbolic }

        /// <summary>
        /// Number of items to rank. 
        /// </summary>
        public int N { get; }

        /// <summary>
        /// The exponential factor controlling the shape of the Zipf curve.
        /// This value must be greater than zero, and is typically 0.4 ≤ Alpha ≤ 1.
        /// </summary>
        public double Alpha { get; }

        /// <summary>
        /// Quality factor. 
        /// 
        /// The larger the value, the more precise a PDF value is returned, but the more costly the computation.
        /// This must be two or larger, but five gives good accuracy for Alpha values up to two.
        /// </summary>
        public int K { get; }

        /// <summary>
        /// Normalization constant, approximated using Formula 16 from the paper and dependent on N, K and Alpha.
        /// </summary>
        public double C { get; }

        /// <summary>
        /// Inverse CDF Interpolation quality. The relative error for each rank will not exceed this.
        /// 
        /// A number between zero and one that affects how many interpolation points will
        /// be generated for the ICDF computation. The lower the value, the more interpolation points
        /// will be generated. 
        /// 
        /// If zero, then no interpolation is performed; all N CDF values are stored.
        /// </summary>
        public double Epsilon { get; }

        public InterpolationMethod InterpolateBy { get; private set; }

        private List<double> InterpolationCDFValues = new List<double>();
        public List<int> InterpolationCDFRanks { get; private set; }  = new List<int>();

        /// <summary>
        /// Number of interpolation points.
        /// </summary>
        public int InterpolationSize { get { return InterpolationCDFValues.Count(); } }

        private FastRandom RandomNumbers { get; set; }  = new FastRandom();


        public ZipfDistribution(int n, double alpha, int k, double epsilon, InterpolationMethod interpolateBy = InterpolationMethod.Hyperbolic)
        {
            N = n;
            Alpha = alpha;
            K = Max(2,k);
            C = Normalize();
            Epsilon = epsilon;
            InterpolateBy = interpolateBy;
            InitInterpolation();
        }

        /// <summary>
        /// Create a ZipfDistribution where the normalization constant is computed exactly, not approximately.
        /// 
        /// This is equivalent to setting K to N, which ceases to be an approximation. 
        /// </summary>
        /// <param name="n">Number of distinct rank values that can be returned.</param>
        /// <param name="alpha">Exponential power used for the Zipf distribution, typically in the range 0.4 ≤ Alpha ≤ 1.</param>
        /// <param name="epsilon"></param>
        public ZipfDistribution(int n, double alpha, double epsilon, InterpolationMethod interpolateBy = InterpolationMethod.Hyperbolic) : this(n,alpha,n,epsilon, interpolateBy)
        {
        }

        /// <summary>
        /// Euler's constant.
        /// </summary>
        private static readonly double EulerGamma = 0.5772156649015328;

        /// <summary>
        /// Compute the normalization factor from formulas 6 and 16.
        /// </summary>
        /// <returns>The value to use for C.</returns>
        private double Normalize()
        {
            double denominator = 0.0;
            if (K == N)
            {
                for (var i = 1; i <= N; i++)
                    denominator += Pow(i, -Alpha);
            }
            else if (Alpha == 1)
            {
                // Formula 6 in Naldi's paper, which is independent of K. 
                // This is Leonhard Euler's classic approximation of the finite Harmonic series
                denominator = EulerGamma + Log(N) + 0.5 / N;
            }
            else
            {
                // Fornula 16 in Naldi's paper, which is undefined at Alpha = 1, and unstable in its neighborhood
                denominator
                    = Pow(N, -Alpha) * (0.5 + N / (1 - Alpha))
                    + Pow(K, -Alpha) * (0.5 - K / (1 - Alpha));
                for (var i = 1; i <= K - 1; i++)
                    denominator += Pow(i, -Alpha);
            }
            return 1 / denominator;
        }

        /// <summary>
        /// Estimated probability density for the given rank, which must be in the range 1..N.
        /// 
        /// If Epsilon is zero, then C is exact and the estimate is exact (to within the accuracy of the Pow function).
        /// </summary>
        /// <param name="rank">Rank of item whose PDF is sought.</param>
        /// <returns>A probability value between zero and one.</returns>
        public double PDF(int rank)
        {
            return C * Pow(rank, -Alpha);
        }

        /// <summary>
        /// Estimated cumulative probability density for the given rank.
        /// </summary>
        /// <param name="rank">Rank of item whose CDF is sought.</param>
        /// <returns>A cumulative probability value between zero and one.</returns>
        public double CDF(int rank)
        {
            // NOTE: This is expensive: execution time proportional to rank/2.
            if (rank >= N)
                return 1.0;
            var cdf = 0.0;
            if (rank <= N / 2) { 
                // Start from rank zero and add
                for (var i = 1; i <= rank; i++)
                    cdf += PDF(i);
            }
            else
            {
                // Start from rank N, going backwards and subtract
                cdf = 1.0;
                for (var i = N; i > rank; i--)
                    cdf -= PDF(i);
            }
            return Min(1.0, cdf);
        }

        /// <summary>
        /// Interpolate the CDF using the control points.
        /// </summary>
        /// <param name="rank">Rank for which the CDF is sought.</param>
        /// <returns>The cumulative density value for the given rank, which varies from a small value at rank = 1
        /// to one for rank = N.</returns>
        public double ApproximateCDF(int rank)
        {
            // NOTE: This is expensive: execution time proportional to rank/2.
            if (rank >= N)
                return 1.0;
            if (rank <= 1)
                return PDF(1);

            var position = InterpolationCDFRanks.BinarySearch(rank);
            if (position > 0) return InterpolationCDFValues[position]; // Exact match - no interpolation needed.
            position = ~position;
            Interpolator<double> interpolator = CDFInterpolator(position);
            return Min(1, interpolator.Y(rank));
        }

        /// <summary>
        /// Create an interpolator of CDF values for the interpolation segment that ends at the given position.
        /// </summary>
        /// <param name="position">Zero-based position into the InterpolationCDFRanks and InterpolationCDFValues lists.
        /// This value must be one or greater.</param>
        /// <returns>An interpolator whose type is specified by InterpolateBy, unless there are not enough interpolation points
        /// to support that method. E.g. Quadratic requires three points.</returns>
        private Interpolator<double> CDFInterpolator(int position)
        {
            if (position <= 0)
                throw new ArgumentOutOfRangeException("position", "Value must be one or greater");
            Interpolator<double> interpolator;
            var topRank = InterpolationCDFRanks[position];
            var bottomRank = InterpolationCDFRanks[position - 1];
            var topCdf = InterpolationCDFValues[position];
            var bottomCdf = InterpolationCDFValues[position - 1];
            // If there are not enough points for the method, use a method that requires fewer points.
            var interpolationMethod = InterpolationSize <= 2 ? InterpolationMethod.Hyperbolic : InterpolateBy;

            // Edge case: When you get to the tail near where CDF reaches one, using a hyperbola to interpolate
            // breaks down, because with a finite CDF we actually reach the asymptote.
            if (topCdf >= 0.99999 && InterpolationSize > 2)
                interpolationMethod = InterpolationMethod.Quadratic;

            switch (interpolationMethod)
            {
                case InterpolationMethod.Quadratic:
                    var thirdPosition = position >= 2 ? position - 2 : position + 1;
                    var thirdCdf = InterpolationCDFValues[thirdPosition];
                    var thirdRank = InterpolationCDFRanks[thirdPosition];
                    //TODO: We could cache these interpolators for reuse to improve performance
                    interpolator = new LagrangeQuadraticInterpolator(bottomRank, bottomCdf, topRank, topCdf, thirdRank, thirdCdf);
                    break;
                case InterpolationMethod.Hyperbolic:
                    interpolator = new HyperbolicInterpolator(bottomRank, bottomCdf, topRank, topCdf, 1);
                    break;
                case InterpolationMethod.Linear:
                default:
                    interpolator = new LinearInterpolator(bottomRank, bottomCdf, topRank, topCdf);
                    break;
            }
            return interpolator;
        }

        /// <summary>
        /// Perform an inverse CDF to find the rank closest to the given CDF value.
        /// 
        /// This uses a binary search to find the nearest grid points, then uses the chosen method of
        /// interpolation between those two grid points to estimate the rank that correspopnds to the CDF value.
        /// </summary>
        /// <param name="cdf">CDF value to find.</param>
        /// <returns>Rank whose corresponding CDF value is closest to the given cdf.</returns>
        public int Rank(double cdf)
        {
            if (cdf <= InterpolationCDFValues[0]) return 1;
            if (cdf >= 1) return N;
            var position = InterpolationCDFValues.BinarySearch(cdf);
            if (position > 0) return InterpolationCDFRanks[position];
            position = ~position;
            Interpolator<double> interpolator = RankInterpolator(position);
            return (int)Ceiling(interpolator.Y(cdf));
        }

        private Interpolator<double> RankInterpolator(int position)
        {
            if (position <= 0)
                throw new ArgumentOutOfRangeException("position", "Value must be one or greater");
            Interpolator<double> interpolator;
            var topRank = InterpolationCDFRanks[position];
            var bottomRank = InterpolationCDFRanks[position - 1];
            var topCdf = InterpolationCDFValues[position];
            var bottomCdf = InterpolationCDFValues[position - 1];
            // If there are not enough points for the method, use a method that requires fewer points.
            var interpolationMethod = InterpolationSize <= 2 ? InterpolationMethod.Hyperbolic : InterpolateBy;

            // Edge case: When you get to the tail near where CDF reaches one, using a hyperbola to interpolate
            // breaks down, because with a finite CDF we actually reach the asymptote.
            if (topCdf >= 0.99999 && InterpolationSize > 2)
                interpolationMethod = InterpolationMethod.Quadratic;

            switch (interpolationMethod)
            {
                case InterpolationMethod.Quadratic:
                    var thirdPosition = position >= 2 ? position - 2 : position + 1;
                    var thirdCdf = InterpolationCDFValues[thirdPosition];
                    var thirdRank = InterpolationCDFRanks[thirdPosition];
                    //TODO: We could cache these polynomials for reuse
                    interpolator = new LagrangeQuadraticInterpolator(bottomCdf, bottomRank, topCdf, topRank, thirdCdf, thirdRank);
                    break;
                case InterpolationMethod.Hyperbolic:
                    // Create an interpolator with the same values as for CDF interpolation, but inverted.
                    interpolator = new HyperbolicInterpolator(bottomRank, bottomCdf, topRank, topCdf, 1, true);
                    break;
                case InterpolationMethod.Linear:
                default:
                    interpolator = new LinearInterpolator(bottomCdf, bottomRank, topCdf, topRank);
                    break;
            }
            return interpolator;
        }

        /// <summary>
        /// Generate a random rank between one and N (inclusively) conforming to a Zipf distribution.
        /// </summary>
        /// <returns>A rank between one and N, inclusive, chosen randomly according to a Zipf distribution.</returns>
        public int NextRandomRank()
        {
            var randomCdf = RandomNumbers.NextDouble();
            return Rank(randomCdf);
        }

        /// <summary>
        /// Store selected ranks and their corresponding CDF values as spline control points.
        /// 
        /// The values that are not stored can be interpolated from nearby values that are.
        /// 
        /// Attempt to store as few points as possible while guaranteeing a maximum error of Epsilon, to save memory.
        /// </summary>
        private void InitInterpolation()
        {
            if (Epsilon <= 0)  
                InitInterpolationWithZeroError();
            else
                InitInterpolationWithBoundedError(Epsilon);
        }

        /// <summary>
        /// Perform no interpolation; store CDF values for all ranks, which may consume much memory.
        /// </summary>
        private void InitInterpolationWithZeroError()
        {
            var cdf = 0.0;
            for (var rank = 1; rank <= N; rank++)
            {
                cdf += PDF(rank);
                InterpolationCDFRanks.Add(rank);
                InterpolationCDFValues.Add(cdf);
            }
        }

        private void InitInterpolationWithBoundedError(double maxPermittedError)
        {
            int rank = 1, startRank = 1, midPointRank = 1;
            var prevPdf = PDF(rank);
            var prevCdf = prevPdf;
            var midPointCdf = prevCdf;
            
            InterpolationCDFRanks.Add(1);
            InterpolationCDFValues.Add(prevCdf);

            // Advance three pointers, one at the start of the segment, one at the midpoint, and one at the end.
            // The midpoint advances at half the speed of the end of the segment.
            for (rank = 2; rank <= N - 1; rank++)
            {
                var currPdf = PDF(rank);
                var currCdf = Min(1.0, prevCdf + currPdf);
                if ((rank - startRank) % 2 == 0)
                {
                    midPointRank++;
                    midPointCdf += PDF(midPointRank);
                }

                // No need to interpolate if the segment has no gap that needs interpolating.  
                if (rank - startRank >= 2)
                {
                    // Add a provisional interpolation point.
                    InterpolationCDFRanks.Add(rank);
                    InterpolationCDFValues.Add(currCdf);

                    // Calculate the interpolation error for the midpoint.
                    // If it is still small enough, remove the provisional interpolation point.
                    var endPointPosition = InterpolationSize - 1;
                    var cdfInterpolator = CDFInterpolator(endPointPosition);
                    var estimatedMidpointCdf = Min(1.0, cdfInterpolator.Y(midPointRank));
                    var relativeError = Abs(midPointCdf - estimatedMidpointCdf) / midPointCdf;

                    InterpolationCDFRanks.RemoveAt(endPointPosition);
                    InterpolationCDFValues.RemoveAt(endPointPosition);

                    // It is not guaranteed that the midpoint of the segment is where the worst error would be found.
                    // Thus permit less error than requested in the hope that this will keep the true maximum error 
                    // below maxPermittedError as well.
                    if (relativeError >= maxPermittedError / 7)
                    {
                        // We decided we need an interpolation point, thus are starting a new segment.
                        midPointRank = startRank = rank - 1;
                        midPointCdf = prevCdf;
                        InterpolationCDFRanks.Add(rank - 1);
                        InterpolationCDFValues.Add(prevCdf);
                    }
                }
                prevPdf = currPdf;
                prevCdf = currCdf;
            }
            // Close out the last segment.
            InterpolationCDFRanks.Add(N);
            InterpolationCDFValues.Add(1);
        }

        public override string ToString()
        {
            var s = "[";
            
            var i = 0;
            for (; InterpolationCDFRanks[i] == i + 1 && i < InterpolationCDFRanks.Count; i++){}
            s += $"1-{i - 1}";
            var ranksToShow = 100 + i;
            for (; i < ranksToShow && i < InterpolationSize; i++)
            {
                s += ",";
                s += InterpolationCDFRanks[i];
            }
            if (InterpolationSize > ranksToShow)
            {
                s += "...";
                s += InterpolationCDFRanks.Last();
            }
            s += "]";
            return $"Zipf for N = {N}, α = {Alpha}, K = {K}, C = {C}, ϵ = {Epsilon}. Size = {InterpolationSize}. Ranks = {s}";
        }
    }
}
