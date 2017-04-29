using HilbertTransformation;
using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Clustering
{
    /// <summary>
    /// Sort points in Hilbert curve order.
    /// 
    /// This is lighter weight than the HilbertIndex class, using less memory.
    /// </summary>
    public class HilbertSort
    {
        public static double RelativeSortCost { get; set; }

        /// <summary>
        /// Sort in Hilbert index order without balancing the coordinates, faster but using much more memory than SmallBalancedSort.
        /// This will sort points in a different order than if balanaced.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="bitsPerDimension">Bits to use per dimension.</param>
        /// <param name="perm">Optional permutation to apply to the coordinates.</param>
        /// <returns>The points, sorted in Hilbert order.</returns>
        public static List<UnsignedPoint> Sort(IReadOnlyList<UnsignedPoint> points, int bitsPerDimension, Permutation<uint> perm = null) {
            if (perm == null)
            {
                return points.OrderBy(
                    point => point.Coordinates.HilbertIndex(bitsPerDimension)
                ).ToList();
            }
            else
            {
                return points.OrderBy(
                    point => perm.ApplyToArray(point.Coordinates).HilbertIndex(bitsPerDimension)
                ).ToList();
            }
        }

        /// <summary>
        /// Sort points according to the Hilbert curve order, using the full precision of all coordinate values.
        /// The Hilbert positions by which the points are sorted are not built directly upon the unmodified points,
        /// but instead upon balanced versions where coordinates are shifted so that the means align with the 
        /// center of the coordinate range as far as possible.
        /// 
        /// NOTE: This temporarily maintains the Hilbert position of all points during the sort operation, which for large
        /// datasets may exhaust memory. Use this speedier method for small sets, but the alternate Sort which accepts arrays for large sets.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="balancer">If supplied, this point balancer is reused.
        /// Otherwise, a new one is built and returned.
        /// Reuse is appropriate if sorting of subsets of data is performed but you want all sorting to be conducted on a uniform basis, 
        /// which using the same balancer ensures.</param>
        /// <returns>Sorted list of points.
        /// Points are not sorted in-place.</returns>
        public static List<UnsignedPoint> BalancedSort(IReadOnlyList<UnsignedPoint> points, ref PointBalancer balancer)
        {
            var pointBalancer = balancer ?? new PointBalancer(points);
            return points.OrderBy(
                point => pointBalancer.ToHilbertPosition(point, pointBalancer.BitsPerDimension)
            ).ToList();
        }

        /// <summary>
        /// Sort points according to the Hilbert curve order, potentially using a lower precision for all coordinate values.
        /// If points are sorted with lower precision, the ordering is consistent with sorts conducted at higher precision.
        /// This means that if two points receive DIFFERING Hilbert positions, they will appear in the sorted list in the 
        /// proper relative order. However if two points receive the SAME Hilbert positions at the lower precision, they likely
        /// would have different positions at the full precision. Such points may appear out of order, but will be grouped together
        /// by that position.
        /// 
        /// Sorting using fewer bits of precision is faster and uses less memory. If data is nearly random, it may sort points entirely 
        /// in the correct order, or close to it, permitting subsequent passes to re-sort the groups of points with like positions
        /// in separate batches.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="balancer">If supplied, this point balancer is reused.
        /// Otherwise, a new one is built and returned.
        /// Reuse is appropriate if sorting of subsets of data is performed but you want all sorting to be conducted on a uniform basis, 
        /// which using the same balancer ensures.</param>
        /// <returns>Sorted list of points.
        /// Points are not sorted in-place.</returns>
        public static List<UnsignedPoint> BalancedSort(IReadOnlyList<UnsignedPoint> points, int bitsPerDimension, ref PointBalancer balancer)
        {
            var pointBalancer = balancer ?? new PointBalancer(points);
            return points.OrderBy(
                point => pointBalancer.ToHilbertPosition(point, bitsPerDimension)
            ).ToList();
        }

        /// <summary>
        /// Sort the points (not in-place) and return a List of Arrays, where all points in a given array share the same
        /// value for the sort key (the Hilbert position) and the arrays are sorted by the Hilbert position.
        /// 
        /// All points are sorted in a single pass using the specified precision of each number in constructing the Hilbert curve.
        /// Consequently, if the full precision is used, this uses more memory than the in-place, recursive sort, 
        /// because all the Hilbert positions are computed and held in memory simultaneously.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="bitsPerDimension">Number of bits to use per dimension in constructing the Hilbert position.
        /// If less than the number required for full precision, this will reduce the precision used in sorting.</param>
        /// <param name="balancer">If passed in, this balancer is reused, otherwise one is created and returned.</param>
        /// <returns>A List of arrays of points where each array holds points that share the same value of Hilbert position
        /// at the given precision. The arrays themselves are sorted by Hilbert position.</returns>
        public static List<UnsignedPoint[]> SortWithTies(IReadOnlyList<UnsignedPoint> points, int bitsPerDimension, ref PointBalancer balancer)
        {
            var pointBalancer = balancer ?? new PointBalancer(points);
            var pointsWithTies = points
                .GroupBy(point => pointBalancer.ToHilbertPosition(point, bitsPerDimension))
                .OrderBy(g => g.Key)
                .Select(g => g.ToArray())
                .ToList();
            return pointsWithTies;
        }

        /// <summary>
        /// Sorts points in-place according to the Hilbert curve, applying the transform to balance points, using less memory.
        /// </summary>
        /// <param name="points">Points to sort in-place.</param>
        /// <param name="balancer">If supplied, this point balancer is reused.
        /// Otherwise, a new one is built and returned.
        /// Reuse is appropriate if sorting of subsets of data is performed but you want all sorting to be conducted on a uniform basis, 
        /// which using the same balancer ensures.</param>
        /// <returns>The same points array passed in as argument, with values sorted.</returns>
        public static UnsignedPoint[] SmallBalancedSort(UnsignedPoint[] points, ref PointBalancer balancer, Permutation<uint> perm = null)
        {
            balancer = balancer ?? new PointBalancer(points);
            var hilbertPositions = new BigInteger[points.Length];
            var allPoints = new ArraySegment<UnsignedPoint>(points, 0, points.Length);
            var allHilbertPositions = new ArraySegment<BigInteger>(hilbertPositions, 0, hilbertPositions.Length);
            
            var cost = SortSegment(allPoints, allHilbertPositions, balancer, 1, false, perm);
            RelativeSortCost = cost / (double)(points.Length * balancer.BitsPerDimension);
            return points;
        }

        /// <summary>
        /// Recursively sort the array in place into smaller and smaller segments, until each segment is one item long.
        /// 
        /// Each segment is distinguished by sharing the same value for the sort key (the Hilbert position).
        /// At each recursion step, we add to the number of bits in the Hilbert transform until we run out of sorting or reach BitsPerDimension,
        /// the maximum. By adding bits, the Hilbert sort key becomes more specific, thus shortening the segments sharing the same key.
        /// </summary>
        /// <param name="points">Points to sort.</param>
        /// <param name="hilbertPositions">Will hold the Hilbert sort keys, which will change at each level of recursion.</param>
        /// <param name="balancer">Used to shift the coordinates of each dimension so that the median falls in the middle of the range.</param>
        /// <param name="bits">Number of bits to use per coordinate when computing the Hilbert position.
        /// Each recursive level increses the number of bits.</param>
        /// <param name="perm">Optional permutation.</param>
        /// <returns>The recursive cost of the operation, which is governed by how many Hilbert transformation were required,
        /// times the number of bits per transform. 
        /// If we sort N points with B bits in the straightforward way (not preserving memory), the cost would be N*B.
        /// If the cost comes in below that, we have improved on the simple, non-recursive quicksort.</returns>
        private static int SortSegment(ArraySegment<UnsignedPoint> points, ArraySegment<BigInteger> hilbertPositions, PointBalancer balancer, int bits, bool executeParallel, Permutation<uint> perm = null)
        {
            var cost = 0;
            Action<int> costUpdater = (int costIncrement) => {
                Interlocked.Add(ref cost, costIncrement);
            };
            var pointsList = (IList<UnsignedPoint>) points;
            var hpList = (IList<BigInteger>) hilbertPositions;
            // Prepare the sort keys - the Hilbert positions.
            if (executeParallel)
            {
                Parallel.For(0, pointsList.Count, i => { hpList[i] = balancer.ToHilbertPosition(pointsList[i], bits, perm); } );
            }
            else
            {
                for (var i = 0; i < pointsList.Count; i++)
                    hpList[i] = balancer.ToHilbertPosition(pointsList[i], bits, perm);
            }

            Array.Sort(hilbertPositions.Array, points.Array, points.Offset, points.Count);
            cost += points.Count * bits;

            // If we are already at the highest number of bits, even if two points have the same
            // Hilbert position, we can sort them no further.
            if (bits >= balancer.BitsPerDimension)
                return cost;

            var actions = new List<Action>();

            var iStart = 0;
            BigInteger? prevPosition = hpList[0];
            for (var i = 1; i <= pointsList.Count; i++)
            {
                BigInteger? currentPosition = null;
                if (i < pointsList.Count) currentPosition = hpList[i];
                if (!prevPosition.Equals(currentPosition))
                {
                    var segmentLength = i - iStart;
                    if (segmentLength > 1)
                    {
                        Action taskAction = SortRecursion(points, hilbertPositions, balancer, bits, iStart, segmentLength, perm, costUpdater);
                        if (executeParallel)
                            actions.Add(taskAction);       
                        else
                            taskAction.Invoke();           
                    }
                    iStart = i;
                }
                prevPosition = currentPosition;
            }
            if (executeParallel)
            {
                foreach (var a in actions)
                    a.Invoke();
                //TODO: Don't know why parallel execution fails to produce correct results. Some shared state is likely being altered.

                //var tasks = actions.Select(a => new Task(a)).ToList();
                //foreach (var t in tasks)
                //    t.Start();
                //Task.WhenAll(tasks).Wait();
            }
            return cost;
        }

        private static Action SortRecursion(ArraySegment<UnsignedPoint> points, ArraySegment<BigInteger> hilbertPositions, PointBalancer balancer, int bits, int iStart, int segmentLength, Permutation<uint> perm, Action<int> costUpdater)
        {
            return () =>
            {
                var smallerSegment = new ArraySegment<UnsignedPoint>(points.Array, points.Offset + iStart, segmentLength);
                var smallerHilbertKeys = new ArraySegment<BigInteger>(hilbertPositions.Array, hilbertPositions.Offset + iStart, segmentLength);
                // The bucket has more than one point, so we need to sort it recursively.
                var grid = new GridCoarseness((IList<UnsignedPoint>)smallerSegment, balancer.BitsPerDimension);
                var targetCount = segmentLength < 50 ? 0 : segmentLength / 10;
                var bitsToRecurse = grid.BitsToDivide(targetCount, segmentLength * 2);
                // The grid sometimes yields the same number of bits twice in a row due to estimation , 
                // so force it to at least increase by one. 
                if (bitsToRecurse <= bits)
                    bitsToRecurse = bits + 1;
                var parallelRecursion = false; // Normally false, but testing to see the effect.
                var costIncrement = SortSegment(smallerSegment, smallerHilbertKeys, balancer, bitsToRecurse, parallelRecursion, perm);
                costUpdater(costIncrement);
            };
        }

        public static int FindBitsPerDimension(IReadOnlyList<UnsignedPoint> points)
        {
            return HilbertPoint.FindBitsPerDimension((int)points.Select(p => p.MaxCoordinate).Max());
        }
    }
}
