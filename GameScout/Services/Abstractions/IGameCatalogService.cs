using GameScout.Domain.Enums;
using GameScout.Domain.Models;

namespace GameScout.Services.Abstractions;

// Ref: Contracts for RAWG/CheapShark services
public interface IGameCatalogService
{
    Task<(IReadOnlyList<GameSummary> Items, int Total)> SearchAsync(
        string? query, IEnumerable<string>? platforms, IEnumerable<string>? genres,
        int page, int pageSize, SortBy? sort, CancellationToken ct = default);

    Task<GameDetails?> GetDetailsAsync(int id, CancellationToken ct = default);
}
