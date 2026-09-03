using deeplynx.datalayer.Models;

namespace deeplynx.interfaces;

public interface ICacheBusiness
{
    string CacheType { get; }
    Task<T?> GetAsync<T>(string key);
    Task<bool> SetAsync(string key, object value, TimeSpan? ttl = null);
    Task<bool> SetAsync(string key, object value, int? ttl = null);
    Task<bool> DeleteAsync(string key);
    Task<bool> DeleteByPrefixAsync(string prefix);
    Task<bool> FlushAsync();
}
