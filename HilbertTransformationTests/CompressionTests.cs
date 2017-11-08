using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using HilbertTransformation;
using static System.Math;
using static System.Double;
using NUnit.Framework;
using System.Diagnostics;

namespace HilbertTransformationTests
{
    /// <summary>
    /// Assess how well the Hilbert curve can be used to compress a function in D-dimensions.
    /// 
    ///  1) Assume all dimensions are independent.
    ///  2) Generate random polynomials for each dimension.
    ///  3) For each test, vary the maximum degree N of the polynomial.
    ///  4) Assume a grid of 16 choices (B = 4-bits) for each dimension.
    ///  5) Delineate the regions where the function is positive (safe) versus negative (unsafe).
    ///  6) Perform run length encoding using a Hilbert curve, generating segments.
    ///  7) Count how many segments in Hilbert space are necessary to capture the shape of the boundary between positive and negative.
    ///  8) Compute the median number across many trials of how many segments S are required versus the degree of polymomial and number of dimensions.
    ///  9) Estimate memory requirements: 
    ///       Memory =((B * D / 8) + C) * S
    ///     where C is a constant related to the C# representation of arrays and pointer size
    /// </summary>
    [TestFixture]
    public class CompressionTests
    {
        public class Stats
        {
            public int Low;
            public int Median;
            public int High;
        }
        private static Stats MedianCompression(int dimensions, int polynomialDegree, int bitsPerDimension, int trials, FastRandom r)
        {
            var polynomials = new RandomPolynomial[dimensions];
            for(var iDim = 0; iDim < dimensions; iDim++)
                polynomials[iDim] = new RandomPolynomial(polynomialDegree, 1000000, r);
            var segmentsRequired = new List<int>();

            for (var iTrial = 0; iTrial < trials; iTrial++)
            {
                int segments = 0;
                var cells = (long) Pow(1 << bitsPerDimension, dimensions);
                double previousValue = NaN;
                if (dimensions > 1)
                {
                    for (var hilbertIndex = BigInteger.Zero; hilbertIndex < cells; hilbertIndex++)
                    {
                        uint[] coordinates = hilbertIndex.HilbertAxes(bitsPerDimension, dimensions);
                        var f = coordinates.Select((x, iDim) => polynomials[iDim].Evaluate(x)).Sum();
                        if (!IsNaN(previousValue) && (f > 0 != previousValue > 0))
                            segments++;
                        previousValue = f;
                    }
                }
                else
                {
                    // In one dimension, no need for Hilbert curve
                    for (var index = 0; index < cells; index++)
                    {
                        var f = polynomials[0].Evaluate(index);
                        if (!IsNaN(previousValue) && (f > 0 != previousValue > 0))
                            segments++;
                        previousValue = f;
                    }
                }
                segmentsRequired.Add(segments);
            }
            segmentsRequired.Sort();
            var medianSegmentsRequired = segmentsRequired[segmentsRequired.Count / 2];
            return new Stats { Low = segmentsRequired.First(), Median = medianSegmentsRequired, High = segmentsRequired.Last() };
        }

        public class RandomPolynomial
        {
            double[] Coefficients;
            int Degree { get => Coefficients.Length - 1; }
            public RandomPolynomial(int degree, double maxCoefficientMagnitude, FastRandom r)
            {
                Coefficients = new double[degree + 1];
                for (var i = 0; i <= degree; i++)
                    Coefficients[i] = (r.NextDouble() - 0.5) * maxCoefficientMagnitude * 2; // Permit range [-maxCoefficientMagnitude, +maxCoefficientMagnitude)
            }

            public double Evaluate(double x)
            {
                var f = 0.0;
                var x_n = 1.0;
                for(var degree = 0; degree <= Degree; degree++)
                {
                    f += x_n * Coefficients[degree];
                    x_n *= x;
                }
                return f;
            }
        }

        /// <summary>
        /// This test creates debug output, so it is really an experiment, not a test.
        /// </summary>
        [Test]
        public void CompressionTrials()
        {
            var bits = 4;
            var r = new FastRandom();
            var numTrials = 5000;
            Debug.WriteLine($"Dimensions,Polynomial Degree,Low Segments,Median Segments,High Segments,Poorest Compression,Median Compression,Best Compression");
            for (var iDim = 1; iDim <= 4; iDim++)
            {
                var spaceSize = Pow(1 << bits, iDim);
                for (var iDegree = 1; iDegree <= 7; iDegree++)
                {
                    var stats = MedianCompression(iDim, iDegree, bits, numTrials, r);
                    var poorestCompression = stats.High / spaceSize;
                    var medianCompression = stats.Median / spaceSize;
                    var bestCompression = stats.Low / spaceSize;
                    Debug.WriteLine($"{iDim},{iDegree},{stats.Low},{stats.Median},{stats.High},{poorestCompression},{medianCompression},{bestCompression}");
                }
            }
            Debug.WriteLine("Done");
        }



    }

    
}
