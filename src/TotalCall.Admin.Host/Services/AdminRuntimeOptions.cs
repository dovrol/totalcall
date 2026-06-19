using Microsoft.Extensions.Configuration;

namespace TotalCall.Admin.Host.Services;

public sealed class AdminRuntimeOptions
{
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }

    public bool HasSupabaseUrl => !string.IsNullOrWhiteSpace(SupabaseUrl);
    public bool HasServiceRoleKey => !string.IsNullOrWhiteSpace(SupabaseSecretKey);
    public bool IsConfigured => HasSupabaseUrl && HasServiceRoleKey;

    public string SupabaseOrigin
    {
        get
        {
            if (!HasSupabaseUrl)
            {
                return "Not configured";
            }

            if (Uri.TryCreate(SupabaseUrl, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Authority);
            }

            return "Invalid URL";
        }
    }

    public static AdminRuntimeOptions FromConfiguration(IConfiguration configuration) => new()
    {
        SupabaseUrl = FirstNonEmpty(
            configuration["SUPABASE_URL"],
            configuration["Supabase:Url"]),
        SupabaseSecretKey = FirstNonEmpty(
            configuration["SUPABASE_SECRET_KEY"],
            configuration["Supabase:SecretKey"])
    };

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
