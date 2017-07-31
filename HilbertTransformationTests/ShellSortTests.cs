using HilbertTransformation.Cache;
using HilbertTransformation.Random;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace HilbertTransformationTests
{
    [TestFixture]
    public class ShellSortTests
    {
        [Test]
        public void ShellSortSortsArrayCorrectly()
        {
            var numbers = 100.CreateRandomPermutation();
            var expected = Enumerable.Range(0, 100).ToArray();
            var actual = numbers.ShellSort();

            CollectionAssert.AreEqual(expected, actual, "ShellSort did not sort array correctly.");
        }

        [Test]
        public void ShellSortSortsRandomShortArraysSlowly()
        {
            var unsorted = new List<int[]>();
            for (var i = 10; i < 100; i++)
            {
                unsorted.Add(i.CreateRandomPermutation());
            }
            ShellSortCase(unsorted, 100, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;

            Assert.IsTrue(ratio > 2, $"ShellSort unexpectedly fast, with Quicksort taking { qsTime } msec and ShellSort taking { ssTime } msec");
        }

        /// <summary>
        /// Sort short arrays whose distribution resembles the use case needed for the Pseudo-LRU Cache.
        /// 
        /// The arrays have 26 elements. All but one of the first 16 elements are sorted.
        /// The remaining 10 elements are randomly ordered and all but one are larger than the largest
        /// value in the first sixteen elements. 
        /// A perfect sorter would fix the one element out of place in the lower half
        /// and sort the upper half.
        /// </summary>
        [Test]
        public void ShellSortSortsPartiallySortedShortArraysFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 1000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                numbers.AddRange(10.CreateRandomPermutation().Select(n => n + 16));
                var lowIndex = r.Next(16);
                var highIndex = r.Next(16, 26);
                var temp = numbers[lowIndex];
                numbers[lowIndex] = numbers[highIndex];
                numbers[highIndex] = temp;
                unsorted.Add(numbers.ToArray());
            }
            ShellSortCase(unsorted, 100, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;

            Assert.IsTrue(ratio < 1, $"ShellSort should have been faster on partially sorted arrays than quicksort, yet Quicksort took { qsTime } msec and ShellSort took { ssTime } msec");
        }

        /// <summary>
        /// Create many sixteen element lists and randomly swap the last item with another item, making many almost sorted arrays.
        /// </summary>
        [Test]
        public void ShellSortSortsAlmostSortedShortArraysFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 1000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                var iSwap = r.Next(16);
                numbers[15] = numbers[iSwap];
                numbers[iSwap] = 16;
                unsorted.Add(numbers.ToArray());
            }
            ShellSortCase(unsorted, 100, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;

            Assert.IsTrue(ratio < 1, $"ShellSort should have been faster on almost sorted arrays than quicksort, yet Quicksort took { qsTime } msec and ShellSort took { ssTime } msec");
        }

        void ShellSortCase(List<int[]> unsorted, int repeats, out long qsTime, out long ssTime)
        {
            ArraySortCase(unsorted, repeats, (a) => a.ShellSort(), out qsTime, out ssTime);
        }

        [Test]
        public void InsertionSortSortsArrayCorrectly()
        {
            var numbers = 100.CreateRandomPermutation();
            var expected = Enumerable.Range(0, 100).ToArray();
            var actual = numbers.InsertionSort();

            CollectionAssert.AreEqual(expected, actual, "InsertionSort did not sort array correctly.");
        }

        /// <summary>
        /// Create many sixteen element lists and randomly swap the last item with another item, making many almost sorted arrays.
        /// </summary>
        [Test]
        public void InsertionSortSortsAlmostSortedShortArraysFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 10000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                var iSwap = r.Next(16);
                numbers[15] = numbers[iSwap];
                numbers[iSwap] = 16;
                unsorted.Add(numbers.ToArray());
            }
            InsertionSortCase(unsorted, 100, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;

            Assert.IsTrue(ratio < 1, $"InsertionSort should have been faster on almost sorted arrays than quicksort, yet Quicksort took { qsTime } msec and InsertionSort took { ssTime } msec");
        }

        void InsertionSortCase(List<int[]> unsorted, int repeats, out long qsTime, out long isTime)
        {
            ArraySortCase(unsorted, repeats, (a) => a.InsertionSort(), out qsTime, out isTime);
        }

        [Test]
        public void HighLowSortSortsArrayCorrectly()
        {
            var numbers = 100.CreateRandomPermutation();
            var expected = Enumerable.Range(0, 100).ToArray();
            var actual = numbers.HighLowSort();

            CollectionAssert.AreEqual(expected, actual, "HighLowSort did not sort array correctly.");
        }

        [Test]
        public void HighLowSemiSortSortsAlmostSortedShortArraysFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 10000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                var iSwap = r.Next(16);
                numbers[15] = numbers[iSwap];
                numbers[iSwap] = 16;
                unsorted.Add(numbers.ToArray());
            }
            HighLowSemiSortCase(unsorted, 100, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;

            Assert.IsTrue(ratio < 1, $"HighLowSort should have been faster on almost sorted arrays than quicksort, yet Quicksort took { qsTime } msec and HighLowSort took { ssTime } msec");
        }

        void HighLowSemiSortCase(List<int[]> unsorted, int repeats, out long qsTime, out long hlsTime)
        {
            ArraySortCase(unsorted, repeats, (a) => a.HighLowSort(1), out qsTime, out hlsTime);
        }

        [Test]
        public void LowHighSemiSortSortsAlmostSortedShortArraysFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 10000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                var iSwap = r.Next(16);
                numbers[15] = numbers[iSwap];
                numbers[iSwap] = 16;
                unsorted.Add(numbers.ToArray());
            }
            LowHighArraySemiSortCase(unsorted, 500, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;
            var message = $"Quicksort took { qsTime } msec and LowHigh took { ssTime } msec";
            System.Console.WriteLine(message);
            Assert.IsTrue(ratio < 1, $"LowHigh should have been faster on almost sorted arrays than quicksort, yet ${message}");
        }

        [Test]
        public void LowHighSemiSortSortsAlmostSortedShortIListsFast()
        {
            var r = new FastRandom();
            var unsorted = new List<int[]>();
            for (var i = 0; i < 10000; i++)
            {
                var numbers = Enumerable.Range(1, 16).ToList();
                var iSwap = r.Next(16);
                numbers[15] = numbers[iSwap];
                numbers[iSwap] = 16;
                unsorted.Add(numbers.ToArray());
            }
            LowHighListSemiSortCase(unsorted, 500, out long qsTime, out long ssTime);
            var ratio = (double)ssTime / qsTime;
            var message = $"Quicksort took { qsTime } msec and LowHigh took { ssTime } msec";
            System.Console.WriteLine(message);
            Assert.IsTrue(ratio < 1, $"LowHigh should have been faster on almost sorted arrays than quicksort, yet ${message}");
        }

        void LowHighListSemiSortCase(List<int[]> unsorted, int repeats, out long qsTime, out long lhsTime)
        {
            ListSortCase(unsorted, repeats, (a) => a.LowHigh(), out qsTime, out lhsTime);
        }

        void LowHighArraySemiSortCase(List<int[]> unsorted, int repeats, out long qsTime, out long lhsTime)
        {
            ArraySortCase(unsorted, repeats, (a) => a.LowHigh(), out qsTime, out lhsTime);
        }

        void ArraySortCase(List<int[]> unsorted, int repeats, Action<int[]> sortRoutine, out long qsTime, out long srTime)
        {
            var qsTimer = new Stopwatch();
            foreach (var trial in Enumerable.Range(0, repeats))
            {
                foreach (var a in unsorted)
                {
                    var aCopy = (int[])a.Clone();
                    qsTimer.Start();
                    Array.Sort(aCopy);
                    qsTimer.Stop();
                }
            }
            qsTime = qsTimer.ElapsedMilliseconds;

            var srTimer = new Stopwatch();
            foreach (var trial in Enumerable.Range(0, repeats))
            {
                foreach (var a in unsorted)
                {
                    var aCopy = (int[])a.Clone();
                    srTimer.Start();
                    sortRoutine(aCopy);
                    srTimer.Stop();
                }
            }
            srTime = srTimer.ElapsedMilliseconds;
        }

        void ListSortCase(List<int[]> unsorted, int repeats, Action<IList<int>> sortRoutine, out long qsTime, out long srTime)
        {
            var qsTimer = new Stopwatch();
            foreach (var trial in Enumerable.Range(0, repeats))
            {
                foreach (var a in unsorted)
                {
                    var aCopy = (int[])a.Clone();
                    qsTimer.Start();
                    Array.Sort(aCopy);
                    qsTimer.Stop();
                }
            }
            qsTime = qsTimer.ElapsedMilliseconds;

            var srTimer = new Stopwatch();
            foreach (var trial in Enumerable.Range(0, repeats))
            {
                foreach (var a in unsorted)
                {
                    var aCopy = (IList<int>)a.Clone();
                    srTimer.Start();
                    sortRoutine(aCopy);
                    srTimer.Stop();
                }
            }
            srTime = srTimer.ElapsedMilliseconds;
        }


    }
}
