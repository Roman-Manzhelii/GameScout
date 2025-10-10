using GameScout.Domain.Models;

namespace GameScout.Services.Abstractions;
public interface IDealsService
{
    Task<IReadOnlyList<Deal>> GetDealsByTitleAsync(string title, CancellationToken ct = default);
    Task<IReadOnlyList<Deal>> GetTopDealsAsync(CancellationToken ct = default);
}
