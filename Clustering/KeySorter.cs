using System;
using System.Collections.Generic;

namespace Clustering
{
	/// <summary>
	/// Given a list of sorted objects that possess a key field, sort a second list of objects (with matching keys)
	/// in the same order as the sorted list.
	/// 
	/// This is equivalent to each object in the already-sorted list having a sort-order field, joining the two lists
	/// on the foreign key, sorting the combination on the sort-order field, then selecting just the part of the combination
	/// belonging to the second list.
	/// 
	/// Two algorithms are used depending on how dense is the array of keys. If the range R from the lowest key to the highest key
	/// is not much higher than N, then a possibly sparse array is used as the basis of a dictionary. 
    /// Otherwise, a Dictionary(int,T) is used. 
    /// 
    /// The ratio is the sparseness. If the sparseness is two, then use a sparse array if 2N >= R.
    /// A good value for sparseness is log2(N).
	/// </summary>
	public class KeySorter<TSorted, TUnsorted>
	{
		/// <summary>
		/// Extract the foreign key from the sorted objects.
		/// </summary>
		Func<TSorted, int> ForeignKeySorted { get; set; }

		/// <summary>
		/// Extract the foreign key from the unsorted objects.
		/// </summary>
		Func<TUnsorted, int> ForeignKeyUnsorted { get; set; }

		public KeySorter(Func<TSorted, int> foreignKeySorted, Func<TUnsorted, int> foreignKeyUnsorted)
		{
			ForeignKeySorted = foreignKeySorted;
			ForeignKeyUnsorted = foreignKeyUnsorted;
		}

		private Tuple<int, int> KeyRange(IList<TSorted> items)
		{
			var minKey = int.MaxValue;
			var maxKey = int.MinValue;
			foreach (var item in items)
			{
				var key = ForeignKeySorted(item);
				if (key > maxKey)
					maxKey = key;
				if (key < minKey)
					minKey = key;
			}
			return new Tuple<int, int>(minKey, maxKey);
		}

		/// <summary>
		/// Sort the unsortedItems such that they are in the same sequence as the corresponding sortedItems.
		/// 
		/// The two lists must have the same number of items and each item in one must correspond to exactly one in the other.
		/// </summary>
		/// <param name="unsortedItems">Unsorted items.</param>
		/// <param name="sortedItems">Sorted items.</param>
        /// <param name="sparseness">If there are N items to sort, and the range from the lowest to highest id is R, then only
        /// use a sparse array to sort if sparseness * N >= R.
        /// If zero is supplied, use Log2(N).</param>
		/// <returns>The items from unsortedItems, sorted.</returns>
		public TUnsorted[] Sort(IList<TUnsorted> unsortedItems, IList<TSorted> sortedItems, double sparseness = 2.0)
		{
			var count = sortedItems.Count;
			var sameSortedItems = new TUnsorted[count];
			var range = KeyRange(sortedItems);
			var span = range.Item2 - range.Item1 + 1;
            if (sparseness == 0.0)
                sparseness = Math.Log(count, 2);
			if (count * sparseness < span)
			{
				// Sparse Ids - use a regular Dictionary.
				var idToPositionLookup = new Dictionary<int, int>();
				var index = 0;
				// Record sort position for each key.
				foreach (var item in sortedItems)
					idToPositionLookup[ForeignKeySorted(item)] = index++;
				// Perform the sort
				foreach (var item in unsortedItems)
					sameSortedItems[idToPositionLookup[ForeignKeyUnsorted(item)]] = item;
			}
			else 
			{
				// Dense Ids - use an Array in place of a Dictionary.
				// We will skip over slot one in the array, because if we are sparse,
				// then zeroes will be used to indicate missing Ids, so we do not want index zero to
				// hold meaningful data.
				var idToPositionLookup = new int[span + 1]; 
				var offset = 1 - range.Item1; // Add this offset to an id to find its position in the lookup.
				var index = 0;
				// Record sort position for each key.
				foreach (var item in sortedItems)
					idToPositionLookup[ForeignKeySorted(item) + offset] = index++;
				// Perform the sort
				foreach (var item in unsortedItems)
					sameSortedItems[idToPositionLookup[ForeignKeyUnsorted(item) + offset]] = item;
			}
			return sameSortedItems;
		}
	}
}
