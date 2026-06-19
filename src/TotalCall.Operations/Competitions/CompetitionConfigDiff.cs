using System.Text.Json.Nodes;

namespace TotalCall.Operations.Competitions;

public enum ConfigDiffKind
{
    // Leaf value differs between local JSON and the active remote version.
    Changed,

    // Present in the local JSON but absent from the active remote version
    // (publishing the local JSON would add it).
    LocalOnly,

    // Present in the active remote version but absent from the local JSON
    // (publishing the local JSON would drop it).
    RemoteOnly
}

public sealed record ConfigDiffEntry(
    string Path,
    ConfigDiffKind Kind,
    string? LocalValue,
    string? RemoteValue);

public sealed record ConfigDiffResult(
    bool IsIdentical,
    string LocalHash,
    string RemoteHash,
    IReadOnlyList<ConfigDiffEntry> Differences,
    bool Truncated);

// Pure structural diff between a local competition config JSON and the active
// remote version's config. Objects are compared order-insensitively and arrays
// by index, mirroring CompetitionConfigHasher's canonical form so the hash
// equality check and the per-path diff agree.
public static class CompetitionConfigDiff
{
    private const int MaxValueLength = 160;

    public static ConfigDiffResult Compare(
        JsonNode? local,
        JsonNode? remote,
        int maxDifferences = 200)
    {
        var localHash = local is null ? string.Empty : CompetitionConfigHasher.Compute(local);
        var remoteHash = remote is null ? string.Empty : CompetitionConfigHasher.Compute(remote);

        if (string.Equals(localHash, remoteHash, StringComparison.Ordinal))
        {
            return new ConfigDiffResult(true, localHash, remoteHash, [], Truncated: false);
        }

        var differences = new List<ConfigDiffEntry>();
        var completed = Walk("", local, remote, differences, Math.Max(1, maxDifferences));
        return new ConfigDiffResult(false, localHash, remoteHash, differences, Truncated: !completed);
    }

    // Returns false when the difference cap was hit (result is truncated).
    private static bool Walk(
        string path,
        JsonNode? local,
        JsonNode? remote,
        List<ConfigDiffEntry> differences,
        int max)
    {
        if (differences.Count >= max)
        {
            return false;
        }

        if (JsonNode.DeepEquals(local, remote))
        {
            return true;
        }

        if (local is JsonObject localObject && remote is JsonObject remoteObject)
        {
            return WalkObject(path, localObject, remoteObject, differences, max);
        }

        if (local is JsonArray localArray && remote is JsonArray remoteArray)
        {
            return WalkArray(path, localArray, remoteArray, differences, max);
        }

        // Leaf value, or a structural type change (e.g. object vs scalar).
        differences.Add(new ConfigDiffEntry(
            NormalizePath(path),
            ConfigDiffKind.Changed,
            Describe(local),
            Describe(remote)));
        return differences.Count < max;
    }

    private static bool WalkObject(
        string path,
        JsonObject local,
        JsonObject remote,
        List<ConfigDiffEntry> differences,
        int max)
    {
        var keys = local.Select(property => property.Key)
            .Union(remote.Select(property => property.Key), StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (differences.Count >= max)
            {
                return false;
            }

            var hasLocal = local.TryGetPropertyValue(key, out var localValue);
            var hasRemote = remote.TryGetPropertyValue(key, out var remoteValue);
            var childPath = $"{path}/{key}";

            if (hasLocal && !hasRemote)
            {
                differences.Add(new ConfigDiffEntry(childPath, ConfigDiffKind.LocalOnly, Describe(localValue), null));
            }
            else if (!hasLocal && hasRemote)
            {
                differences.Add(new ConfigDiffEntry(childPath, ConfigDiffKind.RemoteOnly, null, Describe(remoteValue)));
            }
            else if (!Walk(childPath, localValue, remoteValue, differences, max))
            {
                return false;
            }
        }

        return differences.Count < max;
    }

    private static bool WalkArray(
        string path,
        JsonArray local,
        JsonArray remote,
        List<ConfigDiffEntry> differences,
        int max)
    {
        var count = Math.Max(local.Count, remote.Count);
        for (var index = 0; index < count; index++)
        {
            if (differences.Count >= max)
            {
                return false;
            }

            var childPath = $"{path}/{index}";
            if (index >= remote.Count)
            {
                differences.Add(new ConfigDiffEntry(childPath, ConfigDiffKind.LocalOnly, Describe(local[index]), null));
            }
            else if (index >= local.Count)
            {
                differences.Add(new ConfigDiffEntry(childPath, ConfigDiffKind.RemoteOnly, null, Describe(remote[index])));
            }
            else if (!Walk(childPath, local[index], remote[index], differences, max))
            {
                return false;
            }
        }

        return differences.Count < max;
    }

    private static string NormalizePath(string path) => string.IsNullOrEmpty(path) ? "/" : path;

    private static string Describe(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        var text = node.ToJsonString();
        return text.Length <= MaxValueLength ? text : text[..MaxValueLength] + "…";
    }
}
