namespace CommonGameLib.Services
{
    public interface ICachedService
    {
        Task<T?> GetAndSetAsync<T>(string key, T data, TimeSpan? time = null) where T : class;
        Task<T?> GetAndSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? time = null) where T : class;
        Task<T?> GetAsync<T>(string key) where T : class;
        Task<bool> SetAsync<T>(string key, T data, TimeSpan? time = null) where T : class;
        T? GetAndSet<T>(string key, Func<T> acquire, TimeSpan? time = null) where T : class;
        T? Get<T>(string key) where T : class;
        bool Set<T>(string key, T data, TimeSpan? time = null) where T : class;
        Task<bool> DeleteAsync(string id);
        string CreateKey<T>(params object[] param);
        string GetCurrentLang();
    }
}