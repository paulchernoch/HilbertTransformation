using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace HilbertTransformation.Cache
{
    /// <summary>
    /// Shellsort implementation using Marcin Ciura's gap sequence: [1, 4, 10, 23, 57, 132, 301, 701, 1750].
    /// 
    /// See Ciura, Marcin (2001). "Best Increments for the Average Case of Shellsort". 
    /// http://sun.aei.polsl.pl/~mciura/publikacje/shellsort.pdf
    /// 
    /// For N greater than 4000, we multiply the previous sequence number by 2.25 and take the ceiling, as suggested by Tokuda and recommended on Wikipedia.
    /// </summary>
    public static class Sorting
    {
        #region Shell Sort

        /// <summary>
        /// Optimized set of increments derived empirically by Marcin Ciura.
        /// 
        /// See https://oeis.org/A102549 (The On-line Encyclopedia of Integer Sequences).
        /// </summary>
        static int[] CiuraGaps = new[] { 1, 4, 10, 23, 57, 132, 301, 701, 1750 };

        /// <summary>
        /// Return an ascending series of increments suitable for the number of elements to be sorted.
        /// 
        /// The increments will be used in reverse (descending) order.
        /// </summary>
        /// <param name="listSize">THe number of elements to be sorted.</param>
        /// <returns>Ascending sequence of integers suitable as gap increments for the shellsort.</returns>
        static IEnumerable<int> GapsAscending(int listSize)
        {
            foreach (var gap in CiuraGaps)
            {
                if (gap > listSize)
                    yield break;
                yield return gap;
            }
            // Extend Ciura's increments using a geometric factor of 2.25.
            // Such gaps have not been optimized empirically, but researchers observe that a geometric
            // growth factor of 2.2 or 2.25 yields good sequences.
            double g = CiuraGaps.Last() * 2.25;
            while (g < listSize)
            {
                yield return (int)Ceiling(g);
                g *= 2.25;
            }
        }

        /// <summary>
        /// Sort an Array in place using a ShellSort.
        /// 
        /// ShellSort is not a stable sort.
        /// </summary>
        /// <typeparam name="TElem">Type of element to sort.</typeparam>
        /// <param name="a">Array to sort in place.</param>
        /// <returns>The same array, sorted.</returns>
        public static TElem[] ShellSort<TElem>(this TElem[] a) where TElem: IComparable<TElem> {
            var n = a.Count();
            // Start with the largest gap and work down to a gap of 1.
            // The final pass with a gap of one is equivalent to an insertion sort.
            foreach (var gap in GapsAscending(n).Reverse())
            {
                // Do a gapped insertion sort for this gap size.
                // The first gap elements a[0..gap-1] are already in gapped order
                // keep adding one more element until the entire array is gap sorted
                for (var i = gap; i < n; i += 1)
                {
                    // add a[i] to the elements that have been gap sorted
                    // save a[i] in temp and make a hole at position i
                    var temp = a[i];
                    // shift earlier gap-sorted elements up until the correct location for a[i] is found
                    int j = i;
                    for (; j >= gap && a[j - gap].CompareTo(temp) > 0; j -= gap)
                    {
                        a[j] = a[j - gap];
                    }
                    // put temp (the original a[i]) in its correct location
                    a[j] = temp;
                }
            }
            return a;
        }

        /// <summary>
        /// Sort a List in place using a ShellSort using the default ordering of IComparable objects.
        /// 
        /// ShellSort is not a stable sort.
        /// </summary>
        /// <typeparam name="TElem">Type of element to sort.</typeparam>
        /// <param name="a">List to sort in place.</param>
        /// <returns>The same List, sorted.</returns>
        public static IList<TElem> ShellSort<TElem>(this IList<TElem> a) where TElem : IComparable<TElem>
        {
            var n = a.Count;
            // Start with the largest gap and work down to a gap of 1.
            // The final pass with a gap of one is equivalent to an insertion sort.
            foreach (var gap in GapsAscending(n).Reverse())
            {
                // Do a gapped insertion sort for this gap size.
                // The first gap elements a[0..gap-1] are already in gapped order
                // keep adding one more element until the entire array is gap sorted
                for (var i = gap; i < n; i += 1)
                {
                    // add a[i] to the elements that have been gap sorted
                    // save a[i] in temp and make a hole at position i
                    var temp = a[i];
                    // shift earlier gap-sorted elements up until the correct location for a[i] is found
                    int j = i;
                    for (; j >= gap && a[j - gap].CompareTo(temp) > 0; j -= gap)
                    {
                        a[j] = a[j - gap];
                    }
                    // put temp (the original a[i]) in its correct location
                    a[j] = temp;
                }
            }
            return a;
        }

        /// <summary>
        /// Sort a List in place with a ShellSort using the supplied comparator.
        /// 
        /// ShellSort is not a stable sort.
        /// </summary>
        /// <typeparam name="TElem">Type of element to sort.</typeparam>
        /// <param name="a">List to sort in place.</param>
        /// <returns>The same List, sorted.</returns>
        public static IList<TElem> ShellSort<TElem>(this IList<TElem> a, IComparer<TElem> comparer) where TElem : IComparable<TElem>
        {
            var n = a.Count;
            // Start with the largest gap and work down to a gap of 1.
            // The final pass with a gap of one is equivalent to an insertion sort.
            foreach (var gap in GapsAscending(n).Reverse())
            {
                // Do a gapped insertion sort for this gap size.
                // The first gap elements a[0..gap-1] are already in gapped order
                // keep adding one more element until the entire array is gap sorted
                for (var i = gap; i < n; i += 1)
                {
                    // add a[i] to the elements that have been gap sorted
                    // save a[i] in temp and make a hole at position i
                    var temp = a[i];
                    // shift earlier gap-sorted elements up until the correct location for a[i] is found
                    int j = i;
                    for (; j >= gap && comparer.Compare(a[j - gap], temp) > 0; j -= gap)
                    {
                        a[j] = a[j - gap];
                    }
                    // put temp (the original a[i]) in its correct location
                    a[j] = temp;
                }
            }
            return a;
        }

        #endregion

        #region Insertion Sort

        /// <summary>
        /// Sort an Array in place using an Insertion Sort.
        /// </summary>
        /// <typeparam name="TElem">Type of element to sort.</typeparam>
        /// <param name="a">Array to sort in place.</param>
        /// <returns>The same array, sorted.</returns>
        public static TElem[] InsertionSort<TElem>(this TElem[] a) where TElem : IComparable<TElem>
        {
            var n = a.Count();

            // Do a gapped insertion sort for a gap size of one.
            // The first gap elements a[0..gap-1] are already in gapped order
            // keep adding one more element until the entire array is gap sorted
            for (var i = 1; i < n; i += 1)
            {
                // add a[i] to the elements that have been gap sorted
                // save a[i] in temp and make a hole at position i
                var temp = a[i];
                // shift earlier gap-sorted elements up until the correct location for a[i] is found
                int j = i-1;
                for (; j >= 0 && a[j].CompareTo(temp) > 0; j--)
                {
                    a[j+1] = a[j];
                }
                // put temp (the original a[i]) in its correct location
                a[j+1] = temp;
            }
            return a;
        }

        #endregion

        /// <summary>
        /// Sort a list in place in a manner slow for the average case but fast for a list where only the item 
        /// belonging at the start or end of the list is out of place.
        /// </summary>
        /// <typeparam name="TElem">Type of element to sort, which must implement IComparable.</typeparam>
        /// <param name="a">List to sort in place.</param>
        /// <param name="iterations">Number of iterations to perform.
        /// If zero or greater than Count/2, a full sort of Count/2 iterations is performed.
        /// Otherwise, a partial sort is performed, after which the bottom Count/2 and the top Count/2 elements in the list will be sorted,
        /// and the remaining elements will have values that fall between these bottom and top elements.
        /// 
        /// If iterations is one, the minimum value will end up at the start of the list and the maximum value will end up at the end.
        /// </param>
        /// <returns>A sorted or partially sorted list.</returns>
        public static IList<TElem> HighLowSort<TElem>(this IList<TElem> a, int iterations = 0) where TElem : IComparable<TElem>
        {
            var n = a.Count;
            TElem temp;
            if (iterations <= 0)
                iterations = n / 2;
            else
                iterations = Min(n / 2, iterations);
            for (int bottom = 0, top = n - 1; bottom < iterations; bottom++, top--)
            {
                // Compare the item at the beginning of the list to the item at the end of the list and swap if out of order
                if (a[bottom].CompareTo(a[top]) > 0)
                {
                    temp = a[bottom];
                    a[bottom] = a[top];
                    a[top] = temp;
                }
                // Compare middle elements to both beginning and end of list
                for (var i = bottom + 1; i < top; i++)
                {
                    if (a[bottom].CompareTo(a[i]) > 0)
                    {
                        temp = a[bottom];
                        a[bottom] = a[i];
                        a[i] = temp;
                    }
                    else if (a[top].CompareTo(a[i]) < 0)
                    {
                        temp = a[top];
                        a[top] = a[i];
                        a[i] = temp;
                    }
                }
            }
            return a;
        }

        /// <summary>
        /// Ensure that the lowest value in the specified portion of the list is moved to the beginning 
        /// of the specified range and the largest is moved to the end. Leave all other items mostly as they 
        /// are, unsorted.
        /// </summary>
        /// <typeparam name="TElem">Type of element in the collection.</typeparam>
        /// <param name="a">List to alter in-place.</param>
        /// <param name="startIndex">Index of first element in the range to manipulate.</param>
        /// <param name="count">Number of consecutive items to scan for low and high values.</param>
        /// <returns>The same list, with at most four items moved to new locations.
        /// </returns>
        public static IList<TElem> LowHigh<TElem>(this IList<TElem> a, int startIndex = 0, int count = -1) where TElem : IComparable<TElem>
        {
            var arr = a as TElem[];
            if (arr != null)
            {
                // If "a" is really an array, call the other LowHigh, because operating on arrays is faster than on ILists.
                return arr.LowHigh(startIndex, count);
            }
            var maxCount = a.Count - startIndex;
            count = count < 0 ? maxCount : Min(maxCount,count);
            if (count <= 1) return a;
            var stopIndex = startIndex + count - 1;
            var min = a[startIndex];
            var max = min;
            int iMin = startIndex, iMax = startIndex;
            for (var i = startIndex + 1; i <= stopIndex; i++)
            {
                var item = a[i];
                if (item.CompareTo(min) < 0)
                {
                    iMin = i;
                    min = item;
                }
                else if (item.CompareTo(max) > 0)
                {
                    iMax = i;
                    max = item;
                }
            }
            TElem temp;
            if (iMin != startIndex)
            {
                temp = a[startIndex];
                a[startIndex] = min;
                a[iMin] = temp;
            }
            if (iMax != stopIndex)
            {
                temp = a[stopIndex];
                a[stopIndex] = max;
                a[iMax] = temp;
            }
            return a;
        }

        /// <summary>
        /// Ensure that the lowest value in the specified portion of the list is moved to the beginning 
        /// of the specified range and the largest is moved to the end. Leave all other items mostly as they 
        /// are, unsorted.
        /// </summary>
        /// <typeparam name="TElem">Type of element in the collection.</typeparam>
        /// <param name="a">List to alter in-place.</param>
        /// <param name="startIndex">Index of first element in the range to manipulate.</param>
        /// <param name="count">Number of consecutive items to scan for low and high values.</param>
        /// <returns>The same list, with at most four items moved to new locations.
        /// </returns>
        public static TElem[] LowHigh<TElem>(this TElem[] a, int startIndex = 0, int count = -1) where TElem : IComparable<TElem>
        {
            var maxCount = a.Length - startIndex;
            count = count < 0 ? maxCount : Min(maxCount, count);
            if (count <= 1) return a;
            var stopIndex = startIndex + count - 1;
            var min = a[startIndex];
            var max = min;
            int iMin = startIndex, iMax = startIndex;
            for (var i = startIndex + 1; i <= stopIndex; i++)
            {
                var item = a[i];
                if (item.CompareTo(min) < 0)
                {
                    iMin = i;
                    min = item;
                }
                else if (item.CompareTo(max) > 0)
                {
                    iMax = i;
                    max = item;
                }
            }
            TElem temp;
            if (iMin != startIndex)
            {
                temp = a[startIndex];
                a[startIndex] = min;
                a[iMin] = temp;
            }
            if (iMax != stopIndex)
            {
                temp = a[stopIndex];
                a[stopIndex] = max;
                a[iMax] = temp;
            }
            return a;
        }

    }
}
