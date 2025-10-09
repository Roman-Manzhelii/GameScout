using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameScout.Domain.Models;
using GameScout.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GameScout.Services.Http;
public class CheapSharkService : BaseHttpService, IDealsService
{
    public CheapSharkService(HttpClient http, ILogger<CheapSharkService> log) : base(http, log) { }

    public Task<IReadOnlyList<Deal>> GetDealsByTitleAsync(string title, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Deal>> GetTopDealsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
