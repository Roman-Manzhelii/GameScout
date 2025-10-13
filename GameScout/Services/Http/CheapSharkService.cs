using System.Net;
using System.Text.Json;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using System.Text.Json.Serialization;

namespace GameScout.Services.Http;

public class CheapSharkService : BaseHttpService, IDealsService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, (DateTimeOffset exp, IReadOnlyList<Deal> items)> _cache = new();
    private static readonly object _lock = new();
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, string> _storeNames = new(); // id -> name
    private static DateTimeOffset _storesExp = DateTimeOffset.MinValue;

    public CheapSharkService(HttpClient http, ILogger<CheapSharkService> log, IConfiguration cfg)
        : base(http, log)
    {
        if (_http.BaseAddress is null)
        {
            var baseUrl = cfg["Http:CheapShark:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("CheapShark BaseUrl is missing in configuration.");
            _http.BaseAddress = new Uri(baseUrl);
        }
    }


    public async Task<IReadOnlyList<Deal>> GetDealsByTitleAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return Array.Empty<Deal>();
        await EnsureStoresAsync(ct);

        var cacheKey = "game:" + title.Trim().ToUpperInvariant();
        if (TryGet(cacheKey, out var cached)) return cached;

        var searchUrl = $"games?title={WebUtility.UrlEncode(title)}&limit=5&exact=1";
        using (var resp = await _http.GetAsync(searchUrl, ct))
        {
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            var found = await JsonSerializer.DeserializeAsync<List<RawGameSearch>>(s, _json, ct) ?? new();
            var gameId = found.FirstOrDefault()?.GameID;

            if (string.IsNullOrWhiteSpace(gameId))
            {
                Set(cacheKey, Array.Empty<Deal>());
                return Array.Empty<Deal>();
            }

            var gameUrl = $"games?id={WebUtility.UrlEncode(gameId)}";
        using var resp2 = await _http.GetAsync(gameUrl, ct);
            resp2.EnsureSuccessStatusCode();
            await using var s2 = await resp2.Content.ReadAsStreamAsync(ct);
            var byId = await JsonSerializer.DeserializeAsync<RawGameById>(s2, _json, ct) ?? new();

            var cardThumb = byId.Info?.Thumb;
            var list = new List<Deal>();
            if (byId.Deals is not null)
            {
                foreach (var d in byId.Deals)
                {
                    if (string.IsNullOrWhiteSpace(d.StoreID) || string.IsNullOrWhiteSpace(d.DealID))
                        continue;

                    var storeName = _storeNames.TryGetValue(d.StoreID!, out var n) ? n : $"Store {d.StoreID}";
                    var price = D(d.Price);
                    var normal = D(d.RetailPrice);
                    var savings = normal > 0m ? (normal - price) / normal * 100m : 0m;

                    list.Add(new Deal
                    {
                        Store = storeName ?? "Store",
                        Price = price,
                        NormalPrice = normal,
                        Savings = savings,
                        Url = $"https://www.cheapshark.com/redirect?dealID={d.DealID}",
                        Image = cardThumb
                    });
                }
            }

            list = list
                .OrderByDescending(x => x.Savings)
                .ThenBy(x => x.Price)
                .ToList();

            Set(cacheKey, list);
            return list;
        }
    }

    public async Task<IReadOnlyList<Deal>> GetTopDealsAsync(CancellationToken ct = default)
    {
        await EnsureStoresAsync(ct);

        var url = "deals?pageSize=120&sortBy=DealRating";
        if (TryGet(url, out var cached)) return cached;

        using var resp = await GetSafeAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var raw = await JsonSerializer.DeserializeAsync<List<RawDeal>>(s, _json, ct) ?? new();
        var items = MapBestPerTitle(raw);
        Set(url, items);
        return items;
    }

    private async Task EnsureStoresAsync(CancellationToken ct = default)
    {
        if (_storesExp > DateTimeOffset.UtcNow && _storeNames.Count > 0) return;

        using var resp = await GetSafeAsync("stores", ct);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var stores = await JsonSerializer.DeserializeAsync<List<RawStore>>(s, _json, ct) ?? new();

        _storeNames.Clear();
        foreach (var st in stores.Where(x => x.IsActive == 1))
            if (!string.IsNullOrWhiteSpace(st.StoreID) && !string.IsNullOrWhiteSpace(st.StoreName))
                _storeNames[st.StoreID!] = st.StoreName!;
        _storesExp = DateTimeOffset.UtcNow.AddHours(24);
    }


    private List<Deal> MapBestPerTitle(List<RawDeal> src)
    {
        static decimal P(string? s) => decimal.TryParse(s, out var v) ? v : 0m;

        var groups = src
            .Where(d => !string.IsNullOrWhiteSpace(d.DealID)
                        && !string.IsNullOrWhiteSpace(d.StoreID)
                        && !string.IsNullOrWhiteSpace(d.Title))
            .GroupBy(d => d.Title!, StringComparer.OrdinalIgnoreCase);

        var list = new List<Deal>();
        foreach (var g in groups)
        {
            var best = g.OrderBy(d => P(d.SalePrice)).First();
            var storeName = _storeNames.TryGetValue(best.StoreID!, out var n) ? n : $"Store {best.StoreID}";
            list.Add(new Deal
            {
                Store = $"{best.Title} | {storeName}",
                Price = P(best.SalePrice),
                NormalPrice = P(best.NormalPrice),
                Savings = P(best.Savings),
                Url = $"https://www.cheapshark.com/redirect?dealID={best.DealID}",
                Image = best.Thumb
            });
        }

        list.Sort((a, b) => a.Price.CompareTo(b.Price));
        return list;
    }

    private static decimal D(string? s) => decimal.TryParse(s, out var v) ? v : 0m;

    private static bool TryGet(string key, out IReadOnlyList<Deal> items)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var c) && c.exp > DateTimeOffset.UtcNow)
            {
                items = c.items;
                return true;
            }
        }
        items = Array.Empty<Deal>();
        return false;
    }

    private static void Set(string key, IReadOnlyList<Deal> items)
    {
        lock (_lock)
        {
            _cache[key] = (DateTimeOffset.UtcNow.Add(_ttl), items);
        }
    }


    private sealed class RawStore
    {
        [JsonPropertyName("storeID")] public string? StoreID { get; set; }
        [JsonPropertyName("storeName")] public string? StoreName { get; set; }
        [JsonPropertyName("isActive")] public int IsActive { get; set; }
    }

    private sealed class RawDeal
    {
        public string? DealID { get; set; }
        public string? StoreID { get; set; }
        public string? Title { get; set; }
        public string? SalePrice { get; set; }
        public string? NormalPrice { get; set; }
        public string? Savings { get; set; }
        public string? Thumb { get; set; }
    }

    private sealed class RawGameSearch
    {
        [JsonPropertyName("gameID")] public string? GameID { get; set; }
        [JsonPropertyName("external")] public string? External { get; set; }
    }

    private sealed class RawGameById
    {
        [JsonPropertyName("info")] public RawGameInfo? Info { get; set; }
        [JsonPropertyName("deals")] public List<RawGameDeal>? Deals { get; set; }
    }

    private sealed class RawGameInfo
    {
        [JsonPropertyName("thumb")] public string? Thumb { get; set; }
    }

    private sealed class RawGameDeal
    {
        [JsonPropertyName("dealID")] public string? DealID { get; set; }
        [JsonPropertyName("storeID")] public string? StoreID { get; set; }
        [JsonPropertyName("price")] public string? Price { get; set; }
        [JsonPropertyName("retailPrice")] public string? RetailPrice { get; set; }
    }
}