using System.Net.Sockets;

namespace GameScout.Services.Http;

public abstract class BaseHttpService
{
    protected readonly HttpClient _http;
    protected readonly ILogger _log;

    protected BaseHttpService(HttpClient http, ILogger log)
    {
        _http = http;
        _log = log;
    }

    protected async Task<HttpResponseMessage> GetSafeAsync(string url, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetAsync(url, ct);
        }
        catch (Exception ex) when (IsNetworkException(ex))
        {
            _log.LogError(ex, "Network unavailable while calling {Url}", url);
            throw new NetworkUnavailableException("Network unavailable.", ex);
        }
    }

    private static bool IsNetworkException(Exception ex) =>
        ex is HttpRequestException or IOException or SocketException or TaskCanceledException;
}

public sealed class NetworkUnavailableException : Exception
{
    public NetworkUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
