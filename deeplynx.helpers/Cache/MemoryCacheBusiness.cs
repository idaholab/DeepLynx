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

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheBusiness"/> class.
        /// </summary>
        public MemoryCacheBusiness()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
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
                await SetAsync("type", "memory", (TimeSpan?)null);
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
            var cacheEntryOptions = new MemoryCacheEntryOptions();
            _keys[key] = true;

            if (ttl.HasValue)
            {
                cacheEntryOptions.SetAbsoluteExpiration(ttl.Value);
            }

            var serializedValue = JsonConvert.SerializeObject(value);
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
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
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