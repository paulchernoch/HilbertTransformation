using System;
using System.Collections.Generic;
using System.Linq;


namespace HilbertTransformation.Random
{
    /// <summary>
    /// Some utilitiy methods for generating random permutations  or picking random subsets.
    /// 
    /// Random permutations: Create an array of integers from zero to N-1 that are ordered randomly.
    /// Random subsets: Choose K random values from the next N.
    /// </summary>
    public static class RandomPermutation
    {
        /// <summary>
        /// Random number generator used for generating random permutations.
        /// </summary>
		private static readonly FastRandom RandomNumbers = new FastRandom(DateTime.Now.Millisecond);

		//private static readonly System.Random RandomNumbers = new System.Random(DateTime.Now.Millisecond);

		/// <summary>
		/// Create a random permutation array, consisting of one each of the numbers 0..count-1 in random order.
		/// 
		/// This uses the unbiased Knuth-Fischer-Yates shuffle.
		/// </summary>
		/// <param name="count">Number of integers to shuffle.
		/// This is one higher than the largest integer.</param>
		/// <returns>A randomized permutation array.</returns>
		public static int[] CreateRandomPermutation(this int count)
        {
            var permutation = new int[count];
            for (var d = 0; d < count; d++)
                permutation[d] = d;
            var n = count;
            while (n > 0)
            {
                var k = RandomNumbers.Next(n);   // Use rand(n) rather than rand(count) or the shuffle is not perfectly random!
                n--;
                var temp = permutation[k];
                permutation[k] = permutation[n];
                permutation[n] = temp;
            }
            return permutation;
        }

        /// <summary>
        /// Randomly shuffle an array of any type of item in-place, using the unbiased Knuth-Fischer-Yates shuffle..
        /// </summary>
        /// <typeparam name="T">Type of object to shuffle.</typeparam>
        /// <param name="items">Items to be shuffled.</param>
        /// <returns>Same array as input, with items shuffled in-place.</returns>
        public static T[] Shuffle<T>(this T[] items)
        {
            var n = items.Length;
            while (n > 0)
            {
                var k = RandomNumbers.Next(n);   // Use rand(n) rather than rand(count) or the shuffle is not perfectly random!
                n--;
                var temp = items[k];
                items[k] = items[n];
                items[n] = temp;
            }
            return items;
        }

        /// <summary>
        /// Shuffle some or all of the dimensions.
        /// </summary>
        /// <returns>A new permutation array with some or all dimensions shuffled.</returns>
        /// <param name="startingPermutation">Starting permutation.</param>
        /// <param name="dimensionsToShuffle">Number of dimensions to shuffle.
        /// The swaps are independent, so it is likely that some dimensions may be swapped more than once,
        /// and the number of unique dimensions shuffled may be less than this count.
        /// </param>
        public static int[] PartialShuffle(this int[] startingPermutation, int dimensionsToShuffle)
		{
			var dimensions = startingPermutation.Length;
			var newPermutation = (int[])startingPermutation.Clone();
			if (dimensions < 2) return newPermutation; 
			if (dimensionsToShuffle >= startingPermutation.Length)
				return CreateRandomPermutation(dimensionsToShuffle);
			else {
				var swapsToPerform = (dimensionsToShuffle + 1)/ 2;
				var swapsPerformed = 0;
				while (swapsPerformed < swapsToPerform)
				{
					var dim1 = RandomNumbers.Next(dimensions);
					var dim2 = RandomNumbers.Next(dimensions);
					if (dim1 != dim2)
					{
						var temp = newPermutation[dim1];
						newPermutation[dim1] = newPermutation[dim2];
						newPermutation[dim2] = temp;
						swapsPerformed++;
					}
				}
				return newPermutation;
			}
		}

        /// <summary>
        /// Lazily generate all N! possible permutations of the numbers zero through count-1.
        /// Each number occurs exactly once in the returned array.
        /// </summary>
        /// <param name="count">Number of items in the array, from zero to count-1.</param>
        /// <returns>An array of length count containing all numbers from zero to count-1.</returns>
        public static IEnumerable<int[]> CreateAllPermutations(this int count)
        {
            if (count == 1)
            {
                yield return new[] {0};
            }
            else
            {
                var unused = new int[count];
                var nFactorial = count.Factorial();
                for (var iPermutation = 0L; iPermutation < nFactorial; iPermutation++)
                {
                    var permutation = new int[count];

                    for (var position = 0; position < count; position++)
                        unused[position] = position;
                    var remainder = iPermutation;
                    for (var position = 0; position < count; position++)
                    {
                        var positionFactorial = (count - position - 1).Factorial();
                        var pick = remainder / positionFactorial;
                        remainder = remainder % positionFactorial;
                        permutation[position] = unused.Where(i => i >= 0).ElementAt((int)pick);
                        unused[permutation[position]] = -1;
                    }
                    yield return permutation;
                }
            }
        }

        public static long Factorial(this int n)
        {
            var nFactorial = 1L;
            for (var i = 2; i <= n; i++) nFactorial *= i;
            return nFactorial;
        }


        /// <summary>
        /// Iterate through all values from 0 to count-1 in random order, without replacement.
        /// </summary>
        /// <param name="count">Number of values to return.</param>
        /// <returns>Enumerator over the integers.</returns>
        public static IEnumerable<int> Permutations(this int count)
        {
            var permutations = count.CreateRandomPermutation();
            return permutations;
        }

        /// <summary>
        /// Takes k elements from the next n elements at random, preserving their order.
        /// 
        /// If there are fewer than n elements in items, this may return fewer than k elements.
        /// </summary>
        /// <typeparam name="TElem">Type of element in the items collection.</typeparam>
        /// <param name="items">Items to be randomly selected.</param>
        /// <param name="k">Number of items to pick.</param>
        /// <param name="n">Total number of items to choose from.
        /// If the items collection contains more than this number, the extra members will be skipped.
        /// If the items collection contains fewer than this number, it is possible that fewer than k items will be returned.</param>
        /// <returns>Enumerable over the retained items.
        /// 
        /// See http://stackoverflow.com/questions/48087/select-a-random-n-elements-from-listt-in-c-sharp for the commentary.
        /// </returns>
        public static IEnumerable<TElem> TakeRandom<TElem>(this IEnumerable<TElem> items, int k, int n)
        {
            var r = new FastRandom();
            var itemsList = items as IList<TElem>;

            if (k >= n || (itemsList != null && k >= itemsList.Count))
                foreach (var item in items) yield return item;
            else
            {  
                // If we have a list, we can infer more information and choose a better algorithm.
                // When using an IList, this is about 7 times faster (on one benchmark)!
                if (itemsList != null && k < n/2)
                {
                    // Since we have a List, we can use an algorithm suitable for Lists.
                    // If there are fewer than n elements, reduce n.
                    n = Math.Min(n, itemsList.Count);

                    // This algorithm picks K index-values randomly and directly chooses those items to be selected.
                    // If k is more than half of n, then we will spend a fair amount of time thrashing, picking
                    // indices that we have already picked and having to try again.   
                    var invertSet = k >= n/2;
                    var positions = invertSet ? new HashSet<int>() : (ISet<int>)new SortedSet<int>();

                    var numbersNeeded = invertSet ? n - k : k;
                    while (numbersNeeded > 0)
                        if (positions.Add(r.Next(0, n))) numbersNeeded--;

                    if (invertSet)
                    {
                        // positions contains all the indices of elements to Skip.
                        for (var itemIndex = 0; itemIndex < n; itemIndex++)
                        {
                            if (!positions.Contains(itemIndex))
                                yield return itemsList[itemIndex];
                        }
                    }
                    else
                    {
                        // positions contains all the indices of elements to Take.
                        foreach (var itemIndex in positions)
                            yield return itemsList[itemIndex];              
                    }
                }
                else
                {
                    // Since we do not have a list, we will use an online algorithm.
                    // This permits is to skip the rest as soon as we have enough items.
                    var found = 0;
                    var scanned = 0;
                    foreach (var item in items)
                    {
                        var rand = r.Next(0,n-scanned);
                        if (rand < k - found)
                        {
                            yield return item;
                            found++;
                        }
                        scanned++;
                        if (found >= k || scanned >= n)
                            break;
                    }
                }
            }  
        } 
    }
}
