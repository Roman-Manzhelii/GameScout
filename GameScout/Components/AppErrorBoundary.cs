using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using GameScout.Services.Http;

namespace GameScout.Components;

public sealed class AppErrorBoundary : ErrorBoundary
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        var e = exception;
        while (e.InnerException is not null) e = e.InnerException;

        if (e is NetworkUnavailableException)
            Nav.NavigateTo("/404", replace: true);

        return Task.CompletedTask;
    }
}
