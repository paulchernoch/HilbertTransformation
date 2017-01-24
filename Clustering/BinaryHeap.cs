using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HilbertTransformation;

namespace Clustering
{
    /// <summary>
    /// Identifies the type of heap as a Min-Heap or a Max-Heap.
    /// 
    /// This is called the "heap property" by some.
    /// </summary>
    public enum BinaryHeapType
    {
        /// <summary>
        /// The first element in the heap is the minimum value among all elements,
        /// and Extreme always returns the minimum value.
        /// </summary>
        MinHeap,
        /// <summary>
        /// The first element in the heap is the maximum value among all elements,
        /// and Extreme always returns the maximum value.
        /// </summary>
        MaxHeap
    }

    /// <summary>
    /// When using the BinaryHeap to select Top N or Bottom N items in parallel, this holds the parallelism options.
    /// </summary>
    public class SelectParallelOptions
    {
        private static readonly Lazy<SelectParallelOptions> _serialInstance
            = new Lazy<SelectParallelOptions>(() => new SelectParallelOptions { TaskCount = 1 });

        /// <summary>
        /// Instance that forces serial processing.
        /// </summary>
        public static SelectParallelOptions Serial
        {
            get { return _serialInstance.Value; }
        }

        /// <summary>
        /// Number of independent parallel tasks to run, which defaults to Environment.ProcessorCount.
        /// </summary>
        public int TaskCount { get; set; }

        /// <summary>
        /// Number of items to grab at a time from the enumerator, which defaults to 1024.
        /// </summary>
        public int BatchSize { get; set; }

        public SelectParallelOptions()
        {
            TaskCount = Environment.ProcessorCount;
            BatchSize = 1024;
        }
    }

    /// <summary>
    /// Use a BinaryHeap to find the TopN or BottomN items from a stream in sorted order, either serially or in parallel.
    /// 
    /// The sort order of the result is as follows:
    ///    When finding the Bottom N, sort from highest to lowest. Thus the first element returned is the Nth smallest item.
    ///    When finding the Top N, sort from lowest to highest. Thus the first element returned is the Nth largest item.
    /// </summary>
    public static class BinaryHeapExtensions
    {
        #region Serial Selection

        /// <summary>
        /// Find the K smallest items using a serial procedure, sorted from largest to smallest
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <param name="k">Number of items desired.</param>
        /// <param name="comparisonDelegate">If omitted, assume the elements are IComparable and use their default collation order.
        /// Otherwise use this method to compare the items.</param>
        /// <returns>The smallest items or earliest in the collation order.</returns>
        public static IEnumerable<TElement> BottomNSerial<TElement>(this IEnumerable<TElement> items, int k,
            IComparer<TElement> comparisonDelegate = null)
        {
            return SelectSerial(items, false, k, comparisonDelegate);
        }

        /// <summary>
        /// Find the K largest items using a serial procedure, sorted from smallest to largest.
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <param name="k">Number of items desired.</param>
        /// <param name="comparisonDelegate">If omitted, assume the elements are IComparable and use their default collation order.
        /// Otherwise use this method to compare the items.</param>
        /// <returns>The largest items or last in the collation order.</returns>
        public static IEnumerable<TElement> TopNSerial<TElement>(this IEnumerable<TElement> items, int k,
            IComparer<TElement> comparisonDelegate = null)
        {
            return SelectSerial(items, true, k, comparisonDelegate);
        }

		/// <summary>
		/// Select either the Top N or Bottom N items in sorted order from the given collection, serially (not in parallel).
		/// 
		/// This only performs a partial sort.
		/// </summary>
		/// <typeparam name="TElement">Type of element in the collection.</typeparam>
		/// <param name="items">Collection of items to sort and select.</param>
		/// <param name="topN">If true, find the Top N items in descending order, otherwise the Bottom N items in ascending order.</param>
		/// <param name="k">Number of items to select.</param>
		/// <param name="comparisonDelegate">If null, assume the items are IComparable and sort them according to their natural ordering.
		/// If not null, use this in the comparisons to establish the ordering.</param>
		/// <returns>The Top N or Bottom N items, as requested, sorted appropriately</returns>
		static IEnumerable<TElement> SelectSerial<TElement>(IEnumerable<TElement> items, bool topN, int k,
			IComparer<TElement> comparisonDelegate = null)
		{
			// Seems counterintuitive, but when looking for the Top N we use a Min Heap, and when 
			// looking for the Bottom N we use a Max Heap.
			var heap = new BinaryHeap<TElement>(topN ? BinaryHeapType.MinHeap : BinaryHeapType.MaxHeap, k, comparisonDelegate);
			foreach (var item in items)
			{
				if (k > heap.Count) heap.Add(item);
				else if (heap.IsLessExtreme(item))
				{
					heap.Remove();
					heap.Add(item);
				}
			}
			var resultsCount = heap.Count;
			for (var i = 0; i < resultsCount; i++)
				yield return heap.Remove();
		}

		#endregion

		#region Parallel Selection

		/// <summary>
		/// Find the K smallest items (or earliest according to a collation sequence), sorted from smallest to largest.
		/// 
		/// NOTE: Items are returned in reverse order compared to the other extension methods.
		/// NOTE: This will use either a serial or parallel approach, depending upon the TaskCount in the options.
		/// </summary>
		/// <typeparam name="TMeasurable">Type of item to be sorted by its measure.</typeparam>
		/// <typeparam name="TMeasure">Type of measure.</typeparam>
		/// <param name="items">Items to be sorted and selected.</param>
		/// <param name="reference">Reference item to use in comparison.
		/// For example, if finding the K-nearest neighbors of a certain point, this is that point.
		/// </param>
		/// <param name="k">Number of items to select.</param>
		/// <param name="options">Parallel sort options.
		/// If TaskCount is one, we will perform the operation serially, otherwise in parallel.</param>
		/// <returns>The smallest items</returns>
		public static IEnumerable<TMeasurable> BottomN<TMeasurable, TMeasure>(this IEnumerable<TMeasurable> items,
            TMeasurable reference, int k, SelectParallelOptions options = null)
            where TMeasure : IComparable<TMeasure>
            where TMeasurable : IMeasurable<TMeasurable, TMeasure>
        {
            if (options == null)
                options = new SelectParallelOptions();
            MeasuredItem<TMeasurable, TMeasure>[] measuredItems;
            if (options.TaskCount == 1)
                measuredItems =
                    items.Select(item => new MeasuredItem<TMeasurable, TMeasure>(item, reference.Measure(item), true))
                        .ToArray();
            else
            {
                var itemsArray = items as TMeasurable[] ?? items.ToArray();
                measuredItems = new MeasuredItem<TMeasurable, TMeasure>[itemsArray.Length];
                // See http://www.codeproject.com/Articles/451628/Efficient-Map-Operations-for-Arrays-in-Csharp
                // Parallel.ForEach with range partitioner may be faster than other ways for > 10000 items.
                // It certainly was faster than using Task.Factory.StartNew and Task.WaitAll.
                Parallel.ForEach(Partitioner.Create(0, itemsArray.Length),
                    range =>
                    {
                        for (int i = range.Item1; i < range.Item2; ++i)
                        {
                            var item = itemsArray[i];
                            // ReSharper disable once AccessToModifiedClosure
                            measuredItems[i] = new MeasuredItem<TMeasurable, TMeasure>(item, reference.Measure(item), true);
                        }
                    }
                );
            }
            return measuredItems
                .BottomNParallel(k, null, options)
                .Select(measuredItem => measuredItem.Item)
                .Reverse()
                ;
        }

        public static IEnumerable<TMeasurable> TopN<TMeasurable, TMeasure>(this IEnumerable<TMeasurable> items,
    TMeasurable reference, int k, SelectParallelOptions options = null)
            where TMeasure : IComparable<TMeasure>
            where TMeasurable : IMeasurable<TMeasurable, TMeasure>
        {
            if (options == null)
                options = new SelectParallelOptions();
            MeasuredItem<TMeasurable, TMeasure>[] measuredItems;
            if (options.TaskCount == 1)
                measuredItems =
                    items.Select(item => new MeasuredItem<TMeasurable, TMeasure>(item, reference.Measure(item), true))
                        .ToArray();
            else
            {
                var itemsArray = items as TMeasurable[] ?? items.ToArray();
                measuredItems = new MeasuredItem<TMeasurable, TMeasure>[itemsArray.Length];
                // See http://www.codeproject.com/Articles/451628/Efficient-Map-Operations-for-Arrays-in-Csharp
                // Parallel.ForEach with range partitioner may be faster than other ways for > 10000 items.
                // It certainly was faster than using Task.Factory.StartNew and Task.WaitAll.
                Parallel.ForEach(Partitioner.Create(0, itemsArray.Length),
                    range =>
                    {
                        for (int i = range.Item1; i < range.Item2; ++i)
                        {
                            var item = itemsArray[i];
                            // ReSharper disable once AccessToModifiedClosure
                            measuredItems[i] = new MeasuredItem<TMeasurable, TMeasure>(item, reference.Measure(item), true);
                        }
                    }
                );
            }
            return measuredItems
                .TopNParallel(k, null, options)
                .Select(measuredItem => measuredItem.Item)
                .Reverse()
                ;
        }

        /// <summary>
        /// Find the K smallest items using a parallel procedure, sorted from largest to smallest.
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <param name="k">Number of items desired.</param>
        /// <param name="comparisonDelegate">If omitted, assume the elements are IComparable and use their default collation order.
        /// Otherwise use this method to compare the items.</param>
        /// <param name="options">If null, use the default values, otherwise use these options to control the parallelism.</param>
        /// <returns>The smallest items or earliest in the collation order.</returns>
        public static IEnumerable<TElement> BottomNParallel<TElement>(this IEnumerable<TElement> items, int k,
            IComparer<TElement> comparisonDelegate = null, SelectParallelOptions options = null)
        {
            return SelectParallel(items, false, k, comparisonDelegate, options);
        }

        /// <summary>
        /// Find the K largest items using a serial procedure, sorted from smallest to largest.
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <param name="k">Number of items desired.</param>
        /// <param name="comparisonDelegate">If omitted, assume the elements are IComparable and use their default collation order.
        /// Otherwise use this method to compare the items.</param>
        /// <param name="options">If null, use the default values, otherwise use these options to control the parallelism.</param>
        /// <returns>The largest items or last in the collation order.</returns>
        public static IEnumerable<TElement> TopNParallel<TElement>(this IEnumerable<TElement> items, int k,
            IComparer<TElement> comparisonDelegate = null, SelectParallelOptions options = null)
        {
            return SelectParallel(items, true, k, comparisonDelegate, options);
        }

		/// <summary>
		/// Select either the Top N or Bottom N items in sorted order from the given collection, in parallel.
		/// 
		/// This only performs a partial sort.
		/// </summary>
		/// <typeparam name="TElement">Type of element in the collection.</typeparam>
		/// <param name="items">Collection of items to sort and select.</param>
		/// <param name="topN">If true, find the Top N items in descending order, otherwise the Bottom N items in ascending order.</param>
		/// <param name="k">Number of items to select.</param>
		/// <param name="comparisonDelegate">If null, assume the items are IComparable and sort them according to their natural ordering.
		/// If not null, use this in the comparisons to establish the ordering.</param>
		/// <param name="options">If null, use the default values, otherwise use these options to control the parallelism.</param>
		/// <returns>The Top N or Bottom N items, as requested, sorted appropriately</returns>
		static IEnumerable<TElement> SelectParallel<TElement>(IEnumerable<TElement> items, bool topN, int k,
			IComparer<TElement> comparisonDelegate = null, SelectParallelOptions options = null)
		{
			options = options ?? new SelectParallelOptions();

			// If we are only dedicating a single task to the operation, do it serially to save on Task overhead.
			if (options.TaskCount == 1)
				return SelectSerial(items, topN, k, comparisonDelegate);

			var tasks = new Task[options.TaskCount];
			var extremeItems = new List<TElement>();
			var enumerator = items.GetEnumerator();
			for (var i = 0; i < options.TaskCount; i++)
			{
				var iTask = i;
				var batch = new TElement[options.BatchSize];
				tasks[iTask] = Task.Factory.StartNew(() =>
				{
					var heap = new BinaryHeap<TElement>(topN ? BinaryHeapType.MinHeap : BinaryHeapType.MaxHeap, k + 1, comparisonDelegate);
					var moreItems = true;
					var batchSize = options.BatchSize;
					while (moreItems)
					{
						var iReadCount = 0;
						lock (enumerator)
						{
							for (var iBatch = 0; iBatch < batchSize && moreItems; iBatch++)
							{
								if (enumerator.MoveNext())
									batch[iReadCount++] = enumerator.Current;
								else
									moreItems = false;
							}
						}
						for (var iBatch = 0; iBatch < iReadCount; iBatch++)
						{
							var item = batch[iBatch];
							if (k + 1 > heap.Count) heap.Add(item);
							else if (heap.IsLessExtreme(item))
							{
								heap.Remove();
								heap.Add(item);
							}
						}
					}
					lock (extremeItems)
					{
						extremeItems.AddRange(heap.RemoveAll());
					}
				});
			}
			Task.WaitAll(tasks);
			//  At this point we have as many as k*TaskCount items left. Take the k most extreme.
			return SelectSerial(extremeItems, topN, k, comparisonDelegate);
		}

		#endregion

	}

    /// <summary>
    /// Provides a binary heap that may be used as either a Min-Heap or a Max-Heap and is useful when implementing a priority queue.
    /// 
    /// This data structure allows one to efficiently get the minimum (for a Min-Heap) or the maximum (for a Max-Heap) value
    /// in the heap, add elements and remove the minimum/maximum element. The RemoveAll method returns the elements in 
    /// sorted order.
    /// 
    /// The heart of the structure is a complete binary tree that uses a clever and compact arrangement of elements so that 
    /// pointers are not needed. See http://en.wikipedia.org/wiki/Binary_heap for details.
    /// 
    /// Insert.     On average, insertions require 2.6067 commparisons (constant time). Worst case is log(N).
    /// Extreme.    To get the minimum (for a Min-Heap) or the maximum (for a Max-Heap) always requires constant time.
    /// Remove.     Requires log(N) time.
    /// RemoveAll.  Requires N*Log(N) time.
    /// 
    /// Use Case: Use a Max-Heap to find the K-lowest integers in an array, sorted from high to low.
    /// 
    ///     var data = new int[] { ... numbers to study ... };
    ///     var k = 50;
    ///     var heap = new BinaryHeap{int}(BinaryHeapType.MaxHeap, k);
    ///     foreach(var i in data)
    ///     {
    ///         if (k > heap.Count) heap.Add(i);
    ///         else if (heap.IsLessExtreme(i))
    ///         {
    ///             heap.Remove();
    ///             heap.Add(i);
    ///         }
    ///     }
    ///     var lowestFiftySorted = heap.RemoveAll();
    /// </summary>
    public class BinaryHeap<TElement>
    {
        #region Properties (Capacity, Count, Heap, Comparer, etc)

        /// <summary>
        /// Declares whether the heap is a Min-Heap or a Max-Heap.
        /// </summary>
        public BinaryHeapType Ordering { get; private set; }

        /// <summary>
        /// Used to compare items to establish ordering when adding and removing elements.
        /// </summary>
        private Func<TElement, TElement, int> Comparer { get; set; }

        /// <summary>
        /// Maximum capacity of the heap.
        /// </summary>
        public int Capacity { get;  private set; }

        /// <summary>
        /// Current number of elements in the heap.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Is the heap full?
        /// 
        /// Additional items may not be added to a full heap until some are first removed.
        /// </summary>
        public bool IsFull { get { return Count == Capacity; } }

        /// <summary>
        /// Holds the data in a complete binary tree that does not use pointers.
        /// Element zero is not used in order to make the indexing logic easier, so the array size is Capacity+1..
        /// For the element at index I, its children are at index 2I and 2I+1, and its parent at I/2.
        /// </summary>
        private TElement[] Heap { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Create a BinaryHeap of the given type and capacity which uses the given delegate when ordering elements. 
        /// </summary>
        /// <param name="ordering">Establish the heap as a minimum heap or a maximum heap.</param>
        /// <param name="capacity">Maximum number of items that the heap can hold.</param>
        /// <param name="comparisonDelegate">Used to compare items and establish their ordering.
        /// If null, assume the elements are IComparable and use their CompareTo method.</param>
        public BinaryHeap(BinaryHeapType ordering, int capacity, IComparer<TElement> comparisonDelegate = null)
        {
            Ordering = ordering;
            Capacity = capacity;
            Heap = new TElement[capacity + 1];
            if (comparisonDelegate != null)
                Comparer = comparisonDelegate.Compare;
            else
                Comparer = (e1, e2) => ((IComparable<TElement>)e1).CompareTo(e2);
            Count = 0;
        }

        #endregion

        #region Public Heap methods: Extreme, IsLessExtreme, Add, Remove, RemoveAll

        /// <summary>
        /// Get the extreme value stored in the heap, which for Minimum heaps is the minimum value, 
        /// and for Maximum heaps is the maximum value.
        /// 
        /// If the heap is empty, return the default value for the type (null for reference types).
        /// </summary>
        public TElement Extreme
        {
            // If the heap is empty, Heap[1] should already be set to default(TElement) so no need to check
            // if Count == 0.
            get { return Heap[1]; }
        }

        /// <summary>
        /// Determine if the given element is less extreme than this heap's Extreme element.
        /// </summary>
        /// <param name="element">Element to compare to the Extreme element already in the Heap.</param>
        /// <returns>
        /// If this is a MinHeap, return true if element is larger than Extreme.
        /// If this is a MaxHeap, return true if element is smaller than Extreme.
        /// If this heap is empty, always return true.
        /// </returns>
        public bool IsLessExtreme(TElement element)
        {
            if (Count == 0) return true;
            var comparison = Compare(element, Extreme);
            if (Ordering == BinaryHeapType.MinHeap) return comparison > 0;
            return comparison < 0;
        }

        public void Add(TElement element)
        {
            if (Count == Capacity) throw new IndexOutOfRangeException("BinaryHeap is filled to capacity.");
            Count++;
            var position = Count;
            var heap = Heap;

            // Remember: We are one-based and do not use Heap[0] to make the index logic simpler.
            heap[position] = element;

            // inline this for performance: BubbleUp(position);
            while (true)
            {
                if (position == 1) return;
                var parentPosition = position / 2;
                if (IsOrdered(parentPosition, position)) return;

                // inline Swap: Swap(parentPosition, position);
                var temp = heap[parentPosition];
                heap[parentPosition] = heap[position];
                heap[position] = temp;
                position = parentPosition;
            }
        }

        /// <summary>
        /// Attempt to add this element, and if the heap is already full, remove an existing element
        /// to make room for it.
        /// </summary>
        /// <param name="element">Element to add. 
        /// If the heap is not yet full, add this element and return default(TEelement), to indicate
        /// that nothing was removed.
        /// 
        /// If the heap is already full and this item is less extreme than the most extreme element
        /// already in the heap, add this element and remove the old extreme element.
        /// 
        /// Otherwise, do not add this element to the heap and return it as the removed element.</param>
        /// <returns>The removed element or default(TElement) if nothing was removed.</returns>
        public TElement AddRemove(TElement element)
        {
            TElement removedElement;
            if (IsFull)
            {
                if (IsLessExtreme(element))
                {
                    removedElement = Remove();
                    Add(element);
                }
                else
                    removedElement = element;
            }
            else
            {
                removedElement = default(TElement);
                Add(element);
            }
            return removedElement;
        }

        /// <summary>
        /// Remove the head of the tree, which is the Extreme element (the minimum value for a Min-Heap 
        /// or the maximum value for a Max-Heap).
        /// </summary>
        /// <returns>The item removed, which equals the Extreme value.
        /// If empty, this returns the default value for the type, often null.</returns>
        public TElement Remove()
        {
            var heap = Heap;
            var count = Count;
            var extremeBeforeRemove = Extreme;
            if (count == 0) return extremeBeforeRemove;

            heap[1] = heap[count];
            heap[count] = default(TElement);
            Count--;
            // inline this for performance: BubbleDown(1);
            {
                count = Count;
                var position = 1;
                while (true)
                {
                    if (position * 2 > count) break; // No children - this is a leaf.
                    var extremeChild = ExtremeChild(position);
                    if (IsOrdered(position, extremeChild)) break;

                    // Swap(position, extremeChild);
                    var temp = heap[position];
                    heap[position] = heap[extremeChild];
                    heap[extremeChild] = temp;

                    position = extremeChild;
                }
            }

            return extremeBeforeRemove;
        }

        /// <summary>
        /// Remove all elements from the heap and return an array holding the removed items in sorted order.
        /// If it was a MinHeap, the items will be sorted from smallest to largest.
        /// If it was a MaxHeap, the items will be sorted from largest to smallest.
        /// </summary>
        /// <returns>Array containing all elements originally in the heap.
        /// This array has exactly Count elements and is zero-based, unlike the Heap array, which leaves the zero element empty.</returns>
        public TElement[] RemoveAll()
        {
            var resultsCount = Count;
            var sortedElements = new TElement[resultsCount];
            for (var i = 0; i < resultsCount; i++)
                sortedElements[i] = Remove();
            return sortedElements;
        }

        #endregion

        #region Compare, IsOrdered, Swap, ExtremeChild, BubbleUp and BubbleDown helper methods

        /// <summary>
        /// Compare two elements and decide whether the first element is less than, equal to or greater than the second.
        /// </summary>
        /// <param name="e1">First element to compare.</param>
        /// <param name="e2">Second element to compare.</param>
        /// <returns>-1 if the first element is less than the second.
        /// 0 if the elements are equal.
        /// 1 If the first element is greater than the second.</returns>
        public int Compare(TElement e1, TElement e2)
        {
            return Comparer(e1, e2);
        }

		/// <summary>
		/// Decide if the given parent and child are properly ordered.
		/// 
		/// For a min-heap, parent must be less than child.
		/// For a max-heap, parent must be greater than child.
		/// </summary>
		/// <param name="parentPosition">One-based position of parent in the heap.</param>
		/// <param name="childPosition">One-based position of child in the heap.</param>
		/// <returns>True if the parent is less than the child (for a min-heap) or the opposite (for a max-heap).</returns>
		bool IsOrdered(int parentPosition, int childPosition)
		{
			var comparison = Compare(Heap[parentPosition], Heap[childPosition]);
			return Ordering == BinaryHeapType.MinHeap ? (comparison <= 0) : (comparison >= 0);
		}

		/// <summary>
		/// Swap two elements in the heap.
		/// </summary>
		/// <param name="position1">One-based position of first element to swap.</param>
		/// <param name="position2">One-based position of second element to swap.</param>
		private void Swap(int position1, int position2)
        {
            var temp = Heap[position1];
            Heap[position1] = Heap[position2];
            Heap[position2] = temp;
        }

        /// <summary>
        /// Swap an element with its parent if they are not in correct order, then repeat for the parent's parent, all the way up.
        /// </summary>
        /// <param name="position">One-based position of element to bubble.</param>
        private void BubbleUp(int position)
        {
            var heap = Heap;
            while (true)
            {
                if (position == 1) return;
                var parentPosition = position / 2;
                if (IsOrdered(parentPosition, position)) return;

                //Inline: Swap(parentPosition, position);
                var temp = heap[parentPosition];
                heap[parentPosition] = heap[position];
                heap[position] = temp;

                position = parentPosition;
            }
        }

        /// <summary>
        /// Return the smaller child of the element at the given position for a Min-Heap or the larger child for a Max-Heap.
        /// 
        /// If an element only has one child, that child is returned.
        /// </summary>
        /// <param name="position">One-based position of an element.</param>
        /// <returns>One-based index of one of the two children of the element at the given position.</returns>
        private int ExtremeChild(int position)
        {
            var heap = Heap;
            var child1 = 2 * position;
            if (child1 >= Count) return child1;
            var child2 = child1 + 1;
            var comparison = Compare(heap[child1], heap[child2]);
            // Return the smaller child for a Min-Heap.
            if (Ordering == BinaryHeapType.MinHeap) return comparison <= 0 ? child1 : child2;
            // Return the larger child for a Max-Heap.
            return comparison >= 0 ? child1 : child2;
        }

        /// <summary>
        /// Swap an element with the more extreme of its children if the two are not properly ordered.
        /// Continue down until we reach the leaves of the tree, or ordering is restored.
        /// </summary>
        /// <param name="position">One-based position of element to bubble down.</param>
        private void BubbleDown(int position)
        {
            var heap = Heap;
            var count = Count;
            while (true)
            {
                if (position * 2 > count) return; // No children - this is a leaf.
                var extremeChild = ExtremeChild(position);
                if (IsOrdered(position, extremeChild)) return;

                // Inline: Swap(position, extremeChild);
                var temp = heap[position];
                heap[position] = heap[extremeChild];
                heap[extremeChild] = temp;

                position = extremeChild;
            }
        }

        #endregion
    }

}
