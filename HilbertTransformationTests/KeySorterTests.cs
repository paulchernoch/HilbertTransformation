using System;
using Clustering;
using HilbertTransformation.Random;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class KeySorterTests
	{
		[Test]
		public void DenseKeys()
		{
			var sorted = 100.CreateRandomPermutation();
			var unsorted = (new Permutation<int>(100)).Mapping;
			var sorter = new KeySorter<int,int>((int i) => i, (int i) => i);
			var sameSorted = sorter.Sort(unsorted, sorted);
			CollectionAssert.AreEqual(sorted, sameSorted, "Failed to sort dense keys");
		}

		[Test]
		public void SparseKeys()
		{
			var sorted = 100.CreateRandomPermutation();
			var unsorted = (new Permutation<int>(100)).Mapping;
			var sorter = new KeySorter<int, int>((int i) => i * 5 + 100, (int i) => i * 5 + 100);
			var sameSorted = sorter.Sort(unsorted, sorted);
			CollectionAssert.AreEqual(sorted, sameSorted, "Failed to sort dense keys");
		}
	}
}
