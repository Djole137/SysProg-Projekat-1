using System;
using System.Net.Http;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using System.Linq;

public class ApiService
{
    private class CacheItem
    {
        public JObject ItemValue { get; set; }
        public DateTime LastAccessed { get; set; }

        public CacheItem(JObject data)
        {
            ItemValue = data;
            LastAccessed = DateTime.UtcNow;
        }
    }


    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly ConcurrentDictionary<string, CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();
    private static readonly object cacheLock = new object();
    private const int MAX_CACHE_SIZE = 6;

    public static string GetQuizData(string category, string difficulty)
    {
        string apiUrl = "https://opentdb.com/api.php?amount=10";
        if (!string.IsNullOrEmpty(category))
        {
            apiUrl += $"&category={category}";
        }
        if (!string.IsNullOrEmpty(difficulty))
        {
            apiUrl += $"&difficulty={difficulty}";
        }

        string categoryCache = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim().ToLower();
        string difficultyCache = string.IsNullOrWhiteSpace(difficulty) ? "all" : difficulty.Trim().ToLower();

        string cacheKey = $"category:{categoryCache}_difficulty:{difficultyCache}";


        if (cache.TryGetValue(cacheKey, out CacheItem cachedItem))
        {
            cachedItem.LastAccessed = DateTime.UtcNow;
            Console.WriteLine($"[CACHE HIT] Nit {System.Threading.Thread.CurrentThread.ManagedThreadId} vraća podatke.");
            return cachedItem.ItemValue.ToString();
        }

        lock (cacheLock)
        {
            try
            {
                if (cache.TryGetValue(cacheKey, out cachedItem))
                {
                    cachedItem.LastAccessed = DateTime.UtcNow;
                    Console.WriteLine($"[CACHE HIT] Nit {System.Threading.Thread.CurrentThread.ManagedThreadId} vraća podatke.");
                    return cachedItem.ItemValue.ToString();
                }

                Console.WriteLine($"[API CALL] Nit {Thread.CurrentThread.ManagedThreadId} poziva API...");
                string apiResponseString = httpClient.GetStringAsync(apiUrl).Result;
                JObject jsonResponse = JObject.Parse(apiResponseString);

                int responseCode = jsonResponse["response_code"]?.Value<int>() ?? 0;

                if (responseCode != 0 || jsonResponse["results"] == null || !jsonResponse["results"].HasValues)
                {
                    JObject negativeResponse = new JObject
                    {
                        ["error"] = "Nema rezultata za ove filtere"
                    };

                    AddToCache(cacheKey, negativeResponse);
                    return negativeResponse.ToString();
                }

                AddToCache(cacheKey, jsonResponse);
                return jsonResponse.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    private static void ManageCacheSize()
    {
        var oldestKey = cache
            .OrderBy(p => p.Value.LastAccessed)
            .Select(p => p.Key)
            .FirstOrDefault();

        if (oldestKey != null)
        {
            if (cache.TryRemove(oldestKey, out _))
            {
                Console.WriteLine($"[CACHE] Izbačen najmanje korišćen ključ: {oldestKey}");
            }
        }
    }

    private static void AddToCache(string key, JObject value)
    {
        if (cache.Count >= MAX_CACHE_SIZE && !cache.ContainsKey(key))
        {
            ManageCacheSize();
        }

        var newItem = new CacheItem(value);
        cache.AddOrUpdate(key, newItem, (k, old) => newItem);
    }
}