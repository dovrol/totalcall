using Microsoft.JSInterop;

namespace TotalCall.Client.Infrastructure.Browser;

public sealed class BrowserFileActions(IJSRuntime jsRuntime)
{
    public ValueTask CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return jsRuntime.InvokeVoidAsync(
            "totalCallActions.copyText",
            cancellationToken,
            text);
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
