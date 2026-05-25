using Microsoft.JSInterop;

namespace TotalCall.Client.Infrastructure.Browser;

public sealed class BrowserFileActions(IJSRuntime jsRuntime)
{
    public async ValueTask<bool> CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>(
                "totalCallActions.copyText",
                cancellationToken,
                text);
        }
        catch
        {
            return false;
        }
    }

    public ValueTask DownloadTextFileAsync(
        string fileName,
        string content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeVoidAsync(
            "totalCallActions.downloadTextFile",
            cancellationToken,
            fileName,
            content,
            contentType);
    }
}
