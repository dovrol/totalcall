using System.Net.Http.Json;
using TotalCall.Core.Domain.Releases;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Application.Services;

/// <summary>
/// Loads release notes from a static JSON file and tracks the last version
/// the user has acknowledged via localStorage.
/// </summary>
public sealed class ChangelogService(
    HttpClient httpClient,
    BrowserLocalStorage localStorage,
    AppInfoService appInfo)
{
    private ChangelogDocument? cachedDocument;

    /// <summary>
    /// Returns entries strictly newer than the user's last acknowledged version.
    /// On a first-ever visit (no stored version) the list is empty — we silently
    /// onboard the user instead of dumping the full history on them.
    /// </summary>
    public async Task<IReadOnlyList<ChangelogEntry>> GetUnseenEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(cancellationToken);

        if (document.Entries.Count == 0)
        {
            return [];
        }

        var storedVersion = await localStorage.GetItemAsync(
            LocalStorageKeys.LastSeenAppVersion,
            cancellationToken);

        // First visit: no stored version yet. Mark the current build as seen
        // and skip showing the modal so we don't overwhelm a brand new user.
        if (string.IsNullOrWhiteSpace(storedVersion))
        {
            await MarkCurrentVersionSeenAsync(cancellationToken);
            return [];
        }

        if (!SemanticVersion.TryParse(storedVersion, out var lastSeen))
        {
            // Stored value is corrupted/old format — treat as a clean slate.
            await MarkCurrentVersionSeenAsync(cancellationToken);
            return [];
        }

        var unseen = new List<ChangelogEntry>();

        foreach (var entry in document.Entries)
        {
            if (!SemanticVersion.TryParse(entry.Version, out var entryVersion))
            {
                continue;
            }

            if (entryVersion.CompareTo(lastSeen) > 0)
            {
                unseen.Add(entry);
            }
        }

        // Newest first.
        unseen.Sort((left, right) =>
        {
            SemanticVersion.TryParse(left.Version, out var leftVersion);
            SemanticVersion.TryParse(right.Version, out var rightVersion);
            return rightVersion.CompareTo(leftVersion);
        });

        return unseen;
    }

    /// <summary>
    /// Returns all changelog entries sorted newest-first, regardless of seen state.
    /// </summary>
    public async Task<IReadOnlyList<ChangelogEntry>> GetAllEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(cancellationToken);

        var sorted = document.Entries.ToList();

        sorted.Sort((left, right) =>
        {
            SemanticVersion.TryParse(left.Version, out var leftVersion);
            SemanticVersion.TryParse(right.Version, out var rightVersion);
            return rightVersion.CompareTo(leftVersion);
        });

        return sorted;
    }

    public Task MarkCurrentVersionSeenAsync(CancellationToken cancellationToken = default)
    {
        return localStorage.SetItemAsync(
            LocalStorageKeys.LastSeenAppVersion,
            appInfo.AppVersion,
            cancellationToken).AsTask();
    }

    private async Task<ChangelogDocument> LoadDocumentAsync(CancellationToken cancellationToken)
    {
        if (cachedDocument is not null)
        {
            return cachedDocument;
        }

        try
        {
            var document = await httpClient.GetFromJsonAsync<ChangelogDocument>(
                JsonDataPaths.Changelog,
                JsonDataOptions.SerializerOptions,
                cancellationToken);

            cachedDocument = document ?? new ChangelogDocument();
        }
        catch
        {
            // Changelog is a non-critical surface — if the JSON is missing or
            // unparseable we silently degrade and skip the modal.
            cachedDocument = new ChangelogDocument();
        }

        return cachedDocument;
    }
}
