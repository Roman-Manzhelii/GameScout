using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameScout.Domain.Enums;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameScout.Services.Http;

public class RawgService : BaseHttpService, IGameCatalogService
{
    private readonly string _apiKey;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // cache for search results
    private static readonly Dictionary<string, (DateTimeOffset exp, IReadOnlyList<GameSummary> items, int total)> _cache = new();
    private static readonly object _lock = new();
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public RawgService(HttpClient http, ILogger<RawgService> log, IConfiguration cfg) : base(http, log)
    {
        _apiKey = Environment.GetEnvironmentVariable("RAWG_API_KEY") ?? "";
        if (_http.BaseAddress is null)
        {
            var baseUrl = cfg["Http:RAWG:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("RAWG BaseUrl is missing in configuration.");
            _http.BaseAddress = new Uri(baseUrl);
        }
    }


    public async Task<(IReadOnlyList<GameSummary> Items, int Total)> SearchAsync(
        string? query, IEnumerable<string>? platforms, IEnumerable<string>? genres,
        int page, int pageSize, SortBy? sort, CancellationToken ct = default)
    {
        // ordering docs: https://api.rawg.io/docs/#operation/games_list
        var ordering = sort switch
        {
            SortBy.Name => "name",
            SortBy.Metacritic => "-metacritic",
            SortBy.ReleaseDate => "-released",
            SortBy.Rating => "-rating",
            _ => null
        };

        var qs = new List<string>
        {
            $"page={page}",
            $"page_size={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            qs.Add($"search={WebUtility.UrlEncode(query)}");
            qs.Add("search_precise=true");
            qs.Add("exclude_additions=true");
            qs.Add("search_exact=true");
        }
        if (!string.IsNullOrEmpty(ordering)) qs.Add($"ordering={ordering}");
        if (!string.IsNullOrEmpty(_apiKey)) qs.Add($"key={_apiKey}");

        var url = "games?" + string.Join("&", qs);

        // cache: return if fresh
        lock (_lock)
        {
            if (_cache.TryGetValue(url, out var c) && c.exp > DateTimeOffset.UtcNow)
                return (c.items, c.total);
        }

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<RawgListResponse>(stream, _json, ct)
                   ?? new RawgListResponse();

        var items = new List<GameSummary>(data.Results?.Count ?? 0);
        if (data.Results != null)
        {
            foreach (var g in data.Results)
            {
                DateOnly? released = null;
                if (!string.IsNullOrWhiteSpace(g.Released) && DateOnly.TryParse(g.Released, out var d))
                    released = d;

                var platformsList = new List<string>();
                if (g.Platforms != null)
                    foreach (var p in g.Platforms)
                        if (p.Platform?.Name is { Length: > 0 } n) platformsList.Add(n);

                var genresList = new List<string>();
                if (g.Genres != null)
                    foreach (var gn in g.Genres)
                        if (gn.Name is { Length: > 0 } n) genresList.Add(n);

                items.Add(new GameSummary
                {
                    Id = g.Id,
                    Name = g.Name ?? "",
                    Metacritic = g.Metacritic,
                    Released = released,
                    Platforms = platformsList,
                    Genres = genresList
                });
            }
        }

        // cache: store fresh result
        lock (_lock)
        {
            _cache[url] = (DateTimeOffset.UtcNow.Add(_ttl), items, data.Count);
        }

        return (items, data.Count);
    }

    public Task<GameDetails?> GetDetailsAsync(int id, CancellationToken ct = default)
        => Task.FromResult<GameDetails?>(null);

    private sealed class RawgListResponse
    {
        public int Count { get; set; }
        public List<RawgGame>? Results { get; set; }
    }
    private sealed class RawgGame
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Released { get; set; }
        public int? Metacritic { get; set; }
        public List<PlatformWrapper>? Platforms { get; set; }
        public List<NameWrapper>? Genres { get; set; }
    }
    private sealed class PlatformWrapper { public PlatformObj? Platform { get; set; } }
    private sealed class PlatformObj { public string? Name { get; set; } }
    private sealed class NameWrapper { public string? Name { get; set; } }
}
