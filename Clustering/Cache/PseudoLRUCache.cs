using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clustering.Cache
{
    /// <summary>
    /// Cache items with a policy of evicting the approximately least recently used objects.
    /// 
    /// A Monte-Carlo method is employed to select as well as possible the least recently used objects.
    /// Objects in this cache are not part of a graph, unlike some styles of cache, thus 
    /// they may not be looked up by id. A parent object will hold onto a CacheItem, which references the
    /// cached object and may be evicted, disposed and recreated as necessary.
    /// 
    /// See https://pdfs.semanticscholar.org/d8ca/5822e1d6bf9ffebc6d7d021e7d90da996439.pdf for a graph
    /// of the hit-ratio for various cache replacement algorithms as a function of the cache size to data size ratio.
    /// While LFU (least frequently used) is better for larger caches, in the range from 1% to 12%, 
    /// LRU is better than LFU, LRV (least relative value), and SIZE (GreedyDual-Size). 
    /// By this measure, LRU at 5% cache size should deliver a hit ratio of 0.125.
    /// 
    /// </summary>
    /// <typeparam name="TItem">Type of item to cache.</typeparam>
    public class PseudoLRUCache<TItem> where TItem : class
    {

        /*
            Synchronization strategy:
            
            Synchronization occurs at two levels: the PseudoLRUCache and the CacheItem.
            An attempt is made to lock at the item level when possible to reduce contention over locks against the cache.
         */

        #region CacheItem factory support

        /// <summary>
        /// Permits us to keep any but PseudoLRUCache from calling the CacheItem constructor. 
        /// </summary>
        private static Func<PseudoLRUCache<TItem>, TItem, CacheItem> _privateCacheItemFactory;

        static PseudoLRUCache()
        {
            // Calling this forces the CacheItem static initializer to be called, 
            // as per Martin Fay's comment at https://stackoverflow.com/questions/2736827/visibility-of-nested-class-constructor
            CacheItem.Initialize();
        }

        #endregion

        public class CacheItem: IComparable<CacheItem>
        {
            #region CacheItem factory support

            /// <summary>
            /// Calling this will force the static CacheItem initializer run.
            /// </summary>
            internal static void Initialize() { }

            static CacheItem()
            {
                _privateCacheItemFactory = (cache, item) => new CacheItem(cache, item);
            }

            #endregion

            /// <summary>
            /// Used to lock at the item level, instead of the cache level
            /// </summary>
            private readonly object _ItemLocker = new object();

            private PseudoLRUCache<TItem> Cache { get; set; }

            public int LastAccess { get; private set; }

            private CacheItem(PseudoLRUCache<TItem> cache, TItem item)
            {
                Cache = cache;
                LastAccess = int.MinValue;
                Item = item;
            }

            #region The Item that is being cached (or null, if item has been ejected).

            private TItem _Item;

            /// <summary>
            /// Item stored in cache, or null if it has either never been loaded or has been ejected from the cache.
            /// 
            /// Getting the cached item will refresh its LastAccess counter IF the item is in the cache (is not null).
            /// 
            /// Setting the Item to a value other than null will cause it to be added to the cache.
            /// 
            /// Setting the Item to null will not cause the CacheItem to be removed from the cache until 
            /// the Cache stumbles upon it while looking for an item to eject. This is because
            /// searching for the item's position in the cache is too time consuming; it is not indexed.
            /// </summary>
            public TItem Item
            {
                get {
                    lock (_ItemLocker)
                    {
                        if (_Item != null)
                            LastAccess = Cache.NextAccessCount;
                        return _Item;
                    }
                }
                set
                {
                    if (value == null)
                    {
                        lock (_ItemLocker)
                        {
                            LastAccess = int.MinValue;
                            _Item = value;
                        }
                    }
                    else if (value != _Item)
                    {
                        lock (Cache._Locker)
                        {
                            LastAccess = Cache.NextAccessCount;
                            if (value != _Item)
                                Cache.Add(this);
                        }
                        _Item = value;
                    }
                }
            }

            #endregion

            /// <summary>
            /// Get the cached item, but if it is null, create it first by calling the supplied delegate.
            /// </summary>
            /// <param name="creator">Delegate that will create the cached item if it is missing (null).</param>
            /// <returns>Either the already cached item, or a newly created one.</returns>
            public TItem GetOrCreate(Func<TItem> creator)
            {
                lock (_ItemLocker)
                {
                    // Guarantee that either Item-get or Item-set is called once, but not both, 
                    // because we only want "LastAccess = Cache.NextAccessCount" performed once.
                    if (_Item != null)
                        return Item;
                }
                // Calling the delegate outside the lock presents a narrow chance that another thread also created 
                // a new item for this CacheItem at the same time. However, since creating a new item can be time consuming,
                // we do not want to perform it inside a lock. The chance of lock contention is higher than the
                // risk of duplicating the creation.
                var newItem = creator();
                return Item = newItem;
            }

            /// <summary>
            /// Sort items in descending order by LastAccess, so that the oldest item is sorted into last position.
            /// </summary>
            /// <param name="other">Other item to compare this item to.</param>
            /// <returns>-1 if this CacheItem was last accessed more recently than the other item, i.e. has a larger value for LastAccess.
            /// 0 if both items are of equal age.
            /// 1 if this CacheItem was last accessed before the other item.</returns>
            public int CompareTo(CacheItem other)
            {
                return -LastAccess.CompareTo(other.LastAccess);
            }

            public bool IsOlderThan(CacheItem otherItem)
            {
                return LastAccess < otherItem.LastAccess;
            }
        }

        private readonly object _Locker = new object();

        /// <summary>
        /// Auto-incrementing counter that acts as the clock for the cache. 
        /// The lower the value of the Counter assigned to a CacheItem, the farther
        /// back in time the object was last accessed.
        /// </summary>
        private int _AccessCounter = 0;

        #region Item storage related properties

        private readonly int CandidateSize = 16;

        /// <summary>
        /// Maximum number of items that may be stored in the cache.
        /// </summary>
        public int Capacity => Storage.Length;

        /// <summary>
        /// Capacity reduced by the number of eviction candidates, which do not participate in the ring buffer.
        /// </summary>
        private int RingCapacity => Capacity - CandidateSize;

        /// <summary>
        /// Number of items immediately behind the head of the ring buffer to exclude from being tested as eviction candidates.
        /// This prevents recently created items from being accidentally evicted.
        /// </summary>
        private int RecentlyCreatedItemCount => RingCapacity / 3;

        /// <summary>
        /// Holds the ring buffer plus the eviction candidates.
        /// 
        /// The last "CandidateSize" worth of slots in Storage holds the eviction candidates, older items that
        /// may be suitable for eviction. The ring-buffer skips over these when it cycles. 
        /// Storage[Capacity - 1] likely holds the next item to be evicted, but a random selection of other items
        /// in the main part of the storage may yield an even older item to be evicted in its place. 
        /// </summary>
        private CacheItem[] Storage { get; set; }

        /// <summary>
        /// Number of items in the cache, which may be less than Capacity, but not more.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Zero-based index into the Storage array where the next item to be added will go.
        /// </summary>
        private int AddPosition { get; set; }

        public bool IsFull => Size == Capacity;


        public bool IsEmpty => Size == 0;

        /// <summary>
        /// Convert a logical cache position from zero to Size - 1 to its index in the Storage array, accounting for the
        /// nature of the ring buffer and the Candidates subarray.
        /// 
        /// If the ring buffer has not yet ever wrapped around, index = Capacity - position - 1, which means it runs 
        /// exactly in reverse order.
        /// </summary>
        /// <param name="position">Logical position in the cache, from zero to Size - 1.</param>
        /// <returns>Physical position in the Storage array, from zero to Size - 1.</returns>
        public int PositionToIndex(int position)
        {
            if (position < CandidateSize)
                return Capacity - position - 1;
            if (position == Size)
                return AddPosition;
            position -= CandidateSize;
            
            var ringSize = Size - CandidateSize;
            return (AddPosition + ringSize - position) % RingCapacity;
        }

        #endregion

        /// <summary>
        /// When randomly searching for an approximate least recently used item, try this many new randomly chosen items in addition to the known eviction Candidates. 
        /// </summary>
        public int RandomSearchSize { get; set; } = 10;

        private FastRandom RandomNumbers { get; } = new FastRandom();

        /// <summary>
        /// Increment and return the access counter.
        /// A recently accessed object will have a higher count than an object accessed father back in time.
        /// </summary>
        public int NextAccessCount => Interlocked.Increment(ref _AccessCounter);

        public PseudoLRUCache(int capacity)
        {
            Init(capacity);
        }

        private void Init(int capacity)
        {
            Storage = new CacheItem[capacity];
            // We will fill in the cache starting at the end of the array and working towards the start.
            AddPosition = capacity - 1;
            Size = 0;
        }

        /// <summary>
        /// Add an item to the cache and return a holder for it.
        /// </summary>
        /// <param name="item">Item to add to the cache, which may be null.</param>
        /// <returns>A holder for the item.</returns>
        public CacheItem Add(TItem item)
        {
            return _privateCacheItemFactory(this, item);
        }

        /// <summary>
        /// Add the item holder to the cache. 
        /// If the cache is already full, Evict an item first.
        /// </summary>
        /// <param name="cacheItem">Item to add.</param>
        private void Add(CacheItem cacheItem)
        {
            lock(_Locker)
            {
                if (IsFull)
                {
                    // Find item to evict, overwrite it with the item at the AddPosition, and then put the added item at the AddPosition.
                    // Lastly, decrement the AddPosition.
                    Evict();
                }
                Storage[AddPosition--] = cacheItem;
                if (AddPosition < 0)
                    AddPosition = RingCapacity - 1;  // Ring buffer wrap-around
            }
        }

        private void SortCandidatesPartially()
        {
            Storage.LowHigh(Capacity - CandidateSize, CandidateSize);
        }

        /// <summary>
        /// Find the next item to evict and swap it into position at the end of the Storage array.
        /// 
        /// Ideally, that item would be the oldest item (the one least recently used), but instead several
        /// randomly selected items will be compared and the oldest among them will be selected for eviction.
        /// 
        /// Several items may be moved to new locations in Storage as a result of this call.
        /// </summary>
        private void FindItemToEvict()
        {
            // Assumes that the cache is full.

            // 1. Partially sort the Eviction candidates, such that the oldest and newest items are in place at the end and beginning respectively
            //    of the candidates portion of the Storage array (the end of the Storage array).
            SortCandidatesPartially();

            // 2. Identify the beginning and end of the range of cache positions that have potentially old items.
            //    These are logical positions, not array indices. These position skip the candidates at the end of the Storage array as well
            //    as the third of items near the head of the circular buffer - the newly created items.
            var lowestRandomPosition = CandidateSize;
            var highestRandomPosition = Capacity - RecentlyCreatedItemCount;
            var iYoungest = Capacity - CandidateSize;

            // 3. Choose several CacheItems randomly and see if any are older than the youngest of the Eviction candidates.
            //    Those that are, swap with the Eviction candidates.
            for (var i = 0; i < RandomSearchSize; i++)
            {
                var youngestCandidate = Storage[iYoungest];
                var randomPosition = RandomNumbers.Next(lowestRandomPosition, highestRandomPosition);

                // 4. Convert the random position into a random physical index into the Storage array.
                var randomIndex = PositionToIndex(randomPosition);
                var item = Storage[randomIndex];

                // 5. If the randomly chosen item is older than the youngest item in the Candidate section of Storage, 
                //    make it a candidate and kick the youngest Candidate back into the regular part of Storage.
                if (item.IsOlderThan(youngestCandidate))
                {
                    Storage[iYoungest] = item;
                    Storage[randomIndex] = youngestCandidate;
                    SortCandidatesPartially();
                }
            }

            // At this point, Storage[Capacity - 1] should hold the approximately oldest item.
        }



        /// <summary>
        /// If the cache is full, evict an item, otherwise do not evict it.
        /// </summary>
        /// <returns>The evicted item, or null if the cache is not at Capacity.</returns>
        private TItem Evict()
        {
            // Assume the caller has taken out the necessary concurrency locks.
            if (!IsFull)
                return null;
            FindItemToEvict();
            var evictedCacheItem = Storage[Capacity - 1];
            Storage[Capacity - 1] = Storage[AddPosition];
            Storage[AddPosition] = null;
            Size--;
            var evictedItem = evictedCacheItem.Item;
            evictedCacheItem.Item = null;
            return evictedItem;
        }

        /// <summary>
        /// Remove all items from the Cache.
        /// </summary>
        public void Clear()
        {
            lock (_Locker)
            {
                Init(Capacity);
            }
        }
    }
}
