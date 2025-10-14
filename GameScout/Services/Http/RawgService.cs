using System.Net;
using System.Text.Json;
using GameScout.Domain.Enums;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameScout.Services.Http;

public class RawgService : BaseHttpService, IGameCatalogService
{
    private readonly string _apiKey;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // cache for search results
    private static readonly Dictionary<string, (DateTimeOffset exp, IReadOnlyList<GameSummary> items, int total)> _cache = new();
    private static readonly object _lock = new();
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    // Ref: RAWG API base URL + key)
    // https://api.rawg.io/docs
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

    // Games list + filtering/paging (?search=, page, page_size)
    // https://api.rawg.io/docs/#operation/games_list
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

        using var resp = await GetSafeAsync(url, ct);
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
                    Genres = genresList,
                    Image = g.BackgroundImage
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

    // details cache
    private static readonly Dictionary<int, (DateTimeOffset exp, GameDetails data)> _detailsCache = new();
    private static readonly TimeSpan _detailsTtl = TimeSpan.FromMinutes(30);

    // Game details endpoint: GET /games/{id}?key=...
    // https://api.rawg.io/docs/#operation/games_read
    public async Task<GameDetails?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        lock (_lock)
            if (_detailsCache.TryGetValue(id, out var c) && c.exp > DateTimeOffset.UtcNow)
                return c.data;

        var keyQs = string.IsNullOrEmpty(_apiKey) ? "" : $"?key={_apiKey}";

        // main details
        using var resp = await GetSafeAsync($"games/{id}{keyQs}", ct);
        resp.EnsureSuccessStatusCode();
        await using var s1 = await resp.Content.ReadAsStreamAsync(ct);
        var d = await JsonSerializer.DeserializeAsync<RawgDetails>(s1, _json, ct);
        if (d is null) return null;

        // Screenshots endpoint: GET /games/{id}/screenshots?key=...
        // https://api.rawg.io/docs/#operation/screenshots_list

        using var resp2 = await GetSafeAsync($"games/{id}/screenshots{keyQs}", ct);
        resp2.EnsureSuccessStatusCode();
        await using var s2 = await resp2.Content.ReadAsStreamAsync(ct);
        var shots = await JsonSerializer.DeserializeAsync<RawgScreens>(s2, _json, ct);

        var model = new GameDetails
        {
            Id = d.Id,
            Name = d.Name ?? "",
            Description = !string.IsNullOrWhiteSpace(d.DescriptionRaw)
                ? d.DescriptionRaw!
                : StripHtml(d.Description),
            Metacritic = d.Metacritic,
            Platforms = d.Platforms?.ConvertAll(p => p.Platform?.Name ?? "").FindAll(x => !string.IsNullOrWhiteSpace(x)) ?? new(),
            Genres = d.Genres?.ConvertAll(g => g.Name ?? "").FindAll(x => !string.IsNullOrWhiteSpace(x)) ?? new(),
            Screenshots = shots?.Results?.ConvertAll(x => x.Image ?? "").FindAll(x => !string.IsNullOrWhiteSpace(x)) ?? new()
        };

        lock (_lock) _detailsCache[id] = (DateTimeOffset.UtcNow.Add(_detailsTtl), model);
        return model;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = Regex.Replace(html, @"<(br|BR)\s*/?>", "\n");
        s = Regex.Replace(s, @"</p\s*>", "\n\n");
        s = Regex.Replace(s, @"<p(\s[^>]*)?>", string.Empty);
        s = Regex.Replace(s, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(s).Trim();
    }


    // DTOs for details
    private sealed class RawgDetails
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? DescriptionRaw { get; set; }
        public int? Metacritic { get; set; }
        public List<PlatformWrapper>? Platforms { get; set; }
        public List<NameWrapper>? Genres { get; set; }
    }
    private sealed class RawgScreens
    {
        public List<Shot>? Results { get; set; }
        public sealed class Shot { public string? Image { get; set; } }
    }

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
        [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
    }
    private sealed class PlatformWrapper { public PlatformObj? Platform { get; set; } }
    private sealed class PlatformObj { public string? Name { get; set; } }
    private sealed class NameWrapper { public string? Name { get; set; } }
}
