using HilbertTransformation;
using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace Clustering
{
    /// <summary>
    /// Measures or estimates the coarseness of a grid in relation to a set of points.
    /// Coarseness varies from zero, when all points fall in separate grid cells,
    /// to one, when all points are crammed into the same grid cell.
    /// </summary>
    /// <remarks>
    /// With N points there are N(N-1)/2 distinct pairs of points that can be formed.
    /// The exact coarseness measurement studies all these combinations and counts,
    /// for a given size of grid, how many pairs of points will fall in the same grid cell
    /// or in different grid cells.
    /// 
    /// Each cell is a hypercube in D-dimensions. A grid has a side whose length is always a power of two,
    /// essential if we are to use this analysis to determine how many bits we need per dimension
    /// when performing the Hilbert transformation. The fewer bits needed to split all points
    /// into separate cells, the less memory is consumed and less execution time expended.
    /// 
    /// The measure of coarseness is normalized:
    /// 
    ///    Coarseness = Count(Pairs in same cell) / Count(All pairs)
    ///    
    /// If half of the points are in one cell and half in another:
    /// 
    ///   C = N(N/2 - 1)(1/2)/(N(N-1)/2) = (N/2 - 1)/(N-1) ~ 1/2
    ///   
    /// If points are evenly divided in K clusters:
    /// 
    ///   C ~ 1/K
    /// 
    /// If one point is by itself and all other points are in one cluster:
    /// 
    ///   C = (N-1)(N-2)(1/2) / (N(N-1)/2) = (N-2)/N = 1 - 2/N
    ///   
    /// If all points are in their own cells:
    ///   
    ///   C = 0
    ///   
    /// Thus, if we have 20000 points and our goal is to split it into clusters
    /// with at most 2000 points each, we need to find the number of bits that translates
    /// to a cell size that yields a C value of 1/10th or less.
    /// </remarks>
    public class GridCoarseness
    {
        /// <summary>
        /// Number of bits required to represent the largest value among all coordinates and all points.
        /// </summary>
        public int BitsPerDimension { get; private set; }

        /// <summary>
        /// Points to divide into grid cells.
        /// </summary>
        IList<UnsignedPoint> Points { get; set; }

        /// <summary>
        /// Number of dimensions possessed by each point.
        /// </summary>
        int Dimensions {  get { return Points[0].Dimensions; } }

        /// <summary>
        /// Number of points.
        /// </summary>
        public int Count {  get { return Points.Count;  } }

        /// <summary>
        /// Once Coarseness is called a first time, this stores the results which will be reused unless Clear() is called. 
        /// </summary>
        double[] CoarsenessPerBits { get; set; } = null;

        /// <summary>
        /// Construct a GridCoarseness measurer.
        /// </summary>
        /// <param name="points">Points to grid.</param>
        /// <param name="bitsPerDimension">Number of bits necessary to represent the largest coordinate value.</param>
        public GridCoarseness(IList<UnsignedPoint> points, int bitsPerDimension)
        {
            BitsPerDimension = bitsPerDimension;
            Points = points;
        }

        /// <summary>
        /// Compute the coarseness, either estimated or exact, depending on pairsToTest.
        /// 
        /// If Coarseness is called a second time, the previous results are returned unchanged, unless Clear() is called inbetween.
        /// </summary>
        /// <param name="pairsToTest">Number of random pairs of points to test.
        /// If zero or greater than N(N-1)/2, test all pairs of points.
        /// If there are thirty-two points or fewer, then compute an exact result as well.</param>
        /// <returns>Coarseness values per number of bits of division.
        /// The value at index zero is one, the case when a single grid cell is big enough to hold all data.
        /// At index one is the coarseness if the grid cell size is half of 2^BitsPerDimension.
        /// At index two is the coarseness if the grid cell size is a quarter of 2^BitsPerDimension.
        /// etc.
        /// </returns>
        public double[] Coarseness(int pairsToTest = 0)
        {
            if (CoarsenessPerBits != null)
                return CoarsenessPerBits;
            var sameCellPerBits = new int[BitsPerDimension];
            var allPermutations = Count * (Count - 1) / 2;
            if (pairsToTest <= 0 || pairsToTest >= allPermutations || Count <= 32)
            {
                pairsToTest = allPermutations;
                // Exact computation, visiting all permutations.
                for (var i = 0; i < Count - 1; i++)
                {
                    var point1 = Points[i];
                    for (var j = i + 1; j < Count; j++)
                    {
                        var point2 = Points[j];
                        LoopOverBits(point1, point2, sameCellPerBits);
                    }
                }
            }
            else
            {
                // Estimate, visiting a sample of permutations.
                var alreadyVisited = new HashSet<long>();
                var rng = new FastRandom();
                while (alreadyVisited.Count < pairsToTest)
                {
                    var i = rng.Next(Count);
                    var j = rng.Next(Count);
                    if (i == j) continue;
                    var key = ((long)i << 32) + j;
                    // Do not visit the same permutation more than once.
                    // HashSet.Add returns false if the key is already present, 
                    // so we do not need to call both Contains and Add.
                    if (!alreadyVisited.Add(key))
                        continue;
                    var point1 = Points[i];
                    var point2 = Points[j];
                    LoopOverBits(point1, point2, sameCellPerBits);
                }
            }
            sameCellPerBits[0] = pairsToTest; // Ensure that CoarsenessPerBits[0] = 1.
            CoarsenessPerBits = sameCellPerBits.Select(count => (double)count / (double)pairsToTest).ToArray();
            return CoarsenessPerBits;
        }

        /// <summary>
        /// Find the minimum number of bits that must be used to represent points such that no grid cell is likely to receive more than the given
        /// number of points. 
        /// 
        /// If Coarseness has alread been called, this reuses the results from that call, otherwise it performs the calculations.
        /// </summary>
        /// <param name="targetSize">No grid cell should have more than this many points.</param>
        /// <param name="pairsToTest">Number of random pairs of points to test when computing the coarseness.
        /// If zero or greater than N(N-1)/2, test all pairs of points.
        /// If there are thirty-two points or fewer, then compute an exact result as well.</param>
        /// <returns>Minimum number of bits that must be used to represent points such that no grid cell is likely to receive more than the given
        /// number of points.</returns>
        public int BitsToDivide(int targetSize, int pairsToTest = 0)
        {
            Coarseness(pairsToTest);
            var targetCoarseness = (double)targetSize / Count;
            return CoarsenessPerBits
                .Select((c,i) => new { Coarse = c, Bits = i })
                .Where(pair => pair.Coarse <= targetCoarseness || pair.Bits == this.BitsPerDimension)
                .FirstOrDefault()
                .Bits;
        }

        /// <summary>
        /// Clear the results of a previous call to Coarseness so that subsequent calls must recompute the coarseness.
        /// </summary>
        public void Clear()
        {
            CoarsenessPerBits = null;
        }

        /// <summary>
        /// For a pair of points, loop over many grid sizes and determine whether the points will be in the same or different cells at that size.
        /// </summary>
        /// <param name="point1">First point to test.</param>
        /// <param name="point2">Second point to test.</param>
        /// <param name="sameCellPerBits">Accumulate the results here, adding to what has been recorded for other pairs of points.</param>
        private void LoopOverBits(UnsignedPoint point1, UnsignedPoint point2, int[] sameCellPerBits)
        {
            for (var bits = 1; bits < BitsPerDimension; bits++)
            {
                var cellSize = 1 << (BitsPerDimension - bits);
                if (InSameCell(point1, point2, cellSize))
                    sameCellPerBits[bits]++;
                else // All subsequent values of bits will necessarily split points apart.
                    break;
            }
        }

        /// <summary>
        /// If D-dimensional space were divided into a grid of hypercubes whose side equals cellSize, rooted at the origin,
        /// would the two given points be in the same grid cell or different cells?
        /// 
        /// For example, if the cellSize is 32, the first cell has coordinates from zero to 31, the next from 32 to 63, etc.
        /// Thus if x = 37 and y = 70, the grid line at 64 falls between them, hence they are NOT in the same cell.
        /// </summary>
        /// <param name="p1">First point to compare.</param>
        /// <param name="p2">Second point to compare.</param>
        /// <param name="cellSize">Edge length of grid cell.</param>
        /// <returns>True if the cells will fall in the same cell, false otherwise.</returns>
        private bool InSameCell(UnsignedPoint p1, UnsignedPoint p2, int cellSize)
        {
            // It only takes one divisive dimension to split points apart, even if all the other coordinates match.
            // We are looking for min < grid <= max where grid = i * cellSize for an integer multiplier i.
            // If that is true, then a grid line falls between p1 and p2 for that dimension. 
            // If the grid line falls on the point with the larger coordinate value, that is okay too.
            for(var iDim = 0; iDim < Dimensions; iDim++)
            {
                var x = p1[iDim];
                var y = p2[iDim];
                if (x == y) continue;
                var max = Max(x, y);
                var min = Min(x, y);
                var i = (max / cellSize);
                // If the cellSize is larger than the maximum of the coordinates, both are in the same first cell.
                if (i == 0) continue;
                var grid = i * cellSize;
                // With i > 0, we already know that grid <= max, now see if min < grid
                if (grid > min) return false;
            }
            return true;
        }
    }
}
