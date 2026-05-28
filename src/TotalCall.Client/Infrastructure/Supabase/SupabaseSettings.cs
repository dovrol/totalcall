namespace TotalCall.Client.Infrastructure.Supabase;

public sealed class SupabaseSettings
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;
}
