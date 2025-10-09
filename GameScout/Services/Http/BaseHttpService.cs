using System.Net.Http;
using Microsoft.Extensions.Logging;

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
}
