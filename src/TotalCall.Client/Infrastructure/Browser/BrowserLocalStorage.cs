using Microsoft.JSInterop;

namespace TotalCall.Client.Infrastructure.Browser;

public sealed class BrowserLocalStorage(IJSRuntime jsRuntime)
{
    public ValueTask<string?> GetItemAsync(string key, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);
    }

    public ValueTask SetItemAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value);
    }

    public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
    }

    public ValueTask<string[]> GetKeysAsync(string prefix, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeAsync<string[]>("totalCallActions.getLocalStorageKeys", cancellationToken, prefix);
    }
}
