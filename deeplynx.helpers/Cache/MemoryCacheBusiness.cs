using System.Collections.Concurrent;
using Newtonsoft.Json;
using deeplynx.interfaces;
using Microsoft.Extensions.Caching.Memory;
using deeplynx.helpers;

namespace deeplynx.business
{
    public class MemoryCacheBusiness : ICacheBusiness
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, bool> _keys;

        // Hardcoded for now rather than env-configurable. Sized against the 2Gi container
        // memory limit set in sandbox, dev, and presumably acceptance and prod. ~96MB is
        // roughly 4.7% of that ceiling, leaving room for the .NET runtime, EF Core, Kestrel, and GC. 
        // Possibly make this an env var in the future.
        private const long CacheSizeLimitBytes = 96 * 1024 * 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheBusiness"/> class.
        /// </summary>
        public MemoryCacheBusiness()
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = CacheSizeLimitBytes,
                // On hitting the size limit, evict entries (LRU/priority-based) until
                // 10% of capacity is freed, then insert the new entry.
                CompactionPercentage = 0.10
            });
            _keys = new ConcurrentDictionary<string, bool>();
        }

        /// <summary>
        /// Static property that will return the cache type in use.
        /// </summary>
        public string CacheType => "memory";

        /// <summary>
        /// Retrieves serialized cached data matching the provided key
        /// </summary>
        /// <param name="key">The key of cached data</param>
        /// <returns>The matching Cached data</returns>
        public async Task<T?> GetAsync<T>(string key)
        {
            var value = _cache.Get<string>(key);

            if (value == null)
                return await Task.FromResult<T?>(default);

            try
            {
                var parsed = JsonConvert.DeserializeObject<T>(value);
                return await Task.FromResult(parsed);
            }
            catch
            {
                // Attempt to deserialize as a list of the specified type
                try
                {
                    var parsedList = JsonConvert.DeserializeObject<List<T>>(value);
                    return await Task.FromResult((T)(object)parsedList);
                }
                catch
                {
                    return await Task.FromResult((T)(object)value);
                }
            }
        }

        /// <summary>
        /// Operation to Set cache data with key value pair
        /// </summary>
        /// <param name="key">The Key name of the data to be cached</param>
        /// <param name="value">The value of the data to be cached</param>
        /// <param name="ttl">Time To Live(ttl)- The duration of time the data will be cached</param>
        /// <returns>bool based on set success</returns>
        public Task<bool> SetAsync(string key, object value, TimeSpan? ttl = null)
        {
            var serializedValue = JsonConvert.SerializeObject(value);
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                // Required now that the cache has a SizeLimit — every entry must declare
                // a size. Using serialized string length (chars) as a cheap proxy for cost;
                // it undercounts true heap usage (object overhead, dictionary nodes, etc.)
                // but is directionally correct and consistent across entries.
                Size = serializedValue.Length
            };

            // Keep _keys in sync no matter how an entry leaves the cache - explicit removal,
            // TTL expiry, or SizeLimit-triggered compaction eviction - so DeleteByPrefixAsync
            // never operates on stale/already-evicted keys.
            cacheEntryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                _keys.TryRemove(evictedKey.ToString()!, out _);
            });

            _keys[key] = true;

            if (ttl.HasValue)
            {
                cacheEntryOptions.SetAbsoluteExpiration(ttl.Value);
            }
            
            _cache.Set(key, serializedValue, cacheEntryOptions);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Operation to Set cache data with key value pair
        /// </summary>
        /// <param name="key">The Key name of the data to be cached</param>
        /// <param name="value">The value of the data to be cached</param>
        /// <param name="ttl">Time To Live(ttl)- The duration of time the data will be cached</param>
        /// <returns>bool based on set success</returns>
        public Task<bool> SetAsync(string key, object value, int? ttl = null)
        {
            // Memory Cache only takes timespan ttl's not int. Will convert to timespan if int is provided
            return SetAsync(key, value, ttl.HasValue ? TimeSpan.FromSeconds(ttl.Value) : null);
        }

        /// <summary>
        /// Operation to Delete cache data by matching key
        /// </summary>
        /// <param name="key">The Key name of the data to be cached</param>
        /// <returns>bool based on delete success</returns>
        public Task<bool> DeleteAsync(string key)
        {
            // Removal triggers the post-eviction callback above, which removes the key
            // from _keys - no need to touch _keys directly here.
            _cache.Remove(key);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Deletes all cache entries whose keys begin with the provided prefix.
        /// </summary>
        /// <param name="prefix">The key prefix to match.</param>
        /// <returns>bool based on prefix delete success</returns>
        public Task<bool> DeleteByPrefixAsync(string prefix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

            var matchingKeys = _keys.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();

            foreach (var key in matchingKeys)
            {
                _cache.Remove(key);
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Operation to flush all existing data
        /// </summary>
        /// <returns>bool based on flush success</returns>
        public Task<bool> FlushAsync()
        {
            foreach (var key in _keys.Keys) _cache.Remove(key);

            _keys.Clear();
            return Task.FromResult(true);
        }
    }
}