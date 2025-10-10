using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GameScout.Services.Http;

public class CheapSharkService : BaseHttpService, IDealsService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, (DateTimeOffset exp, IReadOnlyList<Deal> items)> _cache = new();
    private static readonly object _lock = new();
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

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
        var list = new List<Deal>(src.Count);
        foreach (var d in src)
        {
            if (string.IsNullOrWhiteSpace(d.DealID)) continue;
            list.Add(new Deal
            {
                Store = d.StoreID ?? "Store",
                Price = decimal.TryParse(d.SalePrice, out var sp) ? sp : 0m,
                NormalPrice = decimal.TryParse(d.NormalPrice, out var np) ? np : 0m,
                Savings = decimal.TryParse(d.Savings, out var sv) ? sv : 0m,
                Url = $"https://www.cheapshark.com/redirect?dealID={d.DealID}"
            });
        }
        return list;
    }

    private static bool TryGet(string key, out IReadOnlyList<Deal> items)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var c) && c.exp > DateTimeOffset.UtcNow)
            { items = c.items; return true; }
        }
        items = Array.Empty<Deal>();
        return false;
    }
    private static void Set(string key, IReadOnlyList<Deal> items)
    {
        lock (_lock) _cache[key] = (DateTimeOffset.UtcNow.Add(_ttl), items);
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
