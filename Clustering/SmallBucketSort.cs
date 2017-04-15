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
    /// sort key is expensive both in time and memory, such as when sorting by the Hilbert curve position.
    /// </summary>
    /// <remarks>
    /// Inputs:
    ///   A. "B", the limit of the maximum number of keys that can exist and buckets created
    ///   B. Items to sort (with count N) in an ArraySegment or Array
    ///   C. Delegate to generate an IComparable sort key for each item
    ///   D. Delegate to obtain a hashcode for each item to help distinguish items (default is the item's hashcode method)
    ///   E. Delegate that can tell if two items are identical for the purposes of the sort (default assumes items are IComparable)
    ///   F. Max degree of parallelism
    ///   
    /// Algorithm:
    ///   1. Randomize the item order in place
    ///   2. Choose the first B items as pivots for B+1 buckets
    ///   3. Compute corresponding array of sort keys for the pivots
    ///   4. Retain a dictionary mapping hashcodes to pivots. If a pivot is selected that is equivalent
    ///      to an already chosen pivot, choose another pivot in its place.
    ///   5. Use Array.Sort (that takes parallel arrays of items and keys) to quicksort the pivot items by their sort key.
    ///   6. Create a bucket (list) for each pivot, plus one for the items that sort after the last pivot.
    ///   7. Loop over all non-pivot items SERIALLY.
    ///   8.    Compute sort key for the item.
    ///   9.    Perform binary search of the pivot sort key array to identify which bucket to put the item in.
    ///  10.    Each bucket will also have a dictionary keyed on the item hashcodes that counts occurrances in that bucket.
    ///  11.    Discard the sort key for the item once it is added to the bucket
    ///  12. Reuse input array to hold sorted results.
    ///  13. In the end, buckets can be written in sorted pivot order to the result list.
    ///      So now, use the count of items in each bucket to compute the starting offset in the results list
    ///      where its items are to be stored.
    ///  14. Create ArraySegments referring to the underlying results array for each bucket to write into.
    ///  15. Loop over all buckets IN PARALLEL
    ///  16.    Associate hashcode for each item in bucket with the item in a Dictionary
    ///  17.    If the bucket has fewer items than B: 
    ///            a. Write the items for the bucket into its ArraySegment in unsorted order.
    ///            b. Associate each item with its sort key (in a parallel array).
    ///            c. Use Array.Sort to quicksort the value and sort key arrays simultaneously for the bucket in place.
    ///            d. Discard all sort keys for bucket after sorting is done.
    ///  18.    If the bucket has more items than B but the hascode dictionary suggests there are enough duplicates 
    ///         that there are fewer unique items than B, 
    ///            a. Write the items for the bucket into its ArraySegment in unsorted order.
    ///            b. Keep a dictionary that maps each item's sort key to itself.
    ///            c. Associate each item with its sort key (in a parallel array) but if another item has the same sort key,
    ///               reuse the sort key to save memory.
    ///            d. Use Array.Sort to quicksort the value and sort key arrays simultaneously for the bucket in place.
    ///            e. Discard all sort keys for bucket after sorting is done.
    ///  19.    Otherwise, call sort SmallBucketSort recursively using a smaller value for B, 
    ///         but indicate that it should not be in parallel
    ///  20. When all buckets have been processed, the items are sorted.
    /// 
    ///
    /// </remarks>
    public class SmallBucketSort<TItem>
    {

        #region Attributes

        /// <summary>
        /// In single-threaded operation, no more than this many buckets will be created at one time,
        /// and no more than this many sort-keys (plus one) will be retained at the same time,
        /// to conserve memory.
        /// </summary>
        public int MaxBuckets { get; set; }

        /// <summary>
        /// Items to sort.
        /// </summary>
        public TItem[] Items { get; }
 
        public int Count => Items.Length;

        /// <summary>
        /// Obtains the sort key for an item.
        /// </summary>
        public Func<TItem,IComparable<TItem>> Ordering { get; set; }

        /// <summary>
        /// Obtains the hashcode to use for an item to distinguish it from other items
        /// for the purpose of this sort. It may differ from the normal hashcode for the item.
        /// 
        /// The default is a function that uses the item's normal hashcode.
        /// </summary>
        public Func<TItem, int> Hash { get; set; }

        /// <summary>
        /// Decides if two items will have the same sort key.
        /// If so, they can share the same sort key, saving memory.
        /// 
        /// The default is a function that assumes the items are IEquatable.
        /// </summary>
        public Func<TItem,TItem,bool> KeyEquality { get; set; }

        /// <summary>
        /// Determines whether to perform the sort serially or in parallel, and
        /// the maximum number of threads to use.
        /// </summary>
        public int MaxDegreesOfParallelism { get; set; }

        /// <summary>
        /// If the number of items to be sorted is less than this, use quicksort
        /// without memory optimization.
        /// </summary>
        public int MinimumSizeForBucketSort { get; set; }

        #endregion

        public SmallBucketSort(TItem[] items, Func<TItem, IComparable<TItem>> ordering)
        {
            Items = items;
            MaxBuckets = (int) (2 * Sqrt(Count));
            Ordering = ordering;
            Hash = (item) => item.GetHashCode();
            KeyEquality = (item1, item2) => (item1 as IEquatable<TItem>).Equals(item2 as IEquatable<TItem>);
            MaxDegreesOfParallelism = Environment.ProcessorCount;
            MinimumSizeForBucketSort = 10000;
        }

        public SmallBucketSort(IList<TItem> items, Func<TItem, IComparable<TItem>> ordering) : this(items.ToArray(), ordering)
        {
        }

        public TItem[] Sort()
        {
            if (Count < MinimumSizeForBucketSort)
            {
                var sortKeys = Items.Select(item => Ordering(item)).ToArray();
                Array.Sort(sortKeys, Items);
            }
            else
            {
                //   1. Randomize the item order in place. No duplicates will be permitted among the first MaxBuckets items.
                Shuffle(Items);

                //   2. Compute corresponding array of sort keys for the unsorted pivots
                var bucketSortKeys = Items.Take(MaxBuckets).Select(item => Ordering(item)).ToArray();

                //   3. Use Array.Sort (that takes parallel arrays of items and keys) to quicksort the pivot items by their sort key.
                Array.Sort(bucketSortKeys, Items, 0, MaxBuckets);

                //   4. Choose the first MaxBuckets unique items as pivots for B+1 buckets and add each pivot to its bucket.
                var buckets = Enumerable.Range(0, MaxBuckets + 1).Select(i => new List<TItem>()).ToArray();
                var bucketHashCardinality = Enumerable.Range(0, MaxBuckets + 1).Select(i => new Dictionary<int, int>()).ToArray();
                for (var iBucket = 0; iBucket < MaxBuckets; iBucket++)
                {
                    buckets[iBucket].Add(Items[iBucket]);
                }

                //   7. Loop over all non-pivot items SERIALLY.
                foreach (var item in Items.Skip(MaxBuckets))
                {
                    //   8.    Compute sort key for the item.
                    var sortKey = Ordering(item);

                    //   9.    Perform binary search of the pivot sort key array to identify into which bucket to distribute the item.
                    var iBucket = Array.BinarySearch(Items, 0, MaxBuckets, sortKey);
                    if (iBucket < 0) iBucket = ~iBucket;
                    buckets[iBucket].Add(item);

                    //  10.    Discard the sort key for the item once it is added to the bucket
                    sortKey = null;

                    //  11.    Each bucket will also have a dictionary keyed on the item hashcodes that counts unique occurrances in that bucket.
                    var hash = Hash(item);
                    bucketHashCardinality[iBucket].TryGetValue(hash, out int cardinality);
                    bucketHashCardinality[iBucket][Hash(item)] = cardinality + 1; // Hash Collisions okay. Just need approximate cardinality. 
                }

                //  12. Reuse input array to hold sorted results.
                //  13. In the end, buckets can be written in sorted pivot order to the result list.
                //      So now, use the count of items in each bucket to compute the starting offset in the results list
                //      where its items are to be stored.
                var bucketSegments = new ArraySegment<TItem>[MaxBuckets + 1];
                var startOffset = 0;
                // MaxBuckets is the number of pivots. We have one more bucket than pivot.
                for (var iBucket = 0; iBucket <= MaxBuckets; iBucket++)
                {
                    //  14. Create ArraySegments referring to the underlying results array for each bucket to write into.
                    bucketSegments[iBucket] = new ArraySegment<TItem>(Items, startOffset, buckets[iBucket].Count);
                    startOffset += buckets[iBucket].Count;
                }


                //  15. Loop over all buckets IN PARALLEL
                Parallel.For(0, MaxBuckets + 1, new ParallelOptions {  MaxDegreeOfParallelism = MaxDegreesOfParallelism },
                    (iBucket) => {
                        var bucket = buckets[iBucket];
                        var bucketSegment = (IList<TItem>)bucketSegments[iBucket];
                        if (bucketHashCardinality[iBucket].Count() > MaxBuckets)
                        {

                        }
                        
              
                        buckets[iBucket] = null;
                        // Transcribe items to the results array unsorted, except for how they are distributed into buckets.
                        for (var i = 0; i < bucketSegment.Count; i++)
                            {
                                bucketSegment[i] = bucket[i];
                            }


                });

                //  16.    Associate hashcode for each item in bucket with the item in a Dictionary
                //  17.    If the bucket has fewer items than B: 
                //            a. Write the items for the bucket into its ArraySegment in unsorted order.
                //            b. Associate each item with its sort key (in a parallel array).
                //            c. Use Array.Sort to quicksort the value and sort key arrays simultaneously for the bucket in place.
                //            d. Discard all sort keys for bucket after sorting is done.
                //  18.    If the bucket has more items than B but the hascode dictionary suggests there are enough duplicates 
                //         that there are fewer unique items than B, 
                //            a. Write the items for the bucket into its ArraySegment in unsorted order.
                //            b. Keep a dictionary that maps each item's sort key to itself.
                //            c. Associate each item with its sort key (in a parallel array) but if another item has the same sort key,
                //               reuse the sort key to save memory.
                //            d. Use Array.Sort to quicksort the value and sort key arrays simultaneously for the bucket in place.
                //            e. Discard all sort keys for bucket after sorting is done.
                //  19.    Otherwise, call sort SmallBucketSort recursively using a smaller value for B, 
                //         but indicate that it should not be in parallel
                //  20. When all buckets have been processed, the items are sorted.
                // 

            }
            return Items;
        }

        private static FastRandom Rng = new FastRandom();

        /// <summary>
		/// Shuffle array in place using the unbiased Knuth-Fischer-Yates shuffle,
        /// but do not permit any duplicate values among the first MaxBuckets items.
		/// </summary>
		/// <returns>The same array, shuffled.</returns>
		public TItem[] Shuffle(TItem[] items)
        {
            var n = items.Length;
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

        private void Sort(IList<TItem> items)
        {
            var sortKeys = new Dictionary<TItem, IComparable<TItem>>();
            foreach(var item in items)
                sortKeys[item] = Ordering(item);
            //var adapter = ArrayList.Adapter(items);
        }

    }
}
