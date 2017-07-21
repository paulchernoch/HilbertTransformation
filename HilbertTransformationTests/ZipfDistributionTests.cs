using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static System.Math;

namespace HilbertTransformationTests
{
    /// <summary>
    /// Tests of the ZipfDistribution.
    /// </summary>
    [TestFixture]
    public class ZipfDistributionTests
    {
        /// <summary>
        /// As the rank increases, the Probability density should decrease.
        /// </summary>
        [Test]
        public void ZipfPDFValuesDecrease()
        {
            var n = 1000;
            var alpha = .01;
            var zipf = new ZipfDistribution(n, alpha, 5, 0.01);
            var prevPdf = zipf.PDF(1);
            Assert.IsTrue(prevPdf < 1 && prevPdf > 0, $"PDF(1) is out of range: {prevPdf}");
            for(var rank = 2; rank <= n; rank++)
            {
                var pdf = zipf.PDF(rank);
                Assert.Less(pdf, prevPdf, $"PDF({rank}) is not less than PDF({rank-1}): {pdf}");
                prevPdf = pdf;
            }
        }

        /// <summary>
        /// As the rank increases, the cumulative probability density should increase, ending at one.
        /// </summary>
        [Test]
        public void ZipfCDFValuesIncreaseToOne()
        {
            var n = 1000;
            var alpha = 0.9;
            var zipf = new ZipfDistribution(n, alpha, 5, 0.01);
            var prevCdf = zipf.CDF(1);
            Assert.IsTrue(prevCdf < 1 && prevCdf > 0, $"CDF(1) is out of range: {prevCdf}");
            for (var rank = 2; rank <= n; rank++)
            {
                var cdf = zipf.CDF(rank);
                Assert.Greater(cdf, prevCdf, $"CDF({rank}) is not greater than CDF({rank - 1}): {cdf}");
                prevCdf = cdf;
            }
            var actualCdf = zipf.CDF(n);
            Assert.AreEqual(1.0, actualCdf, $"CDF(N) should equal one, but is instead {actualCdf}");
        }

        /// <summary>
        /// Whan Alpha is not one, if NOT interpolating and the actual CDF is obtained for a given rank, then feeding that CDF in to get
        /// the inverse rank should yield the same rank, or be off by no more than one.
        /// </summary>
        [Test]
        public void ZipfUninterpolatedInverseRanksMatchWhenAlphaIsNotOne()
        {
            var n = 1000;
            var alpha = 0.9;
            var epsilon = 0; // No interpolation
            var zipf = new ZipfDistribution(n, alpha, 5, epsilon);
            for (var rank = 1; rank <= n; rank++)
            {
                var cdf = zipf.CDF(rank);
                var actualRank = zipf.Rank(cdf);
                Assert.IsTrue(Abs(rank - actualRank) <= 1, $"Interpolation failed for rank {rank}. CDF = {cdf}. Inverse Rank {actualRank}");
            }
        }

        ///Whan alpha is one, if NOT interpolating and the actual CDF is obtained for a given rank, then feeding that CDF in to get
        /// the inverse rank should yield the same rank, or be off by no more than one.
        /// </summary>
        [Test]
        public void ZipfUninterpolatedInverseRanksMatchWhenAlphaIsOne()
        {
            var n = 1000;
            var alpha = 1.0;
            var epsilon = 0; // Uses No interpolation
            var zipf = new ZipfDistribution(n, alpha, 5, epsilon);
            for (var rank = 1; rank <= n; rank++)
            {
                var cdf = zipf.CDF(rank);
                var actualRank = zipf.Rank(cdf);
                Assert.IsTrue(Abs(rank - actualRank) <= 1, $"Interpolation failed for rank {rank}. CDF = {cdf}. Inverse Rank {actualRank}");
            }
        }

        /// <summary>
        /// If interpolating and the actual CDF is obtained for a given rank, then feeding that CDF in to get
        /// the inverse rank should yield the same rank, or be off by no more than two.
        /// </summary>
        [Test]
        public void ZipfInterpolatedInverseRanksMatch()
        {
            var n = 10000;
            var k = 100;
            var alpha = 1.0;
            var epsilon = 0.0002; // Uses Interpolation
            var zipf = new ZipfDistribution(n, alpha, k, epsilon);
            var lowestRankWithDifference = new int[1000];
            var success = true;
            var detailedLog = "";
            var countWithDifferenceMoreThanTwo = 0;
            var prevDifference = 0;
            for (var rank = 1; rank <= n; rank++)
            {
                var cdf = zipf.CDF(rank);
                var actualRank = zipf.Rank(cdf);
                var trueDifference = rank - actualRank;
                var difference = Abs(trueDifference);
                if (lowestRankWithDifference[difference] == 0)
                    lowestRankWithDifference[difference] = rank;
                success = success && difference <= 2;

                if (difference > 0 && zipf.InterpolationCDFRanks.Contains(rank))
                {
                    Console.WriteLine($"Difference = {difference} for interpolation point at rank = {rank}");
                }
                if (trueDifference != prevDifference) detailedLog += $"{rank}. {trueDifference}\n";
                if (difference > 2)
                    countWithDifferenceMoreThanTwo++;

                prevDifference = trueDifference;
            }
            Console.WriteLine(zipf.ToString());
            Console.WriteLine($"Count with Difference > 2: {countWithDifferenceMoreThanTwo}");
            for(var i = 1; i < lowestRankWithDifference.Length; i++)
            {
                if (lowestRankWithDifference[i] > 0)
                Console.WriteLine($"Difference = {i}, Rank = {lowestRankWithDifference[i]}");
            }
            Console.WriteLine(detailedLog);
            Assert.IsTrue(success, $"Interpolation failed for rank {lowestRankWithDifference[3]}.");
            
        }

        /// <summary>
        /// The interpolated CDF should not differ from the the exhaustively computed CDF with a relative error
        /// more than Epsilon.
        /// </summary>
        [Test]
        public void ZipfApproximateCDFIsCloseToCDF()
        {
            var n = 10000;
            var alpha = 0.95;
            var epsilon = 0.01;
            var zipf = new ZipfDistribution(n, alpha, 10, epsilon, ZipfDistribution.InterpolationMethod.Hyperbolic);
            Console.WriteLine(zipf.ToString());
            for (var rank = 1; rank <= n; rank++)
            {
                var expectedCdf = zipf.CDF(rank);
                var actualCdf = zipf.ApproximateCDF(rank);
                Assert.IsFalse(RelativeErrorExceedsTolerance(expectedCdf, actualCdf, epsilon), $"Approximate CDF has unacceptable error for rank {rank} with {zipf.InterpolationSize} control points: Expected {expectedCdf} vs actual {actualCdf}");
            }
        }

        /// <summary>
        /// As Epsilon (the interpolation tolerance) decreases, the number
        /// of interpolated points should increase, up to a maximum of N.
        /// </summary>
        [Test]
        public void ZipfInterpolationSizeIncreasesWithAccuracy()
        {
            var n = 10000;
            var alpha = 0.95;
            var epsilons = new[] { 0.05, 0.04, 0.03, 0.02, 0.01, 0.005, 0.001, 0.0005, 0.0001, 0.00005 };
            var previousCount = 1;
            foreach(var epsilon in epsilons)
            {
                var zipf = new ZipfDistribution(n, alpha, 10, epsilon, ZipfDistribution.InterpolationMethod.Hyperbolic);
                Console.WriteLine(zipf.ToString());
                var currentCount = zipf.InterpolationSize;
                Assert.GreaterOrEqual(currentCount, previousCount, $"Number of interpolation points decreased unexpectedly for epsilon = {epsilon}");
                previousCount = currentCount;
            }
        }

        private static bool RelativeErrorExceedsTolerance(double expected, double actual, double tolerance)
        {
            if (expected == 0)
                return expected != actual;
            var relativeError = Abs(expected - actual) / expected;
            return relativeError > tolerance;
        }
    }
}
