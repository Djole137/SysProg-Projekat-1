using System;
using System.Net.Http;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using System.Linq;

public class ApiService
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly ConcurrentDictionary<string, JObject> cache = new ConcurrentDictionary<string, JObject>();
    private static readonly ConcurrentQueue<string> cacheKeys = new ConcurrentQueue<string>();
    private static readonly object cacheLock = new object();
    private const int MAX_CACHE_SIZE = 15;

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


        if (cache.TryGetValue(cacheKey, out JObject cachedData))
        {
            Console.WriteLine($"[CACHE HIT] Nit {System.Threading.Thread.CurrentThread.ManagedThreadId} vraća podatke.");
            return cachedData.ToString();
        }

        lock (cacheLock)
        {
            try
            {
                if (cache.TryGetValue(cacheKey, out cachedData))
                {
                    Console.WriteLine($"[CACHE HIT] Nit {System.Threading.Thread.CurrentThread.ManagedThreadId} vraća podatke.");
                    return cachedData.ToString();
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
        while (cache.Count > MAX_CACHE_SIZE)
        {
            if (cacheKeys.TryDequeue(out string oldestKey))
            {
                if (cache.TryRemove(oldestKey, out _))
                {
                    Console.WriteLine($"[CACHE] Oslobođen je prostor: {oldestKey}");
                }
            }
            else
            {
                break;
            }
        }
    }

    private static void AddToCache(string key, JObject value)
    {
        if (cache.TryAdd(key, value))
        {
            cacheKeys.Enqueue(key);
            ManageCacheSize();
        }
    }
}