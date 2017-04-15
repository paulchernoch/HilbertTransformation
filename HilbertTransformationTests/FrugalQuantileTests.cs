using Clustering;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests
{

    [TestFixture]
    public class FrugalQuantileTests
    {
        /// <summary>
        /// Find the median of all integers from zero to 999, presented in random order.
        /// 
        /// Since these are in a linear distribution, not a Gaussian distribution, the estimate might be poor.
        /// </summary>
        [Test]
        public void EstimateMedianOfOneThousandIntegers()
        {
            //var actualMedian = FrugalQuantile.ShuffledEstimate(Enumerable.Range(0, 1000).ToList(), 1,2, FrugalQuantile.ConstantStepAdjuster);
            var actualMedian = FrugalQuantile.ShuffledEstimate(Enumerable.Range(0, 1000).ToList(), 1, 2, FrugalQuantile.LinearStepAdjuster);

            var msg = $"Estimated median of one thousand integers at 500 is {actualMedian}, should be near 500";
            Debug.WriteLine(msg);
            Assert.IsTrue(actualMedian >= 450 && actualMedian <= 550, msg);
        }

        /// <summary>
        /// Find the median of 1000 integers in a Gaussian distribution.
        /// 
        /// Since these are in a Gaussian distribution, the estimate should be bettern than a linear distribution.
        /// </summary>
        [Test]
        public void EstimateMedianOfGaussianDistribution()
        {
            var gaussianRng = new ZigguratGaussianSampler();
            var testData = Enumerable.Range(0, 1000).Select(i => Math.Abs((int)gaussianRng.NextSample(500, 250))).ToList();
            var actualMedian = FrugalQuantile.ShuffledEstimate(testData, 1, 2, FrugalQuantile.LinearStepAdjuster);

            var msg = $"Estimated median of numbers following a Gaussian distribution is {actualMedian}, should be near 500";
            Debug.WriteLine(msg);
            Assert.IsTrue(actualMedian >= 450 && actualMedian <= 550, msg);
        }
    }
}
