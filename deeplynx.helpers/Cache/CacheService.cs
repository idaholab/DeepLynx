using deeplynx.business;
using StackExchange.Redis;
using deeplynx.interfaces;
using deeplynx.helpers;

namespace deeplynx.helpers
{
    public class CacheService
    {
        private static ICacheBusiness _instance;
        
        static CacheService() 
        {
            _instance = CreateCache();
        }
        
        public static ICacheBusiness Instance => _instance;

        /// <summary>
        /// Resets the cache service instance. Used primarily for testing to switch between cache providers.
        /// </summary>
        public static void ResetCacheService()
        {
            _instance = CreateCache();
        }
        
        /// <summary>
        /// Used to determine what cache service to use by the Config.CACHE_PROVIDER_TYPE variable
        /// </summary>
        /// <returns>The Cache Business Object</returns>
        /// <exception cref="Exception">Returned if CACHE_PROVIDER_TYPE = redis but no REDIS_CONNECTION_STRING is provided</exception>
        public static ICacheBusiness CreateCache()
        {
            var cacheType = Environment.GetEnvironmentVariable("CACHE_PROVIDER_TYPE");
            var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            switch (cacheType)
            {
                case "memory":
                    return new MemoryCacheBusiness();
                case "redis":
                    if (!string.IsNullOrEmpty(redisConnectionString))
                    {
                        var options = ConfigurationOptions.Parse(redisConnectionString);
                        options.AllowAdmin = true;
                        var connectionMultiplexer = ConnectionMultiplexer.Connect(options);
                        return new RedisCacheBusiness(connectionMultiplexer);
                    }
                    else
                    {
                        throw new Exception("Redis connection string not found in environment variables.");
                    }
                default:
                    return new MemoryCacheBusiness();
            }
        }
    }
}