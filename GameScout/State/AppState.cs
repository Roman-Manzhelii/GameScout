using GameScout.Domain.Models;
using GameScout.Services;

namespace GameScout.State;

public sealed class AppState
{
    private readonly ILocalStorage _ls;
    private bool _loaded;
    private const string Key = "gamescout.backlog.v1";

    public List<SavedItem> Backlog { get; } = new();

    public AppState(ILocalStorage ls) => _ls = ls;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var data = await _ls.GetAsync<List<SavedItem>>(Key) ?? new();
        Backlog.Clear(); Backlog.AddRange(data);
        _loaded = true;
    }

    public async Task SaveAsync() => await _ls.SetAsync(Key, Backlog);

    public async Task ToggleAsync(GameSummary g)
    {
        await EnsureLoadedAsync();
        var i = Backlog.FindIndex(x => x.GameId == g.Id);
        if (i >= 0) Backlog.RemoveAt(i);
        else Backlog.Add(new SavedItem { GameId = g.Id, Name = g.Name ?? "" });
        await SaveAsync();
    }

    public async Task<bool> ContainsAsync(int id)
    {
        await EnsureLoadedAsync();
        return Backlog.Any(x => x.GameId == id);
    }
}
