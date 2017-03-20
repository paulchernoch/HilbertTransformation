using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests
{
    [TestFixture]
    class BinaryAttributeTests
    {
    
            /// <summary>
            /// Show that vectors of random zeroes and ones are almost all the same
            /// distance from one another.
            /// </summary>
            [Test]
            public void BA_RandomAttributes()
            {
                var n = 10000;
                var dimensions = 1000;
                var expectedDistance = (int)Math.Round(Math.Sqrt(dimensions / 2));
                var lowDistance = expectedDistance - 4;
                var highDistance = expectedDistance + 4;
                var vectors = Enumerable.Range(0, n).Select(i => RandomVector(dimensions)).ToList();
                var h = DistanceHistogram(vectors);
                Console.WriteLine($"Histogram of Distances for {n} vectors of {dimensions} dimensions");
                Console.WriteLine($"Expected distance = {expectedDistance}");
                for (var i = 0; i < h.Length; i++)
                {
                    Console.WriteLine($"Distance = {i}, Count = {h[i]}");
                }
                foreach (var i in Enumerable.Range(0, h.Length).Where(i => i < lowDistance || i > highDistance))
                {
                    Assert.AreEqual(0, h[i], $"Count for distance {i} is {h[i]}");
                }
            }

            /// <summary>
            /// Show that when most attributes match, the distance is low,
            /// while when one attribute matches, the distance is high.
            /// </summary>
            [Test]
            public void BA_OneMatchVersusOneNotMatch()
            {
                var dimensions = 1000;
                var v1 = RandomVector(dimensions, 2);
                var v2 = RandomVector(dimensions, 2);
                v1[0] = 1;
                v2[0] = 1;
                var v3 = Enumerable.Range(0, dimensions).Select(i => 1).ToArray();
                var v4 = Enumerable.Range(0, dimensions).Select(i => 1).ToArray();
                v3[0] = 0;
                v4[0] = 2;

                var oneMatchDistance = Distance(v1, v2);
                var oneNotMatchDistance = Distance(v3, v4);
                var message = $"Distance where one attribute matches = {oneMatchDistance}, where all but one match = {oneNotMatchDistance}";
                Console.WriteLine(message);
                Assert.IsTrue(oneMatchDistance > oneNotMatchDistance, message);
            }

            [Test]
            public void BA_OneMatchVersusTenMatchesAndTwentyAttributesSet()
            {
                var dimensions = 1000;
                var v1 = RandomVector(dimensions, 2);
                var v2 = RandomVector(dimensions, 2);
                v1[0] = 1;
                v2[0] = 1;
                var v3 = RandomVector(dimensions, 2);
                var v4 = RandomVector(dimensions, 2);
                for (var i = 0; i < 20; i++)
                {
                    v3[i] = 1;
                    v4[i + 10] = 1;
                }
                var oneMatchDistance = Distance(v1, v2);
                var tenOfTwentyMatchDistance = Distance(v3, v4);
                var message = $"Distance where one attribute matches = {oneMatchDistance}, where ten of twenty attributes match = {tenOfTwentyMatchDistance}";
                Console.WriteLine(message);
                Assert.IsTrue(oneMatchDistance > tenOfTwentyMatchDistance, message);
            }


        /// <summary>
        /// The Jaccard similarity measure is viewed as superior to Cartesian distance
        /// when comparing binary attributes, such as the presence or absense of keywords
        /// in a document modeled as a "bag of words". This test will compute the
        /// Kendall Tau-B correlation between two orderings of points, one ordered by
        /// the Cartesian distance, the others by the Jaccard index. 
        /// Attributes will be modeled in a novel way:
        /// 
        ///    0 ..... Attribute missing
        ///    1 ..... Attribute present
        ///    2 ..... Attribute missing
        ///    
        /// If the attribute is missing, it will be randomly assigned zero or two as its value.
        /// In high dimensions, this puts documents missing all attributes at a nearly identical 
        /// distance from all other such points.
        /// </summary>
        [Test]
        public void BA_CompareJaccardToCartesian()
        {
            int dimensions = 100;
            int n = 400;
            var maxPairsToCorrelate = 30000;
            var points = GenerateRandomDocumentChains(dimensions, 25, n);
            var pairs = new List<Tuple<int[], int[]>>();
            for (var i = 0; i < points.Count - 1 && pairs.Count <= maxPairsToCorrelate; i++)
            {
                for (var j = i + 1; j < points.Count && pairs.Count <= maxPairsToCorrelate; j++)
                {
                    pairs.Add(new Tuple<int[], int[]>(points[i], points[j]));
                }
            }
            Func<Tuple<int[], int[]>, int> traditionalCartesianMeasure = pair => UnrandomizedDistance(pair.Item1, pair.Item2);
            Func<Tuple<int[],int[]>,int> randomizedCartesianMeasure = pair => Distance(pair.Item1, pair.Item2);
            Func<Tuple<int[], int[]>, int> jaccardMeasure = pair => (int)(Jaccard(pair.Item1, pair.Item2)*100);
            Func<Tuple<int[], int[]>, int> cosineSimilarityMeasure = pair => (int)(CosineSimilarity(pair.Item1, pair.Item2) * 100);

            foreach (var pair in pairs.Take(30))
            {
                Console.WriteLine($"Cartesian = {randomizedCartesianMeasure(pair)}  Jaccard = {jaccardMeasure(pair)}");
            }
            var control = new KendallTauCorrelation<Tuple<int[], int[]>, int>(traditionalCartesianMeasure, jaccardMeasure);
            var correlator = new KendallTauCorrelation<Tuple<int[], int[]>, int>(randomizedCartesianMeasure, jaccardMeasure);
            var cosineCorrelator = new KendallTauCorrelation<Tuple<int[], int[]>, int>(randomizedCartesianMeasure, cosineSimilarityMeasure);

            var traditionalCorrelation = control.TauB(pairs);
            var correlation = correlator.TauB(pairs);
            var cosineCorrelation = cosineCorrelator.TauB(pairs);
            var oldVersusNewMsg = $"The randomized approach had a correlation of {correlation}, while the traditional measure yielded {traditionalCorrelation}";
            var newVersusCosineMsg = $"The randomized approach had a correlation of {correlation}, while the cosine similarity yielded {cosineCorrelation}";

            Console.WriteLine(oldVersusNewMsg);
            Console.WriteLine(newVersusCosineMsg);

            Assert.GreaterOrEqual(correlation, traditionalCorrelation, oldVersusNewMsg);
            Assert.GreaterOrEqual(correlation, 0.9, $"Cartesian and Jaccard distances only have a correlation of {correlation}");
        }

        /// <summary>
        /// Generate points where some are completely dissimilar, half similar, nearly the same and many 
        /// degrees inbetween.
        /// </summary>
        /// <param name="dimensions">Dimensionality of each point.</param>
        /// <param name="overlap">The first three points will have twice this many attributes set.
        /// One will overlap the other two, having this many attributes in common with each.</param>
        /// <param name="numPoints">Number of points to generate.</param>
        /// <returns>Randopm points.</returns>
        List<int[]> GenerateRandomDocumentChains(int dimensions, int overlap, int numPoints)
        {
            // Start with three points: A, B and C. 
            //    A and B have no attributes in common.
            //    A and C have half their attributes in common.
            //    B and C have half their attributes in common.
            // Make random changes to each point to generate
            // a series of progressively different points.
            // Then compare all points to all other points, sort
            // by Cartesian and Jaccard, and compare the two resulting orderings to see
            // how similar the orderings are.
            var pointA = RandomVector(dimensions);
            var pointB = RandomVector(dimensions);
            var pointC = RandomVector(dimensions);
            for (var i = 0; i < 4 * overlap; i++)
            {
                if (i < 2 * overlap)
                    pointA[i] = 1;
                if (i >= 2 * overlap)
                    pointB[i] = 1;
                if (i >= overlap && i < 3 * overlap)
                    pointC[i] = 1;
            }
            var points = new List<int[]> { pointA, pointB, pointC };
            var offProbability = 0.95;
            var onProbability = 2*(overlap / (double)dimensions);
            var numChangesPerIteration = 5;
            while (points.Count < numPoints)
            {
                var cloneA = (int[]) pointA.Clone();
                var cloneB = (int[]) pointB.Clone();
                var cloneC = (int[]) pointC.Clone();
                for (var iChange = 0; iChange < numChangesPerIteration; iChange++)
                {
                    RandomToggle(cloneA, onProbability, offProbability);
                    RandomToggle(cloneB, onProbability, offProbability);
                    RandomToggle(cloneC, onProbability, offProbability);
                }
                points.Add(cloneA);
                points.Add(cloneB);
                points.Add(cloneC);
                pointA = cloneA;
                pointB = cloneB;
                pointC = cloneC;
            }
            return points;
        }

        static void RandomToggle(int[] point, double probOn, double probOff)
        {
            var r = Rng.NextDouble();
            var iDim = Rng.Next(point.Length);
            if (point[iDim] == 1 && r < probOff)
                point[iDim] = Rng.Next(2) * 2;
            else if (point[iDim] != 1 && r < probOn)
                point[iDim] = 1;
        }

        static Random Rng = new Random();

        static int[] RandomVector(int dimensions, int scale = 1)
        {
            var v = new int[dimensions];
            for (var i = 0; i < dimensions; i++)
            {
                v[i] = (Rng.Next() % 2) * scale;
            }
            return v;
        }

        /// <summary>
        /// Compute the Euclidean (Cartesian) distance between two points.
        /// </summary>
        /// <param name="v1">First point.</param>
        /// <param name="v2">Second point.</param>
        /// <returns>Distance rounded to the nearest integer.</returns>
        static int Distance(int[] v1, int[] v2)
        {
            var distance = 0;
            for (var i = 0; i < v1.Length; i++)
            {
                distance += (v1[i] - v2[i]) * (v1[i] - v2[i]);
            }
            return (int)Math.Round(Math.Sqrt(distance));
        }

        /// <summary>
        /// Reverse the effecdt of the randomization of binary attributes by pretending
        /// values of two (2) are really zero.
        /// </summary>
        /// <param name="v1">First point to compare.</param>
        /// <param name="v2">Sexcond point to compare.</param>
        /// <returns>Cartesian distance, rounded.</returns>
        static int UnrandomizedDistance(int[] v1, int[] v2)
        {
            var distance = 0;
            for (var i = 0; i < v1.Length; i++)
            {
                var x = v1[i] == 2 ? 0 : v1[i];
                var y = v2[i] == 2 ? 0 : v2[i];
                distance += (x - y) * (x - y);
            }
            return (int)Math.Round(Math.Sqrt(distance));
        }


        /// <summary>
        /// Compute the Jaccard distance between two points, a value between zero and one.
        /// This is one minus the Jaccard Similarity.
        /// 
        /// The Jaccard Similarity equals:
        /// 
        ///      J(A,B) = |A ∩ B| / |A ∪ B|
        ///      
        /// The Jaccard Distance equals:
        /// 
        ///      JD(A,B) = 1 - J(A,B)
        ///      
        /// Thus we count up the number of distinct attributes set among the two vectors as the denominator,
        /// and the number of attributes in common as the numerator, and take their ratio.
        /// </summary>
        /// <param name="v1">First point.</param>
        /// <param name="v2">Second point.</param>
        /// <returns>Distance, a number between zero nd one.</returns>
        static double Jaccard(int[] v1, int[] v2)
        {
            var union = 0;
            var intersection = 0;
            for (var i = 0; i < v1.Length; i++)
            {
                if (v1[i] == 1 || v2[i] == 1)
                    union++;
                if (v1[i] * v2[i] == 1)
                    intersection++;
            }
            if (union == 0)
                return 1.0;
            return 1.0 - (intersection / (double)union);
        }

        /// <summary>
        /// Compute the Cosine Similarity between two vectors.
        /// This is the opposite of a distance, as distance increases, similarityh decreases.
        /// 
        /// This compensates for randomized boolean attributes by interpreting twos as zeroes.
        /// </summary>
        /// <param name="v1">First vector.</param>
        /// <param name="v2">Second vector.</param>
        /// <returns>Cosine of the angle between the vectors.
        /// This varies from -1 (vectors point in opposite directions) to 1 (they point in the same direction).</returns>
        static double CosineSimilarity(int[] v1, int[] v2)
        {
            var v1Mag = Math.Sqrt(v1.Select(i => i == 2 ? 0 : i).Sum());
            var v2Mag = Math.Sqrt(v2.Select(i => i == 2 ? 0 : i).Sum());
            var dotProduct = 0.0;
            for (var iDim = 0; iDim < v1.Length; iDim++)
            {
                var x = v1[iDim] == 2 ? 0 : v1[iDim];
                var y = v2[iDim] == 2 ? 0 : v2[iDim];
                dotProduct += x * y;
            }
            if (v1Mag * v2Mag == 0)
                return 0;
            return dotProduct / (v1Mag * v2Mag);
        }

        static int[] DistanceHistogram(List<int[]> vectors)
            {
                var histogram = new int[(int)Math.Sqrt(vectors[0].Length) + 1];
                var n = vectors.Count;
                foreach (var i in Enumerable.Range(0, n - 1))
                {
                    for (var j = i + 1; j < n; j++)
                    {
                        histogram[Distance(vectors[i], vectors[j])]++;
                    }
                }
                return histogram;
            }
        }

    
}
