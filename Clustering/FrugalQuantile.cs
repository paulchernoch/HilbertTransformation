using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustering
{
    /// <summary>
    /// Estimate the median or any quantile of a list of integers frugally, 
    /// using little auxiliary memory and making a single pass over the data.
    /// </summary>
    /// <remarks>
    /// Maintain a running estimate of a quantile over a stream with very small memory requirements
    /// using the algorithm frugal_2u found in:
    /// http://arxiv.org/pdf/1407.1121v1.pdf
    /// "Frugal Streaming for Estimating Quantiles: One (or two) memory suffices" by Ma, Muthukrishnan and Sandler (2014).
    /// 
    /// One can, for instance, track the median value of a stream of data, or the 68th percentile, or the third decile.
    /// This estimate follows recent values of data; it is not an estimate over all time.
    /// Thus if the quantile you are measuring changes, this will adapt and track the new value.
    /// 
    /// Caveat: The published algorithm uses integers. While this implementation uses doubles, the quantile values cannot
    /// be resolved any finer than one, the minimum step size. To resolve to finer values would require small
    /// changes to this algorithm and much testing to decide how to balance convergence speed with accuracy.
    /// 
    /// Usage:
    /// 
    ///   // Let's track the median, which has quantile = 0.5.
    ///   var seed = 100; // Educated guess for the median.
    ///   var estimator = new FrugalQuantile(seed, 0.5, FrugalQuantile.LinearStepAdjuster);
    ///   IEnumerable data = ... your data ...;
    ///   foreach (var item in data) {
    ///       var newEstimate = estimator.Add(item);
    ///       // Do something with estimate...
    ///   }
    /// 
    /// Author: Paul A. Chernoch
    /// </remarks>
    public class FrugalQuantile
    {
        #region Standard functions you can use for StepAdjuster.

        /// <summary>
        /// Best step adjuster found so far because it converges fast without overshooting.
        /// Every time the step grows by an amount that increases by one:
        ///    1, 2, 4, 7, 11, 16, 22, 29...
        /// </summary>
        public static Func<int, int> LinearStepAdjuster = oldStep => oldStep + 1;

        /// <summary>
        /// Step adjuster used in the published paper, which is good, but not as good as LinearStepAdjuster.
        /// Every time the step increases by one:
        ///    1, 2, 3, 4, 5, 6...
        /// </summary>
        public static Func<int, int> ConstantStepAdjuster = oldStep => 1;

        #endregion

        #region Input parameters

        /// <summary>
        /// Quantile whose estimate will be maintained.
        /// If 0.5, the median will be estimated.
        /// If 0.75, the third quartile will be estimated.
        /// Id 0.2, the second decile (or first pentile) will be estimated.
        /// etc...
        /// </summary>
        public double Quantile { get; set; }

        /// <summary>
        /// Function to dynamically adjust the step size based on the previous step size.
        /// 
        /// This function is crucial in causing an estimate that is far off the mark to rapidly converge
        /// by increasing the step size every time that a new estimate is on the same side of the true answer.
        /// 
        /// NOTE: Best function found so far: 
        ///    StepAdjuster = step => step + 1;
        /// </summary>
        public Func<int, int> StepAdjuster { get; set; }

        #endregion

        #region Output parameters

        /// <summary>
        /// Estimate of the quantile value.
        /// 
        /// One can initialize this to a guess. A good guess will converge faster.
        /// </summary>
        public int EstimatedQuantile { get; set; }

        #endregion

        #region Internal state

        /// <summary>
        /// Amount to add to or subtract from the current estimate, depending on whether our estimate is too low or too high.
        /// 
        /// As the algorithm proceeds, this is adjusted up and down to improve convergence.
        /// </summary>
        private int Step { get; set; }

        /// <summary>
        /// Tracks whether the previous adjustment was to increase the Estimate or decrease it.
        /// 
        /// If +1, the Estimate increased.
        /// If -1, the Estimate decreased.
        /// This should always have the value +1 or -1.
        /// </summary>
        private SByte Sign { get; set; }

        /// <summary>
        /// Random number generator.
        /// 
        /// Note: One could refactor to use the C# Random class instead. I prefer FastRandom.
        /// </summary>
        private FastRandom Rand { get; set; }

        #endregion

        #region Constructors

        private static readonly Dictionary<int, String> QuantileNames = new Dictionary<int, string>()
    {
        { 2, "median" },
        { 3, "tercile" },
        { 4, "quartile" },
        { 5, "quintile" },
        { 6, "sextile" },
        { 7, "heptile" },
        { 8, "octile" },
        { 9, "nonile" },
        { 10, "decile" },
        { 12, "duo-decile" },
        { 20, "vigintile" },
        { 100, "percentile" },
        { 1000, "permille" }
    };

        private static readonly String[] Ordinals = new string[]
        {
        "0th",
        "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th",
        "11th", "12th", "13th", "14th", "15th", "16th", "17th", "18th", "19th", "20th",
        "21st", "22nd", "23rd", "24th", "25th", "26th", "27th", "28th", "29th", "30th",
        "31st", "32nd", "33rd", "34th", "35th", "36th", "37th", "38th", "39th", "40th",
        "41st", "42nd", "43rd", "44th", "45th", "46th", "47th", "48th", "49th", "50th",
        "51st", "52nd", "53rd", "54th", "55th", "56th", "57th", "58th", "59th", "60th",
        "61st", "62nd", "63rd", "64th", "65th", "66th", "67th", "68th", "69th", "70th",
        "71st", "72nd", "73rd", "74th", "75th", "76th", "77th", "78th", "79th", "80th",
        "81st", "82nd", "83rd", "84th", "85th", "86th", "87th", "88th", "89th", "80th",
        "91st", "92nd", "93rd", "94th", "95th", "96th", "97th", "98th", "99th", "100th",
        };

        /// <summary>
        /// Compose the proper name to identify the most common quantiles, such as "median", "3rd quartile", "75th percentile", etc.
        /// </summary>
        /// <param name="quantileNumerator">Numerator for the quantile.</param>
        /// <param name="quantileDenominator">Denominator for the quantile.</param>
        /// <returns>Name of the quantile. If the numerator and denominator are not common values,
        /// name it "#-quantile".</returns>
        private static string ComposeName(double quantileNumerator, double quantileDenominator)
        {
            if (quantileNumerator <= 0) throw new ArgumentOutOfRangeException("quantileNumerator", "Must be positive.");
            if (quantileDenominator <= quantileNumerator) throw new ArgumentOutOfRangeException("quantileDenominator", "Must be greater than quantileNumerator.");
            if (quantileNumerator == Math.Floor(quantileNumerator) && quantileDenominator == Math.Floor(quantileDenominator))
            {
                var numerator = (int)quantileNumerator;
                var denominator = (int)quantileDenominator;
                if (denominator == 2) return QuantileNames[2]; // "median"

                if (!QuantileNames.TryGetValue(denominator, out string denominatorName))
                    denominatorName = $"{denominator}-quantile";

                string numeratorName = numerator < Ordinals.Length ? Ordinals[numerator] : numerator.ToString();
                return $"{numeratorName} {denominatorName}";
            }
            else
            {
                return $"quantile {quantileNumerator / quantileDenominator:F3}";
            }
        }

        /// <summary>
        /// Create a FrugalQuantile to track a running ESTIMATE of a quantile value, NOT an EXACT value.
        /// 
        /// If the quantileNumerator and quantileDenominator are both integers, then the auto-generated name will be
        /// nice, such as "3rd quartile" or "70th percentile".
        /// </summary>
        /// <param name="seed">Initial estimate for the quantile.
        /// A good initial estimate permits more rapid convergence.</param>    
        /// <param name="quantileNumerator">Numerator of fraction for Quantile to estimate.
        /// This value must be positive.
        /// </param>
        /// <param name="quantileDenominator">Denominator of fraction for Quantile to estimate.
        /// This value must be greater than the quantileNumerator, so that the Quantile falls in the exclusive range [0,1].
        /// </param>
        /// <param name="stepAdjuster">Function that can update the step size to improve the rate of convergence.
        /// Its parameter is the previous step size.
        /// The default lambda for this parameter is good, but there are better functions, like this one:
        ///     stepAdjuster = step => step + 1
        /// Researching the function best for your data is recommended.
        /// </param>
        public FrugalQuantile(int seed, double quantileNumerator, double quantileDenominator, Func<int, int> stepAdjuster = null) 
        {
            Quantile = quantileNumerator / quantileDenominator;
            Step = 1;
            Sign = 1;
            EstimatedQuantile = seed;
            // Default lambda for StepAdjuster shown below always return a step change of 1.
            // This default is per the published algorithm but testing shows a different function works much better: 
            //      StepAdjuster = oldStep => oldStep + 1 (aka LinearStepAdjuster).
            StepAdjuster = stepAdjuster ?? ConstantStepAdjuster;
            Rand = new FastRandom();
        }

        #endregion

        private double Random() { return Rand.NextDouble(); }

        /// <summary>
        /// Update the quantile Estimate to reflect the latest value arriving from the stream and return that estimate.
        /// </summary>
        /// <param name="item">Data Item arriving from the stream.
        /// Note: This algorithm was designed for use on non-negative integers. Its accuracy or suitability
        /// for negative values is not guaranteed.
        /// </param>
        /// <returns>The new Estimate.</returns>
        public double Add(int item)
        {

            // Use a Monte-Carlo approach to decide probabilistically whether the current estimate is too high or too low
            // and stepping up or down accordingly.
            // This is implemented to closely resemble the pseudo code for function frugal_2u on this page:
            //   http://research.neustar.biz/2013/09/16/sketch-of-the-day-frugal-streaming/
            // based on this paper:
            //   https://arxiv.org/pdf/1407.1121.pdf

            var m = EstimatedQuantile;
            var q = Quantile;
            // Published algorithm's step adjuster (same as ConstantStepAdjustor) always increments by one.
            // LinearStepAdjuster does much better.
            var f = StepAdjuster;
            var rand = Random(); // The paper by Qiang and Muthu draws one random number, while the commentator draws two!!!
            if (item > m && rand > 1 - q)
            {
                // Increment the step size if and only if the estimate keeps moving in
                // the same direction. Step size is incremented by the result of applying
                // the specified step function to the previous step size.
                Step += (Sign > 0 ? 1 : -1) * f(Step);
                // Increment the estimate by step size if step is positive. Otherwise,
                // increment the step size by one.
                m += Step > 0 ? Step : 1;

                // If the estimate overshot the item in the stream, pull the estimate back
                // and re-adjust the step size.
                if (m > item)
                {
                    Step += (item - m);
                    m = item;
                }

                // Damp down the step size to avoid oscillation.
                //if (Sign < 0 && Step > 1) // published logic, but suspicious
                if (Sign < 0 && Step < -1)
                    Step = 1;

                // Mark that the estimate increased this step
                Sign = 1;
            }
            else if (item < m && rand > q)
            {
                // If the item is less than the stream, follow all of the same steps as
                // above, with signs reversed.
                Step += (Sign < 0 ? 1 : -1) * f(Step);
                m -= Step > 0 ? Step : 1;
                Sign = -1;
                if (m < item)
                {
                    Step += (m - item);
                    m = item;
                }

                // Damp down the step size to avoid oscillation.
                if (Sign > 0 && Step > 1)
                    Step = 1;

                // Mark that the estimate decreased this step
                Sign = -1;
            }

            EstimatedQuantile = m;
            // Debug.WriteLine($"Item {item} -> Step {Step}, Sign {Sign} -> Est {EstimatedQuantile}");
            return EstimatedQuantile;
        }

        public int AddRange(IEnumerable<int> values)
        {
            foreach (var i in values)
                Add(i);
            return EstimatedQuantile;
        }

        /// <summary>
        /// Estimates the value at the given quantile for a collection of data.
        /// </summary>
        /// <param name="data">The data to analyze. 
        /// Best results for randomly ordered, Gaussian distributed data. 
        /// Poorest results for sorted data or data drawn from an unusual distribution.</param>
        /// <param name="quantileNumerator">Quantile numerator. 
        /// For the third quartile, use three, for example.</param>
        /// <param name="quantileDenominator">Quantile denominator.
        /// For the third quartile, use four, for example.
        /// </param>
        /// <param name="seed">Seed value to use as initial value of the estimate.</param>
        /// <returns>The quantile value.
        /// For example, if quantileNumerator / quantileDenominator is one half, the median value is returned.
        /// </returns>
        public static int Estimate(IEnumerable<int> data, double quantileNumerator = 50, double quantileDenominator = 100, int seed = 0)
        {
            var probe = new FrugalQuantile(seed, quantileNumerator, quantileDenominator)
            {
                StepAdjuster = LinearStepAdjuster
            };
            return probe.AddRange(data);
        }

        /// <summary>
        /// Shuffles the data randonly and then estimates the value at the given quantile.
        /// 
        /// It chooses as its seed the mean of the first three items after shuffling.
        /// </summary>
        /// <param name="data">The data to analyze. 
        /// Best results for randomly ordered, Gaussian distributed data. 
        /// Poorest results for sorted data or data drawn from an unusual distribution.</param>
        /// <param name="quantileNumerator">Quantile numerator. 
        /// For the third quartile, use three, for example.</param>
        /// <param name="quantileDenominator">Quantile denominator.
        /// For the third quartile, use four, for example.
        /// </param>
        /// <returns>The quantile value.
        /// For example, if quantileNumerator / quantileDenominator is one half, the median value is returned.
        /// </returns>
        public static int ShuffledEstimate(IEnumerable<int> data, double quantileNumerator = 50, double quantileDenominator = 100, Func<int, int> stepAdjuster = null)
        {
            var dataArray = data.ToArray();
            if (dataArray.Length == 0)
                return 0;
            else if (dataArray.Length < 100)
            {
                Array.Sort(dataArray);
                return dataArray[(int)Math.Round((dataArray.Length - 1) * quantileNumerator / quantileDenominator,0)];
            }
            else
            {
                var shuffledData = dataArray.Shuffle();
                Array.Sort(shuffledData, 0, 21);
                var seed = shuffledData[(int) (20 * quantileNumerator / quantileDenominator)];
                
                var probe = new FrugalQuantile(seed, quantileNumerator, quantileDenominator)
                {
                    StepAdjuster = stepAdjuster ?? FrugalQuantile.LinearStepAdjuster
                };
                var estimate = probe.AddRange(shuffledData);
                // Console.WriteLine($"seed = {seed}, estimate = {estimate}");
                return estimate;
            }
            

        }

    }
}
