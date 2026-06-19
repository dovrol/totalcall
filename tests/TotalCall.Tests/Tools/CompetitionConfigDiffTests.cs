using System.Text.Json.Nodes;
using TotalCall.Operations.Competitions;

namespace TotalCall.Tests.Tools;

public sealed class CompetitionConfigDiffTests
{
    [Fact]
    public void Compare_treats_reordered_object_keys_as_identical()
    {
        var local = JsonNode.Parse("""{"id":"a","name":"A","configVersion":"1"}""");
        var remote = JsonNode.Parse("""{"configVersion":"1","name":"A","id":"a"}""");

        var result = CompetitionConfigDiff.Compare(local, remote);

        Assert.True(result.IsIdentical);
        Assert.Equal(result.LocalHash, result.RemoteHash);
        Assert.Empty(result.Differences);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Compare_reports_changed_leaf_with_path_and_values()
    {
        var local = JsonNode.Parse("""{"name":"A","meta":{"city":"Oslo"}}""");
        var remote = JsonNode.Parse("""{"name":"A","meta":{"city":"Bergen"}}""");

        var result = CompetitionConfigDiff.Compare(local, remote);

        Assert.False(result.IsIdentical);
        var entry = Assert.Single(result.Differences);
        Assert.Equal("/meta/city", entry.Path);
        Assert.Equal(ConfigDiffKind.Changed, entry.Kind);
        Assert.Equal("\"Oslo\"", entry.LocalValue);
        Assert.Equal("\"Bergen\"", entry.RemoteValue);
    }

    [Fact]
    public void Compare_flags_keys_present_on_only_one_side()
    {
        var local = JsonNode.Parse("""{"shared":1,"localExtra":true}""");
        var remote = JsonNode.Parse("""{"shared":1,"remoteExtra":"x"}""");

        var result = CompetitionConfigDiff.Compare(local, remote);

        var localOnly = Assert.Single(result.Differences, d => d.Kind == ConfigDiffKind.LocalOnly);
        Assert.Equal("/localExtra", localOnly.Path);
        Assert.Equal("true", localOnly.LocalValue);
        Assert.Null(localOnly.RemoteValue);

        var remoteOnly = Assert.Single(result.Differences, d => d.Kind == ConfigDiffKind.RemoteOnly);
        Assert.Equal("/remoteExtra", remoteOnly.Path);
        Assert.Null(remoteOnly.LocalValue);
        Assert.Equal("\"x\"", remoteOnly.RemoteValue);
    }

    [Fact]
    public void Compare_handles_array_element_changes_and_length_difference()
    {
        var local = JsonNode.Parse("""{"groups":[{"id":"g1"},{"id":"g2"}]}""");
        var remote = JsonNode.Parse("""{"groups":[{"id":"g1"}]}""");

        var result = CompetitionConfigDiff.Compare(local, remote);

        var entry = Assert.Single(result.Differences);
        Assert.Equal("/groups/1", entry.Path);
        Assert.Equal(ConfigDiffKind.LocalOnly, entry.Kind);
    }

    [Fact]
    public void Compare_reports_structural_type_change_as_changed()
    {
        var local = JsonNode.Parse("""{"value":{"nested":true}}""");
        var remote = JsonNode.Parse("""{"value":"scalar"}""");

        var result = CompetitionConfigDiff.Compare(local, remote);

        var entry = Assert.Single(result.Differences);
        Assert.Equal("/value", entry.Path);
        Assert.Equal(ConfigDiffKind.Changed, entry.Kind);
        Assert.Equal("\"scalar\"", entry.RemoteValue);
    }

    [Fact]
    public void Compare_truncates_when_difference_cap_is_reached()
    {
        var localObject = new JsonObject();
        var remoteObject = new JsonObject();
        for (var i = 0; i < 10; i++)
        {
            localObject[$"k{i}"] = i;
            remoteObject[$"k{i}"] = i + 100;
        }

        var result = CompetitionConfigDiff.Compare(localObject, remoteObject, maxDifferences: 3);

        Assert.False(result.IsIdentical);
        Assert.True(result.Truncated);
        Assert.Equal(3, result.Differences.Count);
    }

    [Fact]
    public void Compare_against_null_remote_is_not_identical()
    {
        var local = JsonNode.Parse("""{"id":"a"}""");

        var result = CompetitionConfigDiff.Compare(local, remote: null);

        Assert.False(result.IsIdentical);
        Assert.Equal(string.Empty, result.RemoteHash);
        Assert.NotEqual(string.Empty, result.LocalHash);
    }
}
