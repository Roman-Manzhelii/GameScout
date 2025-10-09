using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameScout.Domain.Enums;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GameScout.Services.Http;
public class RawgService : BaseHttpService, IGameCatalogService
{
    public RawgService(HttpClient http, ILogger<RawgService> log) : base(http, log) { }

    public Task<(IReadOnlyList<GameSummary> Items, int Total)> SearchAsync(
        string? query, IEnumerable<string>? platforms, IEnumerable<string>? genres,
        int page, int pageSize, SortBy? sort, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<GameDetails?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
