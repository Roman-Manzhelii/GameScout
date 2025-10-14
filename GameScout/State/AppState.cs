// Ref: Blazor JS interop + localStorage pattern
// https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability

using GameScout.Domain.Models;
using GameScout.Domain.Enums;
using GameScout.Services;

namespace GameScout.State;

public sealed class AppState
{
    //  Backlog
    private readonly ILocalStorage _ls;
    private bool _loaded;
    private const string BacklogKey = "gamescout.backlog.v1";
    public List<SavedItem> Backlog { get; } = new();

    public AppState(ILocalStorage ls) => _ls = ls ?? throw new ArgumentNullException(nameof(ls));

    // Read backlog from browser localStorage via ILocalStorage (JSON)
    // https://developer.mozilla.org/docs/Web/API/Window/localStorage

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var data = await _ls.GetAsync<List<SavedItem>>(BacklogKey) ?? new();
        Backlog.Clear(); Backlog.AddRange(data);
        _loaded = true;
    }

    public async Task SaveAsync() => await _ls.SetAsync(BacklogKey, Backlog);

    public async Task ToggleAsync(GameSummary g)
    {
        await EnsureLoadedAsync();
        var i = Backlog.FindIndex(x => x.GameId == g.Id);
        if (i >= 0) Backlog.RemoveAt(i);
        else Backlog.Add(new SavedItem { GameId = g.Id, Name = g.Name ?? "", Image = g.Image });
        await SaveAsync();
    }

    public async Task ToggleAsync(int id, string name, string? image)
    {
        await EnsureLoadedAsync();
        var i = Backlog.FindIndex(x => x.GameId == id);
        if (i >= 0) Backlog.RemoveAt(i);
        else Backlog.Add(new SavedItem { GameId = id, Name = name ?? "", Image = image });
        await SaveAsync();
    }

    public async Task<bool> ContainsAsync(int id)
    {
        await EnsureLoadedAsync();
        return Backlog.Any(x => x.GameId == id);
    }

    // Search cache
    public string? SearchQuery { get; private set; }
    public SortBy SearchSort { get; private set; } = SortBy.Metacritic;
    public int SearchPage { get; private set; } = 1;
    public int SearchPageSize { get; private set; } = 20;
    public int SearchTotal { get; private set; }
    public List<GameSummary> SearchItems { get; } = new();
    public bool HasSearch => SearchItems.Count > 0;

    public void SetSearchResults(
        string query,
        IEnumerable<GameSummary> items,
        int total,
        SortBy sort,
        int page,
        int pageSize)
    {
        SearchQuery = query;
        SearchSort = sort;
        SearchPage = page;
        SearchPageSize = pageSize;
        SearchTotal = total;

        SearchItems.Clear();
        SearchItems.AddRange(items);
    }

    public void ClearSearch()
    {
        SearchQuery = null;
        SearchSort = SortBy.Metacritic;
        SearchPage = 1;
        SearchPageSize = 20;
        SearchTotal = 0;
        SearchItems.Clear();
    }
}
