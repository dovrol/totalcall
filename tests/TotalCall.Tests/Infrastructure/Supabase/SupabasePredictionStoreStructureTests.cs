namespace TotalCall.Tests.Infrastructure.Supabase;

public sealed class SupabasePredictionStoreStructureTests
{
    [Fact]
    public void SupabasePredictionStore_IsFacadeWithoutHttpEndpointDetails()
    {
        var source = ReadSupabaseSource("SupabasePredictionStore.cs");

        Assert.Contains("SupabasePredictionDraftApi", source);
        Assert.Contains("SupabasePredictionSubmitApi", source);
        Assert.Contains("SupabaseAuthenticatedScoreApi", source);
        Assert.Contains("SupabasePublicPredictionApi", source);
        Assert.DoesNotContain("rest/v1/", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestMessage", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JsonContent", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicPredictionApi_DoesNotUseAuthenticatedRequestFlow()
    {
        var source = ReadSupabaseSource("SupabasePublicPredictionApi.cs");

        Assert.Contains("BuildPublicRequest", source);
        Assert.DoesNotContain("BuildAuthenticatedRequest", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetAuthenticatedContextAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthenticatedPredictionApis_DoNotUsePublicRequestFlow()
    {
        foreach (var fileName in new[]
                 {
                     "SupabasePredictionDraftApi.cs",
                     "SupabasePredictionSubmitApi.cs",
                     "SupabaseAuthenticatedScoreApi.cs"
                 })
        {
            var source = ReadSupabaseSource(fileName);

            Assert.Contains("GetAuthenticatedContextAsync", source);
            Assert.Contains("BuildAuthenticatedRequest", source);
            Assert.DoesNotContain("BuildPublicRequest", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadSupabaseSource(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/TotalCall.Client/Infrastructure/Supabase",
            fileName));

        return File.ReadAllText(path);
    }
}
