using GameScout.Domain.Enums;
using GameScout.Domain.Models;
using GameScout.Services;
using GameScout.Services.Http;
using GameScout.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace GameScout.Tests
{
    [TestFixture]
    public class ProjectTests
    {
        private sealed class NoopLocalStorage : ILocalStorage
        {
            public Task SetAsync<T>(string key, T value) => Task.CompletedTask;
            public Task<T?> GetAsync<T>(string key) => Task.FromResult(default(T));
        }

        private sealed class FakeJs : IJSRuntime
        {
            public string? LastIdentifier { get; private set; }
            public object?[]? LastArgs { get; private set; }
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
                => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
            {
                LastIdentifier = identifier; LastArgs = args;
                return ValueTask.FromResult(default(TValue)!);
            }
        }

        private sealed class NoopLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) => new D();
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            private sealed class D : IDisposable { public void Dispose() { } }
        }

        [Test]
        public async Task LocalStorage_Get_ReturnsNull_WhenMissing()
        {
            var js = new FakeJs();
            var ls = new LocalStorage(js);
            var value = await ls.GetAsync<SavedItem>("absent");
            Assert.That(value, Is.Null);
        }

        [Test]
        public async Task LocalStorage_Set_Calls_Browser_With_Key()
        {
            var js = new FakeJs();
            var ls = new LocalStorage(js);
            await ls.SetAsync("test-key", new SavedItem { GameId = 1, Name = "A" });
            Assert.That(js.LastIdentifier, Is.EqualTo("localStorage.setItem"));
            Assert.That(js.LastArgs![0], Is.EqualTo("test-key"));
        }

        [Test]
        public void CheapSharkService_Sets_BaseAddress_From_Config()
        {
            var http = new HttpClient();
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Http:CheapShark:BaseUrl"] = "https://cheap/" })
                .Build();
            var _ = new CheapSharkService(http, new NoopLogger<CheapSharkService>(), cfg);
            Assert.That(http.BaseAddress, Is.EqualTo(new Uri("https://cheap/")));
        }

        [Test]
        public async Task CheapSharkService_Empty_Title_Returns_Empty_List()
        {
            var http = new HttpClient { BaseAddress = new Uri("https://cheap/") };
            var cfg = new ConfigurationBuilder().Build();
            var svc = new CheapSharkService(http, new NoopLogger<CheapSharkService>(), cfg);
            var items = await svc.GetDealsByTitleAsync("   ");
            Assert.That(items.Count, Is.EqualTo(0));
        }

        [Test]
        public void SavedItem_Defaults_Minimal()
        {
            var s = new SavedItem();
            Assert.That(s.Name, Is.EqualTo(""));
            Assert.That(s.Status, Is.EqualTo(BacklogStatus.Backlog));
        }

        [Test]
        public void GameSummary_Collections_Are_NotNull()
        {
            var g = new GameSummary();
            Assert.That(g.Platforms, Is.Not.Null);
            Assert.That(g.Genres, Is.Not.Null);
        }

        [Test]
        public void AppState_ClearSearch_Resets_Defaults()
        {
            var state = new AppState(new NoopLocalStorage());
            state.SetSearchResults("q", new List<GameSummary> { new GameSummary { Id = 1 } }, 1, SortBy.Name, 2, 5);
            state.ClearSearch();
            Assert.That(state.SearchQuery, Is.Null);
            Assert.That(state.SearchSort, Is.EqualTo(SortBy.Metacritic));
            Assert.That(state.SearchPage, Is.EqualTo(1));
            Assert.That(state.SearchPageSize, Is.EqualTo(20));
            Assert.That(state.SearchTotal, Is.EqualTo(0));
            Assert.That(state.SearchItems.Count, Is.EqualTo(0));
            Assert.That(state.HasSearch, Is.False);
        }

        [Test]
        public async Task AppState_Toggle_Adds_Then_Removes()
        {
            var state = new AppState(new NoopLocalStorage());
            await state.ToggleAsync(7, "Halo", null);
            Assert.That(state.Backlog.Count, Is.EqualTo(1));
            await state.ToggleAsync(7, "Halo", null);
            Assert.That(state.Backlog.Count, Is.EqualTo(0));
        }
    }
}
