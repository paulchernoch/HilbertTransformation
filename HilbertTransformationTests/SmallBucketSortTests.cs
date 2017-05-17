using Clustering;
using HilbertTransformation.Random;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HilbertTransformationTests
{
    [TestFixture]
    
    public class SmallBucketSortTests
    {
        [Test]
        public void SortNumbers()
        {
            var size = 10000;
            var expectedSortedNumbers = Enumerable.Range(0, size).ToList();
            var unsortedNumbers = size.Permutations().ToList();
            var sorter = new SmallBucketSort<int>(unsortedNumbers, n => n);
            var actualSortedNumbers = sorter.Sort();
            CollectionAssert.AreEqual(expectedSortedNumbers, actualSortedNumbers, "Sorting failed");
        }
    }
}
