namespace TotalCall.Client.Domain.Releases;

public sealed record LocalizedText
{
    public string? Pl { get; init; }

    public string? En { get; init; }

    public string Resolve(string cultureName)
    {
        var isEnglish = cultureName?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;

        if (isEnglish)
        {
            return !string.IsNullOrWhiteSpace(En) ? En : Pl ?? string.Empty;
        }

        return !string.IsNullOrWhiteSpace(Pl) ? Pl : En ?? string.Empty;
    }
}
