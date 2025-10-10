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


    private async Task EnsureStoresAsync(CancellationToken ct = default)
    {
        if (_storesExp > DateTimeOffset.UtcNow && _storeNames.Count > 0) return;

        using var resp = await _http.GetAsync("stores", ct); // https://www.cheapshark.com/api/1.0/stores
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var stores = await JsonSerializer.DeserializeAsync<List<RawStore>>(s, _json, ct) ?? new();

        _storeNames.Clear();
        foreach (var st in stores.Where(x => x.IsActive == 1))
            if (!string.IsNullOrWhiteSpace(st.StoreID) && !string.IsNullOrWhiteSpace(st.StoreName))
                _storeNames[st.StoreID!] = st.StoreName!;
        _storesExp = DateTimeOffset.UtcNow.AddHours(24);
    }

    public async Task<IReadOnlyList<Deal>> GetDealsByTitleAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return Array.Empty<Deal>();
        await EnsureStoresAsync(ct);

        var url = $"deals?title={WebUtility.UrlEncode(title)}&pageSize=20&sortBy=DealRating";
        if (TryGet(url, out var cached)) return cached;

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var raw = await JsonSerializer.DeserializeAsync<List<RawDeal>>(s, _json, ct) ?? new();
        var items = Map(raw);
        Set(url, items);
        return items;
    }

    public async Task<IReadOnlyList<Deal>> GetTopDealsAsync(CancellationToken ct = default)
    {
        await EnsureStoresAsync(ct);

        var url = "deals?pageSize=30&sortBy=DealRating";
        if (TryGet(url, out var cached)) return cached;

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var raw = await JsonSerializer.DeserializeAsync<List<RawDeal>>(s, _json, ct) ?? new();
        var items = Map(raw);
        Set(url, items);
        return items;
    }

    private static List<Deal> Map(List<RawDeal> src)
    {
        static decimal P(string? s) => decimal.TryParse(s, out var v) ? v : 0m;

        var bestPerStore = src
            .Where(d => !string.IsNullOrWhiteSpace(d.DealID) && !string.IsNullOrWhiteSpace(d.StoreID))
            .GroupBy(d => d.StoreID!)
            .Select(g => g.OrderBy(d => P(d.SalePrice)).First())
            .ToList();

        var list = new List<Deal>(bestPerStore.Count);
        foreach (var d in bestPerStore)
        {
            var name = (_storeNames.TryGetValue(d.StoreID!, out var n) ? n : $"Store {d.StoreID}") ?? "Store";
            list.Add(new Deal
            {
                Store = name,
                Price = P(d.SalePrice),
                NormalPrice = P(d.NormalPrice),
                Savings = P(d.Savings),
                Url = $"https://www.cheapshark.com/redirect?dealID={d.DealID}"
            });
        }

        list.Sort((a, b) => a.Price.CompareTo(b.Price));
        return list;
    }

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
        public string? SalePrice { get; set; }
        public string? NormalPrice { get; set; }
        public string? Savings { get; set; }
    }
}