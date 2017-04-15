using HilbertTransformation.Random;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class SizedBucketSortTests
    {
        #region Pivot Efficiency Tests

        // Validate this assumption: picking 2*sqrt(N) as the bucket count is a good compromise
        // between memory and performance.
        //
        // The pivot efficiency tests do not test the sorting algorithm itself, but merely validate
        // this assumption.

        /// <summary>
        /// The performance of SizedBucketSort dependes on random pivots dividing the data into roughly equal sized pieces. 
        /// If large buckets receive too many of the items, the sort key will have to be recreated more often and performance will suffer.
        /// We cannot use the efficient Quicksort on a bucket until it is below our ideal bucket size.
        /// 
        /// This test uses square root of N as the ideal bucket size.
        /// </summary>
        [Test]
        public void PivotEfficiency_RootN()
        {
            PivotEfficiencyCase(1000000, 1000, 1000, 25.0);
        }

        [Test]
        public void PivotEfficiency_TenPercentOverRootN()
        {
            PivotEfficiencyCase(1000000, 1100, 1100, 29.0);
        }

        [Test]
        public void PivotEfficiency_TwentyPercentOverRootN()
        {
            PivotEfficiencyCase(1000000, 1200, 1200, 40.0);
        }

        [Test]
        public void PivotEfficiency_TwentyFivePercentOverRootN()
        {
            PivotEfficiencyCase(1000000, 1250, 1250, 42.0);
        }

        [Test]
        public void PivotEfficiency_TwiceRootN()
        {
            PivotEfficiencyCase(1000000, 2000, 2000, 85.0);
        }

        [Test]
        public void PivotEfficiency_TwiceRootN_100000N()
        {
            PivotEfficiencyCase(100000, 632, 632, 85.0);
        }

        private void PivotEfficiencyCase(int n, int idealBucketSize, int bucketCount, double expectedPercent)
        {
            var percentages = new List<double>();
            var maxParallels = new List<int>();
            var repeats = n / bucketCount;
            var rng = new FastRandom();
            var allPivots = n.CreateRandomPermutation();
            foreach (var trial in Enumerable.Range(0, repeats))
            {
                var pivots = allPivots.Skip(trial * bucketCount).Take(bucketCount).ToList();
                pivots.Sort();
                var rangeStart = 0;
                var smallBucketTotal = 0;
                foreach (var pivot in pivots)
                {
                    if (pivot - rangeStart <= idealBucketSize)
                        smallBucketTotal += pivot - rangeStart;
                    rangeStart = pivot;
                }
                if (n - rangeStart <= idealBucketSize)
                    smallBucketTotal += n - rangeStart;
                percentages.Add(smallBucketTotal * 100.0 / (double)n);

                var start = 0;
                var maxParallel = 0;
                for (var i = 3; i < pivots.Count(); i++)
                {
                    var sum = Min(pivots[i-3] - start, idealBucketSize)
                        + Min(pivots[i - 2] - pivots[i - 3], idealBucketSize)
                        + Min(pivots[i - 1] - pivots[i - 2], idealBucketSize)
                        + Min(pivots[i] - pivots[i - 1], idealBucketSize);
                    maxParallel = Max(maxParallel, sum);
                    start = pivots[i-3];
                }
                maxParallels.Add(maxParallel);
            }
            var minPct = percentages.Min();
            var maxPct = percentages.Max();
            var meanPct = percentages.Average();
            var msg = $"For n = {n}, ideal bucket size of {idealBucketSize} and bucket count of {bucketCount} : Percent of items in buckets not exceeding the ideal size. Min = {minPct}  Max = {maxPct}  Mean = {meanPct}";
            Console.WriteLine(msg);
            Assert.GreaterOrEqual(meanPct, expectedPercent, $"Expected that at least {expectedPercent} % of items would be in small buckets");

            var minMaxParallel = maxParallels.Min();
            var maxMaxParallel = maxParallels.Max();
            var meanMaxParallel = maxParallels.Average();
            var msg2 = $"Parallel memory requirements: Min {minMaxParallel}  Max {maxMaxParallel}  Mean {meanMaxParallel}";
            Console.WriteLine(msg2);

        }

        #endregion


        #region Sort key creation count tests

        // Validate this assumption: LINQ OrderBy only computes the sort key once per item, not at each comparison.
        [Test]
        public void LinqOrderBySortKeyCount()
        {
            int counter = 0;
            int n = 10000;
            var values = n.CreateRandomPermutation();
            var sortedValues = values.OrderBy(i => { counter++; return i; }).ToList();
            Assert.AreEqual(n, counter, "Expected counter to match number of items sorted.");
        }

        #endregion
    }
}
