using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HilbertTransformation.Random
{
    /// <summary>
    /// Each UniqueRandomSeed yields a unique set of random numbers, but repeated calls to Sequence
    /// will return iterators that start over at the first number in the sequence and repeat it exactly.
    /// </summary>
    public class UniqueSeedRandom
    {
        private static int _uniqueSeedCounter;

        /// <summary>
        /// For unit tests, so they can reset the counter.
        /// </summary>
        public static void Reset() => _uniqueSeedCounter = 0;

        private static int NextSeed() => Interlocked.Increment(ref _uniqueSeedCounter);

        /// <summary>
        /// Unique seed for random number generator.
        /// </summary>
        private int UniqueSeed { get; } = NextSeed();

        /// <summary>
        /// Generate a sequence of random values.
        /// 
        /// Every call to this method returns a new iterator that restarts the same random sequence.
        /// </summary>
        /// <param name="upperLimit">Random numbers in the sequence will fall between zero (inclusive) and upperLimit (exclusive).</param>
        /// <param name="limit">Iteration will cease after this many values have been yielded.</param>
        public IEnumerable<int> Sequence(int upperLimit, int limit = int.MaxValue) {
            var r = new FastRandom(UniqueSeed);
            for (var i = 0; i < limit; i++)
                yield return r.Next(upperLimit);
        }
    }
}
