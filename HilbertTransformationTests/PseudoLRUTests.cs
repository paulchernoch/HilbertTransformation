using Clustering.Cache;
using HilbertTransformationTests.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class PseudoLRUTests
    {

        [Test]
        public void NewCacheIsEmpty()
        {
            var capacity = 100;
            var cache = new PseudoLRUCache<string>(capacity);
            Assert.IsTrue(cache.IsEmpty, "Cache does not know it is empty");
        }

        [Test]
        public void SizeIncrementsIfYouAddItemsToCacheThatIsNotFull()
        {
            var capacity = 100;
            var cache = new PseudoLRUCache<string>(capacity);
            Assert.AreEqual(0, cache.Size);
            cache.Add("item1");
            Assert.AreEqual(1, cache.Size);
            cache.Add("item2");
            Assert.AreEqual(2, cache.Size);
        }

        [Test]
        public void NoItemsEvictedWhenAddingToCacheThatsNotNotFull()
        {
            var capacity = 100;
            var cache = new PseudoLRUCache<string>(capacity);
            var items = new List<PseudoLRUCache<string>.CacheItem>();
            for (var i = 1; i <= capacity; i++)
            {
                items.Add(cache.Add(i.ToString()));
                var notCachedCount = items.Count(item => !item.IsCached);
                Assert.IsTrue(notCachedCount == 0, $"{notCachedCount} items are not cached after adding {i}.");
            }
        }

        [Test]
        public void OneItemEvictedWhenAddingToFullCache()
        {
            var capacity = 100;
            var cache = new PseudoLRUCache<string>(capacity);
            var items = new List<PseudoLRUCache<string>.CacheItem>();
            for (var i = 1; i <= capacity + 1; i++)
            {
                items.Add(cache.Add(i.ToString()));
                var notCachedCount = items.Count(item => !item.IsCached);
                if (i <= capacity)
                {
                    Assert.IsFalse(cache.IsEmpty, $"Cache thinks it is empty when it is not, for i = {i}.");
                    Assert.AreEqual(i >= capacity, cache.IsFull, $"Cache IsFull = {cache.IsFull} for i = {i}.");
                    Assert.IsTrue(notCachedCount == 0, $"{notCachedCount} items are not cached after adding {i} when cache is NOT FULL.");
                }
                else
                {
                    Assert.IsTrue(cache.IsFull, $"Cache thinks it is not full when i = {i}.");
                    Assert.IsTrue(notCachedCount == 1, $"{notCachedCount} items are not cached after adding {i} when cache IS FULL.");
                }
            }
        }

        /// <summary>
        /// Test the Hit ratio when a small amount of data is accessed according to a Zipf distribution.
        /// 
        /// Cache Hit ratio is dependent upon the Alpha value of the Zipf distribution
        /// and the cache size.
        /// Lower Alpha means poorer Hit ratio. A ratio of 0.15 is advertised as a good ratio 
        /// for a cache of 5% by one paper I read, but that is for unlimited N.
        /// For finite N, the hit rate is much better. We will expect 0.85 (85%).
        /// </summary>
        [Test]
        public void CacheHitRatioIsGoodForFewItems()
        {
            CacheHitRatioCase(10000, 5, 0.8, 0.85, 1000000);
        }

        /// <summary>
        /// Test the Hit Ratio when there are many more items (one million, instead of 10000)
        /// and the Zipf alpha is lower (which adversely affects hit rates in real world data).
        /// One paper said that for a homogenous user community (like a university or corporation)
        /// the Zipf distribution alpha coefficient is higher (typically 0.8). 
        /// For the general populace, the alpha is closer ot 0.7.
        /// This test uses an alpha of 0.75.
        /// </summary>
        [Test]
        public void CacheHitRatioIsGoodForManyItems()
        {
            CacheHitRatioCase(1000000, 5, 0.75, 0.70, 1000000);
        }

        public void CacheHitRatioCase(int n, double cacheSizePercentage, double alpha, double expectedHitRate, int maxTrials)
        {
            var zipf = new ZipfDistribution(n, alpha, 10, 0.0001);
            var capacity = (int)(n * cacheSizePercentage / 100);
            var cache = new PseudoLRUCache<string>(capacity);
            var allItems = new PseudoLRUCache<string>.CacheItem[n + 1];
            var rankHistogram = new int[n + 1];
            for (var trial = 0; trial < maxTrials; trial++)
            {
                var rank = zipf.NextRandomRank();
                if (allItems[rank] == null)
                    allItems[rank] = cache.Add(rank.ToString());
                else
                    allItems[rank].GetOrCreate(() => rank.ToString());
                rankHistogram[rank]++;
            }
            Console.WriteLine($"The Hit Ratio is {cache.HitRatio}; hoped for {expectedHitRate}");
            var histogram = "Rank Histogram for first 100 items\n";
            var cumePct = 0.0;
            for (var rank = 1; rank <= 100; rank++)
            {
                var pct = 100.0 * rankHistogram[rank] / (double)maxTrials;
                cumePct += pct;
                histogram += $"{rank} chosen {rankHistogram[rank]} times ({pct}%, {cumePct}% cume)\n";
            }
            Console.WriteLine(histogram);
            Assert.GreaterOrEqual(cache.HitRatio, expectedHitRate, $"Cache Hit Ratio is lower than expected: {cache.HitRatio} < {expectedHitRate}");
        }
    }
}
