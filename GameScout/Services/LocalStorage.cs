using System.Text.Json;
using Microsoft.JSInterop;

namespace GameScout.Services;

public interface ILocalStorage
{
    Task SetAsync<T>(string key, T value);
    Task<T?> GetAsync<T>(string key);
}

public sealed class LocalStorage : ILocalStorage
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    public LocalStorage(IJSRuntime js) => _js = js;

    public Task SetAsync<T>(string key, T value) =>
        _js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value, _json)).AsTask();

    public async Task<T?> GetAsync<T>(string key)
    {
        var s = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        return string.IsNullOrWhiteSpace(s) ? default : JsonSerializer.Deserialize<T>(s!, _json);
    }
}
