using HilbertTransformation.Random;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;

namespace Clustering
{
    /// <summary>
    /// Sort objects using a comparison-based Bucket sort that limits the number of sort keys in existence at 
    /// any given time to conserve memory. This is designed for situations when the computation of the
    /// sort key is expensive both in time and memory, such as when sorting by the Hilbert curve position for 
    /// points with thousands of dimensions.
    /// </summary>
    /// <remarks>
    /// Inputs:
    ///   A. "B", the limit of the maximum number of keys that can exist and buckets created
    ///   B. IList of Items to sort (with count N) 
    ///   C. Delegate to generate an IComparable sort key for each item
    ///   D. Delegate to obtain a hashcode for each item to help distinguish items (default is the item's hashcode method)
    ///   E. Delegate that can tell if two items are identical for the purposes of the sort (default assumes items are IComparable)
    ///   
    /// Algorithm:
    ///   1. Randomize the item order in place
    ///   2. Choose the first B items as pivots for B+1 buckets, swapping in alternates if any are duplicates
    ///   3. Compute corresponding array of sort keys for the pivots
    ///   4. Retain a dictionary mapping hashcodes to pivots. If a pivot is selected that is equivalent
    ///      to an already chosen pivot, choose another pivot in its place.
    ///   5. Use Array.Sort (that takes parallel arrays of items and keys) to quicksort the pivot items by their sort key.
    ///   6. Create a bucket (list) for each pivot, plus one for the items that sort after the last pivot.
    ///   7. Loop over all non-pivot items SERIALLY.
    ///   8.    Compute sort key for the item.
    ///   9.    Perform binary search of the pivot sort key array to identify which bucket to put the item in.
    ///  10.    Discard the sort key for the item once it is added to the bucket
    ///  11. Create array to hold the results.
    ///  12. Loop over all buckets in bucket sorted order
    ///  13.    Use Quicksort (Linq Order By) to sort the elements in the bucket
    ///  14.    Append sorted bucket to result array.
    /// 
    /// </remarks>
    public class SmallBucketSort<TItem>
    {

        #region Attributes

        /// <summary>
        /// No more than this many buckets will be created at one time,
        /// and no more than this many sort-keys (plus one) will be retained at the same time,
        /// to conserve memory.
        /// </summary>
        public int MaxBuckets { get; set; }

        /// <summary>
        /// Items to sort.
        /// </summary>
        public IList<TItem> Items { get; }
 
        public int Count => Items.Count;

        /// <summary>
        /// Obtains the sort key for an item.
        /// </summary>
        public Func<TItem,IComparable> Ordering { get; set; }

        /// <summary>
        /// Obtains the hashcode to use for an item to distinguish it from other items
        /// for the purpose of this sort. It may differ from the normal hashcode for the item.
        /// 
        /// The default is a function that uses the item's normal hashcode.
        /// </summary>
        public Func<TItem, int> Hash { get; set; }

        public int MinimumSizeForBucketSort { get; set; } = 10000;

        #endregion

        public SmallBucketSort(IList<TItem> items, Func<TItem, IComparable> ordering)
        {
            Items = items;
            MaxBuckets = (int) (2 * Sqrt(Count));
            Ordering = ordering;
            Hash = (item) => item.GetHashCode();
        }

        /// <summary>
        /// Sort the items by a sort key derived by calling the supplied delegate on each item.
        /// 
        /// The list is copied during the sort operation, so items are not sorted in place.
        /// </summary>
        /// <typeparam name="T">Type of item to be sorted.</typeparam>
        /// <param name="items">Items to be sorted.</param>
        /// <param name="ordering">Delegate that extracts the sort key from an item.</param>
        /// <returns>A new List of the original items, now sorted.</returns>
        public static List<T> Sort<T>(IReadOnlyList<T> items, Func<T, IComparable> ordering)
        {
            var unsortedItems = items.ToList();
            var sorter = new SmallBucketSort<T>(unsortedItems, ordering);
            return sorter.Sort();
        }

        /// <summary>
        /// Sort the items and return them in a new List.
        /// </summary>
        /// <returns>A new List of the items, sorted.</returns>
        public List<TItem> Sort()
        {
            if (Count < MinimumSizeForBucketSort)
                return Items.OrderBy(i => Ordering(i)).ToList();
            
            //   1. Randomize the item order in place
            Shuffle(Items);

            //   2. Choose the first B items as pivots for B+1 buckets
            var bucketItems = Items.Take(MaxBuckets).ToArray();

            //   3. Compute corresponding array of sort keys for the unsorted pivots
            var bucketSortKeys = Items.Take(MaxBuckets).Select(item => Ordering(item)).ToArray();

            //   4. Use Array.Sort (that takes parallel arrays of items and keys) to quicksort the pivot items by their sort key.
            Array.Sort(bucketSortKeys, bucketItems, 0, MaxBuckets);

            //   5. Create a bucket (list) for each pivot, plus one for the items that sort after the last pivot.
            var buckets = Enumerable.Range(0, MaxBuckets + 1).Select(i => new List<TItem>()).ToArray();

            //   6.  Put each bucket item into its corresponding bucket. The last bucket has no items at first.
            for (var iBucket = 0; iBucket < MaxBuckets; iBucket++)
                buckets[iBucket].Add(bucketItems[iBucket]);

            //   7. Loop over all non-pivot items.
            foreach (var item in Items.Skip(MaxBuckets))
            {
                //   8. Compute sort key for the item.
                var sortKey = Ordering(item);

                //   9. Perform binary search of the pivot sort key array to identify into which bucket to distribute the item.
                var iBucket = Array.BinarySearch(bucketSortKeys, 0, MaxBuckets, sortKey);
                if (iBucket < 0) iBucket = ~iBucket;
                buckets[iBucket].Add(item);

                //  10. Discard the sort key for the item once it is added to the bucket
                sortKey = null;
            }
            bucketSortKeys = null;

            //  11. Create a list to hold the sorted results.
            var sortedItems = new List<TItem>(Count);

            //  12. Loop through all buckets to sort them and then append to the results.
            foreach (var bucket in buckets)
                sortedItems.AddRange(bucket.OrderBy(item => Ordering(item)));
            
            return sortedItems;
        }

        private static FastRandom Rng = new FastRandom();

        /// <summary>
		/// Shuffle list in place using the unbiased Knuth-Fischer-Yates shuffle,
        /// but do not permit any duplicate values among the first MaxBuckets items.
		/// </summary>
		/// <returns>The same list, shuffled.</returns>
		public IList<TItem> Shuffle(IList<TItem> items)
        {
            var n = items.Count;
            while (n > 0)
            {
                var k = Rng.Next(n);   // Use rand(n) rather than rand(count) or the shuffle is not perfectly random!
                n--;
                var temp = items[k];
                items[k] = items[n];
                items[n] = temp;
            }
            var hashes = new HashSet<int>();
            var nextSwapPosition = Count - 1;
            // If any values among the first MaxBuckets positions (following the shuffle)
            // are duplicates, swap them with items not part of that range.
            for (var i = 0; i < MaxBuckets; i++)
            {
                var hash = Hash(Items[i]);
                while (hashes.Contains(hash) && nextSwapPosition >= MaxBuckets)
                {
                    var temp = Items[i];
                    Items[i] = Items[nextSwapPosition];
                    Items[nextSwapPosition] = temp;
                    nextSwapPosition--;
                    hash = Hash(Items[i]);
                }
                hashes.Add(hash);
            }
            return items;
        }

    }
}
